namespace Billing.Api.LegalSynq;

/// <summary>
/// LS-INT-01 — LegalSynq platform role constants as emitted by
/// <c>Identity.Infrastructure.Services.JwtTokenService</c> into the
/// <c>"role"</c> JWT claim.
///
/// Used in <see cref="LegalSynqJwtTenantContextResolver"/> to identify
/// internal-service callers and in future RBAC guards.
///
/// Principle: identity determines WHO; entitlement determines WHAT.
/// Never move entitlement enforcement into this class.
/// </summary>
public static class LegalSynqBillingRoles
{
    public const string PlatformAdmin = "PlatformAdmin";
    public const string TenantAdmin = "TenantAdmin";
    public const string BillingManager = "BillingManager";
    public const string BillingReadOnly = "BillingReadOnly";
    public const string SupportAgent = "SupportAgent";

    /// <summary>
    /// Service-to-service identity. Internal callers (e.g. Commerce → Tenant Billing
    /// entitlement publisher) present this role with a JWT scoped to a specific tenant.
    /// When present, <see cref="LegalSynqTenantContextOptions.AllowInternalTokenFallback"/>
    /// allows the <c>X-Tenant-Id</c> header as tenant source.
    /// </summary>
    public const string InternalService = "InternalService";

    public static bool HasBillingWrite(IEnumerable<string> roles)
        => roles.Any(r => r is PlatformAdmin or TenantAdmin or BillingManager or InternalService);

    public static bool HasBillingRead(IEnumerable<string> roles)
        => roles.Any(r => r is PlatformAdmin or TenantAdmin or BillingManager
                              or BillingReadOnly or SupportAgent or InternalService);
}
