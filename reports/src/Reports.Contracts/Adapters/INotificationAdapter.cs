namespace Reports.Contracts.Adapters;

public interface INotificationAdapter
{
    Task NotifyReportReadyAsync(string tenantId, string userId, string reportId, string reportName, CancellationToken ct = default);
    Task NotifyReportFailedAsync(string tenantId, string userId, string reportId, string reason, CancellationToken ct = default);
}
