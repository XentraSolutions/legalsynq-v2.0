using Commerce.Contracts.Integration;

namespace Commerce.Application.Integration.Abstractions;

/// <summary>
/// Builds <see cref="CommerceEntitlementSnapshot"/> projections from
/// Commerce-owned data. Output-only; no host calls; never enforces.
/// </summary>
public interface ICommerceEntitlementSnapshotService
{
    /// <summary>
    /// Snapshot for a known billing account. Returns <c>null</c> when
    /// the billing account does not exist.
    /// </summary>
    /// <param name="billingAccountId">Commerce billing account id.</param>
    /// <param name="includeAllSubscriptionStatuses">
    /// When false (default), only Active and Trialing subscriptions are
    /// included — i.e. those with current commercial entitlements.
    /// When true, all subscription statuses are included.
    /// </param>
    Task<CommerceEntitlementSnapshot?> GetByBillingAccountAsync(
        Guid billingAccountId,
        bool includeAllSubscriptionStatuses,
        CancellationToken ct);

    /// <summary>
    /// Snapshot for a host-tenant reference. Returns <c>null</c> when
    /// no <c>BillingAccountExternalRef</c> mapping exists.
    /// </summary>
    Task<CommerceEntitlementSnapshot?> GetByHostTenantAsync(
        string hostPlatformKey,
        string externalTenantId,
        bool includeAllSubscriptionStatuses,
        CancellationToken ct);
}
