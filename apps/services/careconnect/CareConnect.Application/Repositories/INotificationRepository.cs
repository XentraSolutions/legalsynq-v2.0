using CareConnect.Application.DTOs;
using CareConnect.Domain;

namespace CareConnect.Application.Repositories;

public interface INotificationRepository
{
    Task<CareConnectNotification?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<(List<CareConnectNotification> Items, int TotalCount)> SearchAsync(Guid tenantId, GetNotificationsQuery query, CancellationToken ct = default);
    Task AddAsync(CareConnectNotification notification, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<CareConnectNotification> notifications, CancellationToken ct = default);
    Task UpdateAsync(CareConnectNotification notification, CancellationToken ct = default);
}
