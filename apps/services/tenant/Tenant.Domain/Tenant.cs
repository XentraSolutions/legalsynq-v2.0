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
    Pending,
    Active,
    Inactive,
    Suspended
}

/// <summary>
/// Core Tenant entity — the canonical record for a company/organisation.
///
/// This entity is the future owner of tenant master data, replacing the
/// tenant data currently held inside Identity.Domain.Tenant.
///
/// Identity field mapping (Block 1 in-scope):
///   Identity.Id                   → Id               (preserved, cross-service FK)
///   Identity.Name                 → DisplayName
///   Identity.Code                 → Code
///   Identity.IsActive             → Status
///   Identity.Subdomain            → Subdomain
///   Identity.LogoDocumentId       → LogoDocumentId
///   Identity.LogoWhiteDocumentId  → LogoWhiteDocumentId
///   Identity.CreatedAtUtc         → CreatedAtUtc
///   Identity.UpdatedAtUtc         → UpdatedAtUtc
///
/// Deferred fields (added in later blocks):
///   Address, geo, provisioning lifecycle, branding palette,
///   product entitlements, domain ownership, settings.
/// </summary>
public class Tenant
{
    public Guid Id { get; private set; }

    /// <summary>
    /// Short, URL-safe identifier (slug). Unique across all tenants.
    /// Preserved exactly from Identity.Code.
    /// </summary>
    public string Code { get; private set; } = string.Empty;

    /// <summary>
    /// Human-readable display name. Maps from Identity.Name.
    /// </summary>
    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>
    /// Optional formal legal entity name. Not present in Identity; added here.
    /// </summary>
    public string? LegalName { get; private set; }

    /// <summary>
    /// Active/Inactive/Pending lifecycle. Maps from Identity.IsActive and provisioning state.
    /// </summary>
    public TenantStatus Status { get; private set; }

    /// <summary>
    /// Assigned subdomain slug. Maps from Identity.Subdomain.
    /// </summary>
    public string? Subdomain { get; private set; }

    /// <summary>
    /// Reference to the logo document in the Documents service.
    /// Maps from Identity.LogoDocumentId. Tenant service does NOT own binary storage.
    /// </summary>
    public Guid? LogoDocumentId { get; private set; }

    /// <summary>
    /// Reference to the white-variant logo document. Maps from Identity.LogoWhiteDocumentId.
    /// </summary>
    public Guid? LogoWhiteDocumentId { get; private set; }

    /// <summary>
    /// IANA timezone code (e.g. "America/New_York"). Nullable — not yet present in Identity.
    /// </summary>
    public string? TimeZone { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private Tenant() { }

    public static Tenant Create(string code, string displayName, string? legalName = null, string? subdomain = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        var now = DateTime.UtcNow;
        return new Tenant
        {
            Id            = Guid.NewGuid(),
            Code          = code.Trim().ToLowerInvariant(),
            DisplayName   = displayName.Trim(),
            LegalName     = legalName?.Trim(),
            Status        = TenantStatus.Active,
            Subdomain     = subdomain?.Trim().ToLowerInvariant(),
            CreatedAtUtc  = now,
            UpdatedAtUtc  = now
        };
    }

    /// <summary>
    /// Used when migrating an existing record from Identity — preserves the original Id.
    /// </summary>
    public static Tenant Rehydrate(
        Guid id,
        string code,
        string displayName,
        TenantStatus status,
        string? legalName = null,
        string? subdomain = null,
        Guid? logoDocumentId = null,
        Guid? logoWhiteDocumentId = null,
        string? timeZone = null,
        DateTime? createdAtUtc = null,
        DateTime? updatedAtUtc = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        var now = DateTime.UtcNow;
        return new Tenant
        {
            Id                   = id,
            Code                 = code.Trim().ToLowerInvariant(),
            DisplayName          = displayName.Trim(),
            LegalName            = legalName?.Trim(),
            Status               = status,
            Subdomain            = subdomain?.Trim().ToLowerInvariant(),
            LogoDocumentId       = logoDocumentId,
            LogoWhiteDocumentId  = logoWhiteDocumentId,
            TimeZone             = timeZone,
            CreatedAtUtc         = createdAtUtc ?? now,
            UpdatedAtUtc         = updatedAtUtc ?? now
        };
    }

    public void UpdateProfile(string displayName, string? legalName, string? timeZone)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        DisplayName  = displayName.Trim();
        LegalName    = legalName?.Trim();
        TimeZone     = timeZone;
        UpdatedAtUtc = DateTime.UtcNow;
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
