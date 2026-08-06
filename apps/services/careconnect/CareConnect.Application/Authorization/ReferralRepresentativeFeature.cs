namespace CareConnect.Application.Authorization;

/// <summary>
/// Tenant-scoped feature flag key for the representative portal, stored via the platform's
/// existing tenant-capability system (Tenant.Domain.TenantCapability) rather than a
/// bespoke flag mechanism. Disabled unless a tenant administrator explicitly enables it.
/// </summary>
public static class ReferralRepresentativeFeature
{
    public const string PortalEnabledCapabilityKey = "careconnect.referral_representative_portal";
}
