namespace Tenant.Domain;

/// <summary>
/// Lifecycle status of a tenant record.
///
/// Migration mapping from Identity.Domain.Tenant:
///   Identity.IsActive == true  AND ProvisioningStatus == Active  → Active
///   Identity.IsActive == false                                    → Inactive
///   Identity.ProvisioningStatus == Pending/InProgress/Verifying   → Pending
/// </summary>
public enum TenantStatus
{
    /// <summary>Tenant is being provisioned / onboarding in progress.</summary>
    Pending,

    /// <summary>Tenant is live and operational.</summary>
    Active,

    /// <summary>Tenant is deactivated (soft delete).</summary>
    Inactive,

    /// <summary>Temporarily suspended (billing / compliance hold).</summary>
    Suspended
}

/// <summary>
/// Core Tenant entity — canonical company / organisation master record.
///
/// Identity field mapping (Block 1):
///   Identity.Id                   → Id               (preserved cross-service FK)
///   Identity.Name                 → DisplayName
///   Identity.Code                 → Code
///   Identity.IsActive             → Status
///   Identity.Subdomain            → Subdomain
///   Identity.LogoDocumentId       → LogoDocumentId
///   Identity.LogoWhiteDocumentId  → LogoWhiteDocumentId
///
/// Block 2 additions: profile / contact / address metadata.
/// Deferred: product entitlements, domains, settings, migration utility.
/// </summary>
public class Tenant
{
    // ── Identity ─────────────────────────────────────────────────────────────

    public Guid Id { get; private set; }

    /// <summary>Short URL-safe slug. Unique across all tenants.</summary>
    public string Code { get; private set; } = string.Empty;

    /// <summary>Human-readable display name. Maps from Identity.Name.</summary>
    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>Optional formal legal entity name.</summary>
    public string? LegalName { get; private set; }

    /// <summary>Company / organisation description.</summary>
    public string? Description { get; private set; }

    /// <summary>Active / Inactive / Pending lifecycle status.</summary>
    public TenantStatus Status { get; private set; }

    /// <summary>Assigned subdomain slug (unique).</summary>
    public string? Subdomain { get; private set; }

    // ── Brand / logo references (kept for Identity backward-compat) ───────────

    /// <summary>Logo document ref in Documents service. Not a blob.</summary>
    public Guid? LogoDocumentId { get; private set; }

    /// <summary>White-variant logo document ref.</summary>
    public Guid? LogoWhiteDocumentId { get; private set; }

    // ── Profile metadata ──────────────────────────────────────────────────────

    /// <summary>Canonical company website URL.</summary>
    public string? WebsiteUrl { get; private set; }

    /// <summary>IANA timezone (e.g. "America/New_York").</summary>
    public string? TimeZone { get; private set; }

    /// <summary>IETF BCP 47 locale tag (e.g. "en-US").</summary>
    public string? Locale { get; private set; }

    // ── Contact metadata ──────────────────────────────────────────────────────

    /// <summary>Public support email address.</summary>
    public string? SupportEmail { get; private set; }

    /// <summary>Public support phone number.</summary>
    public string? SupportPhone { get; private set; }

    // ── Address ───────────────────────────────────────────────────────────────

    public string? AddressLine1    { get; private set; }
    public string? AddressLine2    { get; private set; }
    public string? City            { get; private set; }
    public string? StateOrProvince { get; private set; }
    public string? PostalCode      { get; private set; }

    /// <summary>ISO 3166-1 alpha-2 country code (e.g. "US").</summary>
    public string? CountryCode { get; private set; }

    // ── Timestamps ────────────────────────────────────────────────────────────

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    // ── Navigation ────────────────────────────────────────────────────────────

    /// <summary>Optional one-to-one branding record (lazy-loaded by repo).</summary>
    public TenantBranding? Branding { get; private set; }

    private Tenant() { }

    // ── Factory ───────────────────────────────────────────────────────────────

    public static Tenant Create(
        string  code,
        string  displayName,
        string? legalName       = null,
        string? subdomain       = null,
        string? description     = null,
        string? websiteUrl      = null,
        string? timeZone        = null,
        string? locale          = null,
        string? supportEmail    = null,
        string? supportPhone    = null,
        string? addressLine1    = null,
        string? addressLine2    = null,
        string? city            = null,
        string? stateOrProvince = null,
        string? postalCode      = null,
        string? countryCode     = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        var now = DateTime.UtcNow;
        return new Tenant
        {
            Id              = Guid.NewGuid(),
            Code            = code.Trim().ToLowerInvariant(),
            DisplayName     = displayName.Trim(),
            LegalName       = legalName?.Trim(),
            Description     = description?.Trim(),
            Status          = TenantStatus.Active,
            Subdomain       = subdomain?.Trim().ToLowerInvariant(),
            WebsiteUrl      = websiteUrl?.Trim(),
            TimeZone        = timeZone,
            Locale          = locale,
            SupportEmail    = supportEmail?.Trim().ToLowerInvariant(),
            SupportPhone    = supportPhone?.Trim(),
            AddressLine1    = addressLine1?.Trim(),
            AddressLine2    = addressLine2?.Trim(),
            City            = city?.Trim(),
            StateOrProvince = stateOrProvince?.Trim(),
            PostalCode      = postalCode?.Trim(),
            CountryCode     = countryCode?.Trim().ToUpperInvariant(),
            CreatedAtUtc    = now,
            UpdatedAtUtc    = now
        };
    }

    /// <summary>Used when migrating an existing record from Identity — preserves the original Id.</summary>
    public static Tenant Rehydrate(
        Guid         id,
        string       code,
        string       displayName,
        TenantStatus status,
        string?      legalName           = null,
        string?      subdomain           = null,
        Guid?        logoDocumentId      = null,
        Guid?        logoWhiteDocumentId = null,
        string?      timeZone            = null,
        DateTime?    createdAtUtc        = null,
        DateTime?    updatedAtUtc        = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        var now = DateTime.UtcNow;
        return new Tenant
        {
            Id                  = id,
            Code                = code.Trim().ToLowerInvariant(),
            DisplayName         = displayName.Trim(),
            LegalName           = legalName?.Trim(),
            Status              = status,
            Subdomain           = subdomain?.Trim().ToLowerInvariant(),
            LogoDocumentId      = logoDocumentId,
            LogoWhiteDocumentId = logoWhiteDocumentId,
            TimeZone            = timeZone,
            CreatedAtUtc        = createdAtUtc ?? now,
            UpdatedAtUtc        = updatedAtUtc ?? now
        };
    }

    // ── Mutators ──────────────────────────────────────────────────────────────

    public void UpdateProfile(
        string  displayName,
        string? legalName    = null,
        string? description  = null,
        string? websiteUrl   = null,
        string? timeZone     = null,
        string? locale       = null,
        string? supportEmail = null,
        string? supportPhone = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        DisplayName  = displayName.Trim();
        LegalName    = legalName?.Trim();
        Description  = description?.Trim();
        WebsiteUrl   = websiteUrl?.Trim();
        TimeZone     = timeZone;
        Locale       = locale;
        SupportEmail = supportEmail?.Trim().ToLowerInvariant();
        SupportPhone = supportPhone?.Trim();
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void UpdateAddress(
        string? addressLine1,
        string? addressLine2,
        string? city,
        string? stateOrProvince,
        string? postalCode,
        string? countryCode)
    {
        AddressLine1    = addressLine1?.Trim();
        AddressLine2    = addressLine2?.Trim();
        City            = city?.Trim();
        StateOrProvince = stateOrProvince?.Trim();
        PostalCode      = postalCode?.Trim();
        CountryCode     = countryCode?.Trim().ToUpperInvariant();
        UpdatedAtUtc    = DateTime.UtcNow;
    }

    public void SetStatus(TenantStatus status)
    {
        Status       = status;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void SetSubdomain(string? subdomain)
    {
        Subdomain    = subdomain?.Trim().ToLowerInvariant();
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void SetLogo(Guid? documentId)
    {
        LogoDocumentId = documentId;
        UpdatedAtUtc   = DateTime.UtcNow;
    }

    public void SetLogoWhite(Guid? documentId)
    {
        LogoWhiteDocumentId = documentId;
        UpdatedAtUtc        = DateTime.UtcNow;
    }
}
