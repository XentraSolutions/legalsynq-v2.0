using Microsoft.Extensions.Logging;
using Reports.Contracts.Adapters;

namespace Reports.Infrastructure.Adapters;

public sealed class MockAuditAdapter : IAuditAdapter
{
    private readonly ILogger<MockAuditAdapter> _log;

    public MockAuditAdapter(ILogger<MockAuditAdapter> log) => _log = log;

    public Task RecordEventAsync(string tenantId, string userId, string action, string description, CancellationToken ct)
    {
        _log.LogInformation("MockAuditAdapter: [{Action}] tenant={TenantId} user={UserId} — {Description}",
            action, tenantId, userId, description);
        return Task.CompletedTask;
    }
}
