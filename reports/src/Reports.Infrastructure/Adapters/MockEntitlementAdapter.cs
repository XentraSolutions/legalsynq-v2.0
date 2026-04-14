using Microsoft.Extensions.Logging;
using Reports.Contracts.Adapters;

namespace Reports.Infrastructure.Adapters;

public sealed class MockEntitlementAdapter : IEntitlementAdapter
{
    private readonly ILogger<MockEntitlementAdapter> _log;

    public MockEntitlementAdapter(ILogger<MockEntitlementAdapter> log) => _log = log;

    public Task<bool> CanAccessReportsAsync(string tenantId, string userId, CancellationToken ct)
    {
        _log.LogDebug("MockEntitlementAdapter: CanAccessReports for {TenantId}/{UserId}", tenantId, userId);
        return Task.FromResult(true);
    }

    public Task<bool> CanExecuteReportAsync(string tenantId, string userId, string reportTypeCode, CancellationToken ct)
    {
        _log.LogDebug("MockEntitlementAdapter: CanExecuteReport for {ReportType}", reportTypeCode);
        return Task.FromResult(true);
    }
}
