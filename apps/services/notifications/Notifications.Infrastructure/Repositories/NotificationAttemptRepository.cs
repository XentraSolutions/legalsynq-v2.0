using Microsoft.EntityFrameworkCore;
using Notifications.Application.Interfaces;
using Notifications.Domain;
using Notifications.Infrastructure.Data;

namespace Notifications.Infrastructure.Repositories;

public class NotificationAttemptRepository : INotificationAttemptRepository
{
    private readonly NotificationsDbContext _db;
    public NotificationAttemptRepository(NotificationsDbContext db) => _db = db;

    public async Task<NotificationAttempt?> GetByIdAsync(Guid id)
        => await _db.NotificationAttempts.FindAsync(id);

    public async Task<NotificationAttempt?> FindByProviderMessageIdAsync(string providerMessageId)
        => await _db.NotificationAttempts.FirstOrDefaultAsync(a => a.ProviderMessageId == providerMessageId);

    public async Task<List<NotificationAttempt>> GetByNotificationIdAsync(Guid notificationId)
        => await _db.NotificationAttempts.Where(a => a.NotificationId == notificationId)
            .OrderBy(a => a.AttemptNumber).ToListAsync();

    public async Task<NotificationAttempt> CreateAsync(NotificationAttempt attempt)
    {
        attempt.Id = attempt.Id == Guid.Empty ? Guid.NewGuid() : attempt.Id;
        attempt.CreatedAt = DateTime.UtcNow;
        attempt.UpdatedAt = DateTime.UtcNow;
        _db.NotificationAttempts.Add(attempt);
        await _db.SaveChangesAsync();
        return attempt;
    }

    public async Task UpdateAsync(NotificationAttempt attempt)
    {
        attempt.UpdatedAt = DateTime.UtcNow;
        _db.NotificationAttempts.Update(attempt);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateStatusAsync(Guid id, string status, DateTime? completedAt = null)
    {
        var a = await _db.NotificationAttempts.FindAsync(id);
        if (a == null) return;
        a.Status = status;
        if (completedAt.HasValue) a.CompletedAt = completedAt;
        a.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }
}
