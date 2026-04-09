using Notifications.Domain;

namespace Notifications.Application.Interfaces;

public interface INotificationAttemptRepository
{
    Task<NotificationAttempt?> GetByIdAsync(Guid id);
    Task<NotificationAttempt?> FindByProviderMessageIdAsync(string providerMessageId);
    Task<List<NotificationAttempt>> GetByNotificationIdAsync(Guid notificationId);
    Task<NotificationAttempt> CreateAsync(NotificationAttempt attempt);
    Task UpdateAsync(NotificationAttempt attempt);
    Task UpdateStatusAsync(Guid id, string status, DateTime? completedAt = null);
}
