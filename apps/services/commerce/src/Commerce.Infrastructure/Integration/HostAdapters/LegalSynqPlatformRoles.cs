namespace Commerce.Infrastructure.Integration.HostAdapters;

/// <summary>
/// LS-INT-01 — LegalSynq platform role claim values as emitted by
/// <c>Identity.Infrastructure.Services.JwtTokenService</c> into the
/// <c>"role"</c> JWT claim.
///
/// Role → access mapping:
/// <list type="bullet">
///   <item><see cref="PlatformAdmin"/> — full access to all billing accounts and catalog.</item>
///   <item><see cref="TenantAdmin"/> — access to own billing account and subscriptions.</item>
///   <item><see cref="BillingManager"/> — billing + subscription reads and writes.</item>
///   <item><see cref="BillingReadOnly"/> — read-only across billing, catalog, subscriptions.</item>
///   <item><see cref="SupportAgent"/> — account standing + audit log reads.</item>
///   <item><see cref="InternalService"/> — integration endpoints (entitlement publish, diagnostics).</item>
/// </list>
///
/// Principle: identity determines WHO; entitlement determines WHAT.
/// These constants are used only for identity-layer claims inspection —
/// never for entitlement enforcement.
/// </summary>
public static class LegalSynqPlatformRoles
{
    public const string PlatformAdmin = "PlatformAdmin";
    public const string TenantAdmin = "TenantAdmin";
    public const string BillingManager = "BillingManager";
    public const string BillingReadOnly = "BillingReadOnly";
    public const string SupportAgent = "SupportAgent";
    public const string InternalService = "InternalService";

    /// <summary>Returns true when any role in <paramref name="roles"/> is a billing-write role.</summary>
    public static bool HasBillingWrite(IEnumerable<string> roles)
        => roles.Any(r => r is PlatformAdmin or TenantAdmin or BillingManager or InternalService);

    /// <summary>Returns true when any role in <paramref name="roles"/> is a billing-read role.</summary>
    public static bool HasBillingRead(IEnumerable<string> roles)
        => roles.Any(r => r is PlatformAdmin or TenantAdmin or BillingManager
                              or BillingReadOnly or SupportAgent or InternalService);
}
