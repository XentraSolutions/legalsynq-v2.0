using Microsoft.Extensions.Logging;
using Reports.Contracts.Adapters;
using Reports.Contracts.Context;

namespace Reports.Infrastructure.Adapters;

public sealed class MockAuditAdapter : IAuditAdapter
{
    private readonly ILogger<MockAuditAdapter> _log;

    public MockAuditAdapter(ILogger<MockAuditAdapter> log) => _log = log;

    public Task<AdapterResult<bool>> RecordEventAsync(RequestContext ctx, TenantContext tenant, string userId, string action, string description, CancellationToken ct)
    {
        _log.LogInformation("MockAuditAdapter: [{Action}] tenant={TenantId} user={UserId} — {Description} [Correlation={CorrelationId}]",
            action, tenant.TenantId, userId, description, ctx.CorrelationId);
        return Task.FromResult(AdapterResult<bool>.Ok(true));
    }
}
