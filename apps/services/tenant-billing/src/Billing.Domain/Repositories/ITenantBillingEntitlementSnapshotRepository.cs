using Billing.Domain.Entities;

namespace Billing.Domain.Repositories;

/// <summary>
/// TB-DATA-02 — repository surface for
/// <see cref="TenantBillingEntitlementSnapshot"/>. One current row per
/// profile; updated in place. All reads are tenant-scoped.
/// </summary>
public interface ITenantBillingEntitlementSnapshotRepository
{
    Task<TenantBillingEntitlementSnapshot> AddAsync(
        TenantBillingEntitlementSnapshot snapshot, CancellationToken ct = default);

    Task<TenantBillingEntitlementSnapshot> UpdateAsync(
        TenantBillingEntitlementSnapshot snapshot, CancellationToken ct = default);

    /// <summary>
    /// Tenant-scoped lookup by profile id. Returns null when no snapshot
    /// has ever been applied to the profile or the profile is not in the
    /// tenant.
    /// </summary>
    Task<TenantBillingEntitlementSnapshot?> GetByProfileIdAsync(
        Guid tenantId, Guid profileId, CancellationToken ct = default);
}
