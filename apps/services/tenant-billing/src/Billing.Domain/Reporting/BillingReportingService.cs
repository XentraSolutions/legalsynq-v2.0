namespace Billing.Domain.Reporting;

/// <summary>
/// MS-BILL-WRITE-007 — concrete reporting service. Today the
/// implementation is a pure pass-through to
/// <see cref="IBillingReportingRepository"/> — the heavy lifting
/// (tenant scoping, decimal aggregation, aging-bucket assignment,
/// non-voided-payment filter) lives in the repository so EF can
/// translate the math into single SQL queries. The service exists
/// as a stable seam for future cross-row composition (e.g. running
/// totals, multi-currency breakdowns, ERP packaging) without
/// rewriting consumer call sites.
///
/// Tenant scoping and input clamping are the controller's
/// responsibility; the service trusts its inputs and applies a
/// defensive <c>Guid.Empty</c> guard only.
/// </summary>
public sealed class BillingReportingService : IBillingReportingService
{
    private readonly IBillingReportingRepository _repo;
    private readonly TimeProvider _time;

    public BillingReportingService(
        IBillingReportingRepository repo,
        TimeProvider time)
    {
        _repo = repo;
        _time = time;
    }

    public Task<IReadOnlyList<AccountingSummaryRow>> GetAccountingSummaryAsync(
        Guid tenantId,
        Guid? customerId,
        string? status,
        DateTime? fromDate,
        DateTime? toDate,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required.", nameof(tenantId));
        return _repo.ListAccountingSummaryAsync(
            tenantId, customerId, status, fromDate, toDate, page, pageSize, ct);
    }

    public Task<IReadOnlyList<InvoiceAgingRow>> GetInvoiceAgingAsync(
        Guid tenantId,
        Guid? customerId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required.", nameof(tenantId));
        return _repo.ListInvoiceAgingAsync(
            tenantId, customerId, _time.GetUtcNow().UtcDateTime, page, pageSize, ct);
    }

    public Task<IReadOnlyList<AdjustmentReportRow>> GetAdjustmentsAsync(
        Guid tenantId,
        Guid? customerId,
        string? type,
        DateTime? fromDate,
        DateTime? toDate,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required.", nameof(tenantId));
        return _repo.ListAdjustmentsAsync(
            tenantId, customerId, type, fromDate, toDate, page, pageSize, ct);
    }

    public Task<IReadOnlyList<PaymentReportRow>> GetPaymentsAsync(
        Guid tenantId,
        Guid? customerId,
        DateTime? fromDate,
        DateTime? toDate,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required.", nameof(tenantId));
        return _repo.ListPaymentsAsync(
            tenantId, customerId, fromDate, toDate, page, pageSize, ct);
    }
}
