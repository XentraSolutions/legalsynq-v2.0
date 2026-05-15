using Billing.Domain.Entities;
using Billing.Domain.Exceptions;
using Billing.Domain.Repositories;

namespace Billing.Domain.Services;

/// <summary>
/// MS-BILL-WRITE-005 — append-only invoice adjustment service.
///
/// Single write surface: <see cref="CreateAsync"/>. The method
/// validates the request, computes the post-adjustment effective
/// balance, applies the over-credit guard for Credit adjustments,
/// and appends the adjustment row. The parent invoice is never
/// mutated by this flow (no totals change, no status change, no
/// line-item change). If the resulting balance is fully covered the
/// tenant-admin can explicitly trigger <c>POST /transition</c>
/// (MS-BILL-WRITE-004) to land the invoice on Paid via the canonical
/// status engine.
/// </summary>
public sealed class InvoiceAdjustmentService : IInvoiceAdjustmentService
{
    public const int ReasonMaxLength = 1000;
    public const int ReferenceNumberMaxLength = 64;
    public const int CreatedByMaxLength = 200;

    /// <summary>
    /// Decimal(18,2) precision cap. A request above this value is
    /// rejected at the service boundary so it never reaches the
    /// database (where MySQL would silently truncate or raise a
    /// driver error depending on configuration).
    /// </summary>
    public const decimal MaxAmount = 99_999_999.99m;

    /// <summary>
    /// Canonical type strings — see <see cref="InvoiceAdjustment.Type"/>
    /// summary. Both literals are PascalCase; input is normalised
    /// case-insensitively.
    /// </summary>
    public const string TypeCredit = "Credit";
    public const string TypeDebit = "Debit";

    private readonly IInvoiceAdjustmentRepository _repository;
    private readonly IInvoiceRepository _invoices;
    private readonly IPaymentRepository _payments;

    public InvoiceAdjustmentService(
        IInvoiceAdjustmentRepository repository,
        IInvoiceRepository invoices,
        IPaymentRepository payments)
    {
        _repository = repository;
        _invoices = invoices;
        _payments = payments;
    }

    public async Task<InvoiceAdjustmentResult?> CreateAsync(
        Guid tenantId,
        Guid invoiceId,
        string type,
        decimal amount,
        string reason,
        string? referenceNumber,
        string? createdBy,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (invoiceId == Guid.Empty) throw new ArgumentException("InvoiceId is required.", nameof(invoiceId));

        // ---- normalise + validate inputs -----------------------------
        var normalisedType = NormaliseType(type);
        if (normalisedType is null) throw new InvalidAdjustmentTypeException(type ?? string.Empty);

        if (amount <= 0m) throw new ArgumentException("Amount must be greater than zero.", nameof(amount));
        if (amount > MaxAmount)
            throw new ArgumentException(
                $"Amount must be ≤ {MaxAmount} (decimal(18,2) precision).", nameof(amount));
        // Round to 2dp away-from-zero, mirroring the CreateInvoice rounding rule.
        var roundedAmount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);

        var trimmedReason = (reason ?? string.Empty).Trim();
        if (trimmedReason.Length == 0)
            throw new ArgumentException("Reason is required.", nameof(reason));
        if (trimmedReason.Length > ReasonMaxLength)
            throw new ArgumentException(
                $"Reason must be ≤ {ReasonMaxLength} characters.", nameof(reason));

        var trimmedRef = referenceNumber?.Trim();
        if (!string.IsNullOrEmpty(trimmedRef) && trimmedRef.Length > ReferenceNumberMaxLength)
            throw new ArgumentException(
                $"ReferenceNumber must be ≤ {ReferenceNumberMaxLength} characters.", nameof(referenceNumber));

        var trimmedCreatedBy = createdBy?.Trim();
        if (!string.IsNullOrEmpty(trimmedCreatedBy) && trimmedCreatedBy.Length > CreatedByMaxLength)
            throw new ArgumentException(
                $"CreatedBy must be ≤ {CreatedByMaxLength} characters.", nameof(createdBy));

        // ---- tenant-scoped invoice fetch -----------------------------
        // Cross-tenant id surfaces as null → 404 at the API. The same
        // null shape covers "missing id" so a probe cannot distinguish
        // a foreign-tenant invoice from a non-existent one.
        var invoice = await _invoices.GetByIdForTenantAsync(tenantId, invoiceId, ct);
        if (invoice is null) return null;

        // ---- terminal-state guard ------------------------------------
        if (invoice.Status == InvoiceStatus.Voided
            || invoice.Status == InvoiceStatus.Refunded
            || invoice.Status == InvoiceStatus.PartiallyRefunded)
        {
            throw new InvoiceNotAdjustableException(invoice.Status);
        }

        // ---- recompute effective balance -----------------------------
        var (existingCredit, existingDebit) = await _repository.SumByInvoiceAsync(tenantId, invoiceId, ct);
        var paidSum = await _payments.SumRecordedPaymentsForInvoiceAsync(tenantId, invoiceId, ct);

        var newCredit = normalisedType == TypeCredit ? existingCredit + roundedAmount : existingCredit;
        var newDebit = normalisedType == TypeDebit ? existingDebit + roundedAmount : existingDebit;

        // Effective owed BEFORE this credit (for the over-credit
        // exception message — gives the operator the prior context).
        var effectiveOwedBefore = invoice.TotalAmount + existingDebit - existingCredit;

        // Post-adjustment effective totals.
        var effectiveTotal = invoice.TotalAmount + newDebit - newCredit;
        var effectiveOutstanding = effectiveTotal - paidSum;

        // ---- over-credit fail-closed guard ---------------------------
        if (normalisedType == TypeCredit && effectiveOutstanding < 0m)
        {
            throw new OverCreditException(
                effectiveOwed: effectiveOwedBefore,
                requestedCredit: roundedAmount,
                paidSum: paidSum);
        }

        // ---- append the row ------------------------------------------
        var now = DateTime.UtcNow;
        var adjustment = new InvoiceAdjustment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            InvoiceId = invoice.Id,
            CustomerId = invoice.CustomerId,
            Type = normalisedType,
            Amount = roundedAmount,
            Currency = invoice.Currency,
            Reason = trimmedReason,
            ReferenceNumber = string.IsNullOrEmpty(trimmedRef) ? null : trimmedRef,
            CreatedAt = now,
            CreatedBy = string.IsNullOrEmpty(trimmedCreatedBy) ? null : trimmedCreatedBy,
        };

        var saved = await _repository.AddAsync(adjustment, ct);

        return new InvoiceAdjustmentResult(
            Adjustment: saved,
            Invoice: invoice,
            PaidSum: paidSum,
            AdjustmentSumCredit: newCredit,
            AdjustmentSumDebit: newDebit,
            EffectiveTotal: effectiveTotal,
            EffectiveOutstanding: effectiveOutstanding);
    }

    /// <summary>
    /// Case-insensitively normalise a request-supplied type string to
    /// one of the two canonical PascalCase values. Returns null when
    /// the input is null/blank/unrecognised — the caller throws
    /// <see cref="InvalidAdjustmentTypeException"/> in that case.
    /// </summary>
    private static string? NormaliseType(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var trimmed = input.Trim();
        if (string.Equals(trimmed, TypeCredit, StringComparison.OrdinalIgnoreCase)) return TypeCredit;
        if (string.Equals(trimmed, TypeDebit, StringComparison.OrdinalIgnoreCase)) return TypeDebit;
        return null;
    }
}
