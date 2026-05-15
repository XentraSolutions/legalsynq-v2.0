using Commerce.Contracts.Integration;

namespace Commerce.Application.Integration.Abstractions;

/// <summary>
/// Translates between host-platform tenant references
/// (<see cref="HostTenantRef"/>) and Commerce billing-account ids using
/// the local <c>BillingAccountExternalRef</c> mapping table seeded in
/// COM-B03.
///
/// Implementations must NOT call out to host services in COM-B08; the
/// mapping is resolved purely from Commerce-owned data.
/// </summary>
public interface IHostTenantResolver
{
    /// <summary>
    /// Resolve a host-platform tenant reference to a Commerce billing
    /// account id. Returns <c>null</c> when no mapping exists.
    /// </summary>
    Task<Guid?> ResolveBillingAccountIdAsync(
        string hostPlatformKey,
        string externalTenantId,
        CancellationToken ct);

    /// <summary>
    /// Resolve a Commerce billing account id back to a host-tenant
    /// reference (the primary external ref when one exists, otherwise
    /// the first registered ref). Returns <c>null</c> when no
    /// external-ref row has been registered.
    /// </summary>
    Task<HostTenantRef?> ResolveByBillingAccountAsync(
        Guid billingAccountId,
        CancellationToken ct);
}
