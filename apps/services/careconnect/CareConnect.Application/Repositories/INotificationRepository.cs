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

    // LSCC-005-01: referral-scoped notification queries
    Task<CareConnectNotification?> GetLatestByReferralAsync(
        Guid tenantId,
        Guid referralId,
        string? notificationType = null,
        CancellationToken ct = default);

    Task<List<CareConnectNotification>> GetAllByReferralAsync(
        Guid tenantId,
        Guid referralId,
        CancellationToken ct = default);
}
