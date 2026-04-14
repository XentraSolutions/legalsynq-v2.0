namespace Reports.Contracts.Adapters;

public interface IEntitlementAdapter
{
    Task<bool> CanAccessReportsAsync(string tenantId, string userId, CancellationToken ct = default);
    Task<bool> CanExecuteReportAsync(string tenantId, string userId, string reportTypeCode, CancellationToken ct = default);
}
