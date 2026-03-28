using BuildingBlocks.Domain;

namespace CareConnect.Domain;

public class Facility : AuditableEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string AddressLine1 { get; private set; } = string.Empty;
    public string City { get; private set; } = string.Empty;
    public string State { get; private set; } = string.Empty;
    public string PostalCode { get; private set; } = string.Empty;
    public string? Phone { get; private set; }
    public bool IsActive { get; private set; }

    public List<ProviderFacility> ProviderFacilities { get; private set; } = new();

    private Facility() { }

    public static Facility Create(
        Guid tenantId,
        string name,
        string addressLine1,
        string city,
        string state,
        string postalCode,
        string? phone,
        bool isActive,
        Guid? createdByUserId)
    {
        return new Facility
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name.Trim(),
            AddressLine1 = addressLine1.Trim(),
            City = city.Trim(),
            State = state.Trim(),
            PostalCode = postalCode.Trim(),
            Phone = phone?.Trim(),
            IsActive = isActive,
            CreatedByUserId = createdByUserId,
            UpdatedByUserId = createdByUserId,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
    }

    public void Update(
        string name,
        string addressLine1,
        string city,
        string state,
        string postalCode,
        string? phone,
        bool isActive,
        Guid? updatedByUserId)
    {
        Name = name.Trim();
        AddressLine1 = addressLine1.Trim();
        City = city.Trim();
        State = state.Trim();
        PostalCode = postalCode.Trim();
        Phone = phone?.Trim();
        IsActive = isActive;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
