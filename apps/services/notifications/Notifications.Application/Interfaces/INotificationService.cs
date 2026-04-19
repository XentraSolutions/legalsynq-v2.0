using Notifications.Application.DTOs;

namespace Notifications.Application.Interfaces;

public interface INotificationService
{
    Task<NotificationResultDto> SubmitAsync(Guid tenantId, SubmitNotificationDto request);
    Task<NotificationDto?> GetByIdAsync(Guid tenantId, Guid id);
    Task<List<NotificationDto>> ListAsync(Guid tenantId, int limit = 50, int offset = 0);

    Task<PagedNotificationsResponse> ListPagedAsync(Guid tenantId, NotificationListQuery query);
    Task<NotificationStatsDto> GetStatsAsync(Guid tenantId, NotificationStatsQuery query);
    Task<List<NotificationEventDto>> GetEventsAsync(Guid tenantId, Guid id);
    Task<List<NotificationIssueDto>> GetIssuesAsync(Guid tenantId, Guid id);
    Task<RetryResultDto?> RetryAsync(Guid tenantId, Guid id);
    Task<ResendResultDto?> ResendAsync(Guid tenantId, Guid id);
}
