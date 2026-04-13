using Microsoft.EntityFrameworkCore;
using Notifications.Application.Interfaces;
using Notifications.Domain;
using Notifications.Infrastructure.Data;

namespace Notifications.Infrastructure.Repositories;

public class NotificationRepository : INotificationRepository
{
    private readonly NotificationsDbContext _db;
    public NotificationRepository(NotificationsDbContext db) => _db = db;

    public async Task<Notification?> GetByIdAsync(Guid id)
        => await _db.Notifications.FindAsync(id);

    public async Task<Notification?> GetByIdAndTenantAsync(Guid id, Guid tenantId)
        => await _db.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.TenantId == tenantId);

    public async Task<Notification?> FindByIdempotencyKeyAsync(Guid tenantId, string idempotencyKey)
        => await _db.Notifications.FirstOrDefaultAsync(n => n.TenantId == tenantId && n.IdempotencyKey == idempotencyKey);

    public async Task<List<Notification>> GetByTenantAsync(Guid tenantId, int limit = 50, int offset = 0)
        => await _db.Notifications.Where(n => n.TenantId == tenantId)
            .OrderByDescending(n => n.CreatedAt).Skip(offset).Take(limit).ToListAsync();

    public async Task<Notification> CreateAsync(Notification notification)
    {
        notification.Id = notification.Id == Guid.Empty ? Guid.NewGuid() : notification.Id;
        notification.CreatedAt = DateTime.UtcNow;
        notification.UpdatedAt = DateTime.UtcNow;
        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync();
        return notification;
    }

    public async Task UpdateAsync(Notification notification)
    {
        notification.UpdatedAt = DateTime.UtcNow;
        _db.Notifications.Update(notification);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateStatusAsync(Guid id, string status, string? providerUsed = null, string? failureCategory = null, string? lastErrorMessage = null)
    {
        var n = await _db.Notifications.FindAsync(id);
        if (n == null) return;
        n.Status = status;
        if (providerUsed != null) n.ProviderUsed = providerUsed;
        if (failureCategory != null) n.FailureCategory = failureCategory;
        if (lastErrorMessage != null) n.LastErrorMessage = lastErrorMessage;
        n.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }
}
