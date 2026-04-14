using Microsoft.Extensions.Logging;
using Reports.Contracts.Adapters;

namespace Reports.Infrastructure.Adapters;

public sealed class MockNotificationAdapter : INotificationAdapter
{
    private readonly ILogger<MockNotificationAdapter> _log;

    public MockNotificationAdapter(ILogger<MockNotificationAdapter> log) => _log = log;

    public Task NotifyReportReadyAsync(string tenantId, string userId, string reportId, string reportName, CancellationToken ct)
    {
        _log.LogInformation("MockNotificationAdapter: Report ready — {ReportName} ({ReportId})", reportName, reportId);
        return Task.CompletedTask;
    }

    public Task NotifyReportFailedAsync(string tenantId, string userId, string reportId, string reason, CancellationToken ct)
    {
        _log.LogWarning("MockNotificationAdapter: Report failed — {ReportId}: {Reason}", reportId, reason);
        return Task.CompletedTask;
    }
}
