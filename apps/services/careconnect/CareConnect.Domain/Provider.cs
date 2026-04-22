using BuildingBlocks.Domain;

namespace CareConnect.Domain;

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

    public List<ProviderCategory> ProviderCategories { get; private set; } = new();

    /// <summary>
    /// Phase D: link this provider record to the corresponding Identity Organization.
    /// Sets the soft FK OrganizationId so cross-service identity can be resolved.
    /// </summary>
    public void LinkOrganization(Guid organizationId)
    {
        OrganizationId = organizationId;
        UpdatedAtUtc = DateTime.UtcNow;
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
