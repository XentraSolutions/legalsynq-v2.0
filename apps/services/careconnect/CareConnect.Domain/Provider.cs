using BuildingBlocks.Domain;

namespace CareConnect.Domain;

/// <summary>
/// CC2-INT-B06-02 — Provider access-stage constants.
/// Controls how the provider interacts with the CareConnect platform.
/// </summary>
public static class ProviderAccessStage
{
    /// <summary>
    /// Default state. Provider exists in the shared registry and receives
    /// referrals only via signed token URLs. No portal access.
    /// </summary>
    public const string Url = "URL";

    /// <summary>
    /// Provider has activated via referral token, has an Identity user,
    /// and can log in to the shared Common Portal.
    /// </summary>
    public const string CommonPortal = "COMMON_PORTAL";

    /// <summary>
    /// Provider has been fully provisioned as a tenant within LegalSynq,
    /// uses the Tenant Portal instead of (or in addition to) the Common Portal.
    /// </summary>
    public const string Tenant = "TENANT";

    /// <summary>Ordered ordinal so stage comparisons can use >= semantics.</summary>
    public static int Ordinal(string stage) => stage switch
    {
        Url          => 0,
        CommonPortal => 1,
        Tenant       => 2,
        _            => -1,
    };

    public static bool IsAtLeast(string current, string minimum)
        => Ordinal(current) >= Ordinal(minimum);
}

public class Provider : AuditableEntity
{
    public Guid    Id                { get; private set; }
    public Guid    TenantId          { get; private set; }
    public string  Name              { get; private set; } = string.Empty;
    public string? OrganizationName  { get; private set; }
    public string  Email             { get; private set; } = string.Empty;
    public string  Phone             { get; private set; } = string.Empty;
    public string  AddressLine1      { get; private set; } = string.Empty;
    public string  City              { get; private set; } = string.Empty;
    public string  State             { get; private set; } = string.Empty;
    public string  PostalCode        { get; private set; } = string.Empty;
    public bool    IsActive          { get; private set; }
    public bool    AcceptingReferrals { get; private set; }

    public double?   Latitude        { get; private set; }
    public double?   Longitude       { get; private set; }
    public string?   GeoPointSource  { get; private set; }
    public DateTime? GeoUpdatedAtUtc { get; private set; }

    // Phase 5: link Provider to an Identity Organization (nullable during migration window)
    public Guid? OrganizationId { get; private set; }

    /// <summary>
    /// National Provider Identifier — globally unique across the shared provider registry.
    /// Used as the primary deduplication key when adding providers to networks.
    /// Null when unknown; set once and immutable via SetNpi().
    /// </summary>
    public string? Npi { get; private set; }

    // ── CC2-INT-B06-02: Access-stage lifecycle ────────────────────────────────

    /// <summary>
    /// Current access stage. Defaults to URL for all new and migrated providers.
    /// Transitions: URL → COMMON_PORTAL (activation) → TENANT (tenant onboarding).
    /// See <see cref="ProviderAccessStage"/> for valid values.
    /// </summary>
    public string  AccessStage              { get; private set; } = ProviderAccessStage.Url;

    /// <summary>
    /// Identity service user ID linked during COMMON_PORTAL activation.
    /// Null while the provider is in URL stage (no Identity account yet).
    /// </summary>
    public Guid?   IdentityUserId           { get; private set; }

    /// <summary>
    /// When the provider transitioned to COMMON_PORTAL (token activation).
    /// Null for URL-stage providers.
    /// </summary>
    public DateTime? CommonPortalActivatedAtUtc { get; private set; }

    /// <summary>
    /// When the provider transitioned to TENANT (tenant provisioning).
    /// Null for providers that have not joined a tenant yet.
    /// </summary>
    public DateTime? TenantProvisionedAtUtc { get; private set; }

    // ─────────────────────────────────────────────────────────────────────────

    public List<ProviderCategory> ProviderCategories { get; private set; } = new();

    /// <summary>
    /// Phase 5: link this provider record to the corresponding Identity Organization.
    /// Sets the soft FK OrganizationId so cross-service identity can be resolved.
    /// </summary>
    public void LinkOrganization(Guid organizationId)
    {
        OrganizationId = organizationId;
        UpdatedAtUtc   = DateTime.UtcNow;
    }

    // ── CC2-INT-B06-02: Stage transitions ─────────────────────────────────────

    /// <summary>
    /// Transition to COMMON_PORTAL stage.
    /// Called during auto-provisioning once the Identity user is created and
    /// the provider has confirmed their referral token activation.
    /// Idempotent — safe to call again if the provider is already at this stage.
    /// </summary>
    public void MarkCommonPortalActivated(Guid? identityUserId)
    {
        if (AccessStage == ProviderAccessStage.Tenant) return; // never downgrade

        AccessStage                = ProviderAccessStage.CommonPortal;
        IdentityUserId             = identityUserId ?? IdentityUserId;
        CommonPortalActivatedAtUtc ??= DateTime.UtcNow;
        UpdatedAtUtc               = DateTime.UtcNow;
    }

    /// <summary>
    /// Transition to TENANT stage.
    /// Called when the provider is fully onboarded as a tenant within LegalSynq.
    /// Updates TenantId to the provider's own tenant.
    /// </summary>
    public void MarkTenantProvisioned(Guid providerTenantId)
    {
        AccessStage              = ProviderAccessStage.Tenant;
        TenantId                 = providerTenantId;
        TenantProvisionedAtUtc   = DateTime.UtcNow;
        UpdatedAtUtc             = DateTime.UtcNow;
    }

    private Provider() { }

    public static Provider Create(
        Guid    tenantId,
        string  name,
        string? organizationName,
        string  email,
        string  phone,
        string  addressLine1,
        string  city,
        string  state,
        string  postalCode,
        bool    isActive,
        bool    acceptingReferrals,
        Guid?   createdByUserId,
        double? latitude       = null,
        double? longitude      = null,
        string? geoPointSource = null,
        string? npi            = null)
    {
        return new Provider
        {
            Id               = Guid.NewGuid(),
            TenantId         = tenantId,
            Name             = name.Trim(),
            OrganizationName = organizationName?.Trim(),
            Email            = email.Trim(),
            Phone            = phone.Trim(),
            AddressLine1     = addressLine1.Trim(),
            City             = city.Trim(),
            State            = state.Trim(),
            PostalCode       = postalCode.Trim(),
            IsActive         = isActive,
            AcceptingReferrals = acceptingReferrals,
            Npi              = string.IsNullOrWhiteSpace(npi) ? null : npi.Trim(),
            Latitude         = latitude,
            Longitude        = longitude,
            GeoPointSource   = latitude.HasValue ? (geoPointSource ?? "Manual") : null,
            GeoUpdatedAtUtc  = latitude.HasValue ? DateTime.UtcNow : null,

            // CC2-INT-B06-02: All new providers start in URL stage
            AccessStage      = ProviderAccessStage.Url,

            CreatedByUserId  = createdByUserId,
            UpdatedByUserId  = createdByUserId,
            CreatedAtUtc     = DateTime.UtcNow,
            UpdatedAtUtc     = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Set the NPI for an existing provider that didn't have one at creation.
    /// NPI is globally unique — caller must check for conflicts first.
    /// </summary>
    public void SetNpi(string npi)
    {
        Npi          = npi.Trim();
        UpdatedAtUtc = DateTime.UtcNow;
    }

    // LSCC-01-003: Admin-safe idempotent activation — sets IsActive + AcceptingReferrals = true.
    public void Activate()
    {
        IsActive           = true;
        AcceptingReferrals = true;
        UpdatedAtUtc       = DateTime.UtcNow;
    }

    public void Update(
        string  name,
        string? organizationName,
        string  email,
        string  phone,
        string  addressLine1,
        string  city,
        string  state,
        string  postalCode,
        bool    isActive,
        bool    acceptingReferrals,
        Guid?   updatedByUserId,
        double? latitude       = null,
        double? longitude      = null,
        string? geoPointSource = null)
    {
        Name             = name.Trim();
        OrganizationName = organizationName?.Trim();
        Email            = email.Trim();
        Phone            = phone.Trim();
        AddressLine1     = addressLine1.Trim();
        City             = city.Trim();
        State            = state.Trim();
        PostalCode       = postalCode.Trim();
        IsActive         = isActive;
        AcceptingReferrals = acceptingReferrals;
        UpdatedByUserId  = updatedByUserId;
        UpdatedAtUtc     = DateTime.UtcNow;

        Latitude        = latitude;
        Longitude       = longitude;
        GeoPointSource  = latitude.HasValue ? (geoPointSource ?? "Manual") : null;
        GeoUpdatedAtUtc = latitude.HasValue ? DateTime.UtcNow : null;
    }
}
