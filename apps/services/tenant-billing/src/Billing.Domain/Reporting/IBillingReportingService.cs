namespace Billing.Domain.Reporting;

/// <summary>
/// MS-BILL-WRITE-007 — single authoritative entry point for the
/// reporting / reconciliation surface. Exists so the controller and
/// any future consumer (ERP exporter, scheduled job) compose the
/// same projection logic — no duplicate accounting math, no
/// duplicate aging math.
///
/// Read-only by contract. All four methods are tenant-scoped via
/// the supplied <c>tenantId</c> (passed through from
/// <c>ITenantContext.TenantId</c> at the controller). Page/pageSize
/// are clamped at the controller (default 100, hard cap 1000) so the
/// service can trust the inputs.
/// </summary>
public interface IBillingReportingService
{
    Task<IReadOnlyList<AccountingSummaryRow>> GetAccountingSummaryAsync(
        Guid tenantId,
        Guid? customerId,
        string? status,
        DateTime? fromDate,
        DateTime? toDate,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<IReadOnlyList<InvoiceAgingRow>> GetInvoiceAgingAsync(
        Guid tenantId,
        Guid? customerId,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<IReadOnlyList<AdjustmentReportRow>> GetAdjustmentsAsync(
        Guid tenantId,
        Guid? customerId,
        string? type,
        DateTime? fromDate,
        DateTime? toDate,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<IReadOnlyList<PaymentReportRow>> GetPaymentsAsync(
        Guid tenantId,
        Guid? customerId,
        DateTime? fromDate,
        DateTime? toDate,
        int page,
        int pageSize,
        CancellationToken ct = default);
}
