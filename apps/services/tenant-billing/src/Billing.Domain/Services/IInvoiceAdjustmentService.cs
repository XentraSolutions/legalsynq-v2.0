using Billing.Domain.Entities;

namespace Billing.Domain.Services;

/// <summary>
/// MS-BILL-WRITE-005 — append-only invoice adjustment / credit memo
/// service. The single write surface for the
/// <c>POST /api/invoices/{id}/adjustments</c> endpoint.
/// </summary>
public interface IInvoiceAdjustmentService
{
    /// <summary>
    /// Validate and append a new adjustment row to the invoice's
    /// append-only adjustment ledger. On success the parent invoice
    /// is NOT mutated (totals, line items, payments, status all
    /// preserved). The returned <see cref="InvoiceAdjustmentResult"/>
    /// carries the persisted adjustment plus the recomputed effective
    /// totals so the caller can render the post-adjustment balance
    /// without a second round-trip.
    ///
    /// Validation:
    ///   - tenantId / invoiceId non-empty
    ///   - type ∈ {Credit, Debit} (case-insensitive on input)
    ///   - amount &gt; 0 and ≤ 99,999,999.99 (decimal(18,2) precision)
    ///   - reason required, [1, 1000] chars after trim
    ///   - referenceNumber optional, [0, 64] chars after trim
    ///   - invoice exists for the tenant (else null → 404 at API)
    ///   - invoice not in a terminal/refund state (Voided, Refunded,
    ///     PartiallyRefunded) — else <see cref="InvoiceNotAdjustableException"/>
    ///   - over-credit guard for Credit adjustments — see below
    ///
    /// Over-credit guard (fail-closed): for Credit adjustments, the
    /// post-insert effective outstanding balance must remain ≥ 0.
    /// Concretely:
    /// <c>(invoice.TotalAmount + sumDebit) - (sumCredit + amount) - paidSum &lt; 0</c>
    /// → throws <see cref="OverCreditException"/> with NO insert.
    /// Debit adjustments do not have an equivalent cap.
    ///
    /// Tenant scoping: returns null when the invoice does not exist or
    /// belongs to a different tenant — same shape as
    /// <see cref="IInvoiceService.GetAsync"/> so a cross-tenant probe
    /// surfaces as a generic 404 with no existence leak.
    /// </summary>
    Task<InvoiceAdjustmentResult?> CreateAsync(
        Guid tenantId,
        Guid invoiceId,
        string type,
        decimal amount,
        string reason,
        string? referenceNumber,
        string? createdBy,
        CancellationToken ct = default);
}

/// <summary>
/// Return shape for <see cref="IInvoiceAdjustmentService.CreateAsync"/>.
/// Carries the persisted adjustment plus the recomputed effective
/// totals so the API response can render the post-adjustment balance
/// without a second tenant-scoped read.
/// </summary>
public sealed record InvoiceAdjustmentResult(
    InvoiceAdjustment Adjustment,
    Invoice Invoice,
    decimal PaidSum,
    decimal AdjustmentSumCredit,
    decimal AdjustmentSumDebit,
    decimal EffectiveTotal,
    decimal EffectiveOutstanding);
