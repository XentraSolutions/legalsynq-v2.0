namespace Billing.Domain.Reporting;

/// <summary>
/// MS-BILL-WRITE-007 — read-only reporting repository. Hosts the four
/// EF queries that back the reporting endpoints. Every method is
/// tenant-scoped at the call site and returns immutable record rows
/// so a consumer cannot mutate the projection after the fact.
///
/// All queries are bounded by page/pageSize at the repository layer
/// (cap enforced by the controller); there is no "give me everything"
/// surface — exporting more than the page cap requires the caller to
/// page through. Voided payments are excluded everywhere by the
/// SQL predicate.
/// </summary>
public interface IBillingReportingRepository
{
    Task<IReadOnlyList<AccountingSummaryRow>> ListAccountingSummaryAsync(
        Guid tenantId,
        Guid? customerId,
        string? status,
        DateTime? fromDate,
        DateTime? toDate,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<IReadOnlyList<InvoiceAgingRow>> ListInvoiceAgingAsync(
        Guid tenantId,
        Guid? customerId,
        DateTime nowUtc,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<IReadOnlyList<AdjustmentReportRow>> ListAdjustmentsAsync(
        Guid tenantId,
        Guid? customerId,
        string? type,
        DateTime? fromDate,
        DateTime? toDate,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<IReadOnlyList<PaymentReportRow>> ListPaymentsAsync(
        Guid tenantId,
        Guid? customerId,
        DateTime? fromDate,
        DateTime? toDate,
        int page,
        int pageSize,
        CancellationToken ct = default);
}
