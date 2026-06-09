using Billing.Domain.Entities;

namespace Billing.Domain.Repositories;

/// <summary>
/// TB-DATA-01 — repository surface for <see cref="TenantBillingProfile"/>.
/// All reads are tenant-scoped: a caller cannot accidentally fetch a profile
/// that belongs to a different tenant. Writes are append/replace per profile;
/// the lifecycle invariants live on the entity, not in SQL.
/// </summary>
public interface ITenantBillingProfileRepository
{
    Task<TenantBillingProfile> AddAsync(TenantBillingProfile profile, CancellationToken ct = default);
    Task<TenantBillingProfile> UpdateAsync(TenantBillingProfile profile, CancellationToken ct = default);

    Task<TenantBillingProfile?> GetByIdAsync(Guid tenantId, Guid profileId, CancellationToken ct = default);

    /// <summary>
    /// Returns the single Active profile for the tenant, or null if none.
    /// </summary>
    Task<TenantBillingProfile?> GetActiveByTenantAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// Tenant-scoped exact match by BillingAccountId. Returns the most-recent
    /// non-Closed profile pointing at <paramref name="billingAccountId"/> for
    /// the tenant, or null if none.
    /// </summary>
    Task<TenantBillingProfile?> GetByBillingAccountAsync(
        Guid tenantId,
        Guid billingAccountId,
        CancellationToken ct = default);

    /// <summary>
    /// TB-DATA-02 — same as <see cref="GetByBillingAccountAsync"/> but DOES
    /// NOT filter out Closed rows. The entitlement bridge needs to detect
    /// "profile exists for this account but is Closed" and surface 409
    /// (instead of the generic 404 the open-only lookup would produce).
    /// Returns the most-recent matching row across all statuses.
    /// </summary>
    Task<TenantBillingProfile?> GetAnyByBillingAccountAsync(
        Guid tenantId,
        Guid billingAccountId,
        CancellationToken ct = default);

    /// <summary>
    /// True when an open (non-Closed) profile already exists for the tenant.
    /// Used by the service layer before allowing a new profile to be created.
    /// </summary>
    Task<bool> HasOpenProfileForTenantAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// True when an open (non-Closed) profile already points at the given
    /// <paramref name="billingAccountId"/>. Cross-tenant: a single Commerce
    /// account cannot be claimed by two tenants simultaneously.
    /// </summary>
    Task<bool> IsBillingAccountClaimedAsync(Guid billingAccountId, CancellationToken ct = default);

    Task<IReadOnlyList<TenantBillingProfile>> ListAsync(
        Guid tenantId,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<int> CountAsync(Guid tenantId, CancellationToken ct = default);
}
