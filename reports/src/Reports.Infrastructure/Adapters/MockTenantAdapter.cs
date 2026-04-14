using Microsoft.Extensions.Logging;
using Reports.Contracts.Adapters;

namespace Reports.Infrastructure.Adapters;

public sealed class MockTenantAdapter : ITenantAdapter
{
    private readonly ILogger<MockTenantAdapter> _log;

    public MockTenantAdapter(ILogger<MockTenantAdapter> log) => _log = log;

    public Task<string?> ResolveTenantIdAsync(string tenantCode, CancellationToken ct)
    {
        _log.LogDebug("MockTenantAdapter: ResolveTenantId called for {Code}", tenantCode);
        return Task.FromResult<string?>("mock-tenant-id");
    }

    public Task<bool> IsTenantActiveAsync(string tenantId, CancellationToken ct)
    {
        _log.LogDebug("MockTenantAdapter: IsTenantActive called for {TenantId}", tenantId);
        return Task.FromResult(true);
    }
}
