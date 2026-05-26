namespace CareConnect.Application.Interfaces;

/// <summary>
/// Process-lifetime cache for tenant subdomain slugs.
///
/// Registered as Singleton so the cache survives across DI scopes and request lifetimes.
/// Subdomain slugs never change after tenant provisioning, so no TTL or eviction is needed.
///
/// Separating this from ReferralEmailService (Scoped) avoids the anti-pattern of a
/// static mutable field on a scoped class — the lifetime is now explicit in DI.
/// </summary>
public interface ITenantSubdomainCache
{
    /// <summary>Returns true and sets <paramref name="subdomain"/> if a cached slug exists for the tenant.</summary>
    bool TryGetValue(Guid tenantId, out string? subdomain);

    /// <summary>Adds a subdomain slug to the cache. No-op if the tenantId is already present.</summary>
    void TryAdd(Guid tenantId, string subdomain);
}
