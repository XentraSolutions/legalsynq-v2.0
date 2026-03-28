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

    public List<ProviderCategory> ProviderCategories { get; private set; } = new();

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
        string? geoPointSource = null)
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
