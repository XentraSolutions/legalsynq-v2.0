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

    /// <summary>
    /// Find stale outbound SMS attempts for vendor reconciliation.
    /// Returns attempts where Channel=sms, ProviderMessageId is set,
    /// Status is in <paramref name="statuses"/>, and UpdatedAt is older than <paramref name="olderThan"/>.
    /// Results are ordered by UpdatedAt ascending (oldest first) and bounded by <paramref name="limit"/>.
    /// </summary>
    Task<List<NotificationAttempt>> GetStaleSmsAttemptsAsync(
        int limit,
        DateTime olderThan,
        IReadOnlyCollection<string> statuses,
        CancellationToken ct = default);
}
