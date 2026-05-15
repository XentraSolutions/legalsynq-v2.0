using Billing.Domain.Entities;
using Billing.Domain.Repositories;

namespace Billing.Domain.Tests.Fakes;

internal sealed class InMemoryTenantBillingProfileRepository : ITenantBillingProfileRepository
{
    private readonly Dictionary<Guid, TenantBillingProfile> _store = new();

    public Task<TenantBillingProfile> AddAsync(TenantBillingProfile profile, CancellationToken ct = default)
    {
        _store[profile.Id] = profile;
        return Task.FromResult(profile);
    }

    public Task<TenantBillingProfile> UpdateAsync(TenantBillingProfile profile, CancellationToken ct = default)
    {
        _store[profile.Id] = profile;
        return Task.FromResult(profile);
    }

    public Task<TenantBillingProfile?> GetByIdAsync(Guid tenantId, Guid profileId, CancellationToken ct = default)
        => Task.FromResult(_store.TryGetValue(profileId, out var p) && p.TenantId == tenantId ? p : null);

    public Task<TenantBillingProfile?> GetActiveByTenantAsync(Guid tenantId, CancellationToken ct = default)
        => Task.FromResult(_store.Values
            .FirstOrDefault(p => p.TenantId == tenantId && p.Status == TenantBillingProfileStatus.Active));

    public Task<TenantBillingProfile?> GetByBillingAccountAsync(
        Guid tenantId, Guid billingAccountId, CancellationToken ct = default)
        => Task.FromResult(_store.Values
            .Where(p => p.TenantId == tenantId
                        && p.BillingAccountId == billingAccountId
                        && p.Status != TenantBillingProfileStatus.Closed)
            .OrderByDescending(p => p.UpdatedAtUtc)
            .FirstOrDefault());

    public Task<TenantBillingProfile?> GetAnyByBillingAccountAsync(
        Guid tenantId, Guid billingAccountId, CancellationToken ct = default)
        => Task.FromResult(_store.Values
            .Where(p => p.TenantId == tenantId
                        && p.BillingAccountId == billingAccountId)
            .OrderByDescending(p => p.UpdatedAtUtc)
            .FirstOrDefault());

    public Task<bool> HasOpenProfileForTenantAsync(Guid tenantId, CancellationToken ct = default)
        => Task.FromResult(_store.Values.Any(p =>
            p.TenantId == tenantId && p.Status != TenantBillingProfileStatus.Closed));

    public Task<bool> IsBillingAccountClaimedAsync(Guid billingAccountId, CancellationToken ct = default)
        => Task.FromResult(_store.Values.Any(p =>
            p.BillingAccountId == billingAccountId && p.Status != TenantBillingProfileStatus.Closed));

    public Task<IReadOnlyList<TenantBillingProfile>> ListAsync(
        Guid tenantId, int page, int pageSize, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<TenantBillingProfile>>(_store.Values
            .Where(p => p.TenantId == tenantId)
            .OrderByDescending(p => p.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList());

    public Task<int> CountAsync(Guid tenantId, CancellationToken ct = default)
        => Task.FromResult(_store.Values.Count(p => p.TenantId == tenantId));
}
