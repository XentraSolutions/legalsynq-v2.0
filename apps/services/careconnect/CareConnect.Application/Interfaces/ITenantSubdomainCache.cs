namespace CareConnect.Application.Interfaces;

/// <summary>
/// Process-lifetime cache for tenant platform host details.
///
/// Registered as Singleton so the cache survives across DI scopes and request lifetimes.
/// Platform host details never change after tenant provisioning, so no TTL or eviction is needed.
///
/// Separating this from ReferralEmailService (Scoped) avoids the anti-pattern of a
/// static mutable field on a scoped class — the lifetime is now explicit in DI.
/// </summary>
public interface ITenantSubdomainCache
{
    /// <summary>Returns true and sets <paramref name="host"/> if cached host details exist for the tenant.</summary>
    bool TryGetValue(Guid tenantId, out TenantHostResult? host);

    /// <summary>Adds tenant host details to the cache. No-op if the tenantId is already present.</summary>
    void TryAdd(Guid tenantId, TenantHostResult host);
}
