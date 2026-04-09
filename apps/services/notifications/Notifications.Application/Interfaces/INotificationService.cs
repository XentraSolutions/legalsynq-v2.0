using Notifications.Application.DTOs;

namespace Notifications.Application.Interfaces;

public interface INotificationService
{
    Task<NotificationResultDto> SubmitAsync(Guid tenantId, SubmitNotificationDto request);
    Task<NotificationDto?> GetByIdAsync(Guid tenantId, Guid id);
    Task<List<NotificationDto>> ListAsync(Guid tenantId, int limit = 50, int offset = 0);
}
