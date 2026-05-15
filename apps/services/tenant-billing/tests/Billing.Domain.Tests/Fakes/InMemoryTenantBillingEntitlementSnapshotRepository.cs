using Billing.Domain.Entities;
using Billing.Domain.Repositories;

namespace Billing.Domain.Tests.Fakes;

internal sealed class InMemoryTenantBillingEntitlementSnapshotRepository
    : ITenantBillingEntitlementSnapshotRepository
{
    private readonly Dictionary<Guid, TenantBillingEntitlementSnapshot> _byId = new();

    public Task<TenantBillingEntitlementSnapshot> AddAsync(
        TenantBillingEntitlementSnapshot snapshot, CancellationToken ct = default)
    {
        if (_byId.Values.Any(s => s.TenantBillingProfileId == snapshot.TenantBillingProfileId))
            throw new InvalidOperationException(
                "Duplicate entitlement snapshot for profile (the unique invariant from " +
                "the relational unique index — service layer should have called Update).");
        _byId[snapshot.Id] = snapshot;
        return Task.FromResult(snapshot);
    }

    public Task<TenantBillingEntitlementSnapshot> UpdateAsync(
        TenantBillingEntitlementSnapshot snapshot, CancellationToken ct = default)
    {
        _byId[snapshot.Id] = snapshot;
        return Task.FromResult(snapshot);
    }

    public Task<TenantBillingEntitlementSnapshot?> GetByProfileIdAsync(
        Guid tenantId, Guid profileId, CancellationToken ct = default)
        => Task.FromResult(_byId.Values
            .FirstOrDefault(s => s.TenantId == tenantId
                                 && s.TenantBillingProfileId == profileId));
}
