using CareConnect.Application.DTOs;
using CareConnect.Application.Repositories;
using CareConnect.Domain;
using CareConnect.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CareConnect.Infrastructure.Repositories;

public class NotificationRepository : INotificationRepository
{
    private readonly CareConnectDbContext _db;

    public NotificationRepository(CareConnectDbContext db)
    {
        _db = db;
    }

    public async Task<CareConnectNotification?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => await _db.CareConnectNotifications
            .Where(n => n.TenantId == tenantId && n.Id == id)
            .FirstOrDefaultAsync(ct);

    public async Task<(List<CareConnectNotification> Items, int TotalCount)> SearchAsync(
        Guid tenantId,
        GetNotificationsQuery query,
        CancellationToken ct = default)
    {
        var q = _db.CareConnectNotifications
            .Where(n => n.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(query.Status))
            q = q.Where(n => n.Status == query.Status);

        if (!string.IsNullOrWhiteSpace(query.NotificationType))
            q = q.Where(n => n.NotificationType == query.NotificationType);

        if (!string.IsNullOrWhiteSpace(query.RelatedEntityType))
            q = q.Where(n => n.RelatedEntityType == query.RelatedEntityType);

        if (query.RelatedEntityId.HasValue)
            q = q.Where(n => n.RelatedEntityId == query.RelatedEntityId.Value);

        if (query.ScheduledFrom.HasValue)
            q = q.Where(n => n.ScheduledForUtc >= query.ScheduledFrom.Value);

        if (query.ScheduledTo.HasValue)
            q = q.Where(n => n.ScheduledForUtc <= query.ScheduledTo.Value);

        var total = await q.CountAsync(ct);

        var items = await q
            .OrderByDescending(n => n.CreatedAtUtc)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task AddAsync(CareConnectNotification notification, CancellationToken ct = default)
    {
        await _db.CareConnectNotifications.AddAsync(notification, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task AddRangeAsync(IEnumerable<CareConnectNotification> notifications, CancellationToken ct = default)
    {
        await _db.CareConnectNotifications.AddRangeAsync(notifications, ct);
        await _db.SaveChangesAsync(ct);
    }
}
