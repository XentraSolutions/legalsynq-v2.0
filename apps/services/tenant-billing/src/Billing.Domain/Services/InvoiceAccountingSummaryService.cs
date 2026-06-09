using Billing.Domain.Projections;
using Billing.Domain.Repositories;

namespace Billing.Domain.Services;

/// <summary>
/// MS-BILL-WRITE-006 — concrete implementation of
/// <see cref="IInvoiceAccountingSummaryService"/>. Pure read-side
/// projection composed from three existing tenant-scoped repository
/// methods:
///
/// <list type="bullet">
///   <item><see cref="IInvoiceRepository.GetByIdForTenantAsync"/> —
///         immutable invoice total + currency.</item>
///   <item><see cref="IInvoiceAdjustmentRepository.SumByInvoiceAsync"/>
///         — credit / debit sums over the append-only ledger.</item>
///   <item><see cref="IPaymentRepository.SumRecordedPaymentsForInvoiceAsync"/>
///         — non-voided payment sum (the repo predicate already
///         filters <c>Status != "Voided"</c>).</item>
/// </list>
///
/// No new repository surface is introduced. The service performs
/// the same decimal arithmetic as the WRITE-005 over-credit guard
/// so the read-path and write-path agree by construction.
/// </summary>
public sealed class InvoiceAccountingSummaryService : IInvoiceAccountingSummaryService
{
    private readonly IInvoiceRepository _invoices;
    private readonly IInvoiceAdjustmentRepository _adjustments;
    private readonly IPaymentRepository _payments;

    public InvoiceAccountingSummaryService(
        IInvoiceRepository invoices,
        IInvoiceAdjustmentRepository adjustments,
        IPaymentRepository payments)
    {
        _invoices = invoices;
        _adjustments = adjustments;
        _payments = payments;
    }

    public async Task<InvoiceAccountingSummary?> GetAsync(
        Guid tenantId, Guid invoiceId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (invoiceId == Guid.Empty)
            throw new ArgumentException("InvoiceId is required.", nameof(invoiceId));

        // Tenant-scoped invoice fetch. Cross-tenant or unknown id
        // surfaces as null → 404 at the API. Same shape as
        // IInvoiceService.GetAsync so a probe cannot distinguish a
        // foreign-tenant invoice from a non-existent one.
        var invoice = await _invoices.GetByIdForTenantAsync(tenantId, invoiceId, ct);
        if (invoice is null) return null;

        // Two reads in parallel-friendly order; we await sequentially
        // because the EF DbContext is not safe for concurrent commands
        // on a single scope. The cost is negligible (each is a single
        // indexed scalar aggregate).
        var (creditSum, debitSum) = await _adjustments.SumByInvoiceAsync(tenantId, invoiceId, ct);
        var paidSum = await _payments.SumRecordedPaymentsForInvoiceAsync(tenantId, invoiceId, ct);

        var effectiveTotal = invoice.TotalAmount + debitSum - creditSum;
        var effectiveOutstanding = effectiveTotal - paidSum;

        return new InvoiceAccountingSummary(
            InvoiceId: invoice.Id,
            Currency: invoice.Currency,
            InvoiceTotal: invoice.TotalAmount,
            PaidSum: paidSum,
            AdjustmentCreditSum: creditSum,
            AdjustmentDebitSum: debitSum,
            EffectiveTotal: effectiveTotal,
            EffectiveOutstanding: effectiveOutstanding);
    }
}
