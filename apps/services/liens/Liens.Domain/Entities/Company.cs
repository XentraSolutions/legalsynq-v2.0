using BuildingBlocks.Domain;

namespace Liens.Domain.Entities;

public class Company : AuditableEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid OrgId { get; private set; }
    public Guid? LinkedTenantId { get; private set; }
    public Guid CompanyTypeId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public string? AddressLine1 { get; private set; }
    public string? City { get; private set; }
    public string? State { get; private set; }
    public string? PostalCode { get; private set; }
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public bool IsActive { get; private set; }

    public CompanyType? CompanyType { get; private set; }

    private Company() { }

    public static Company Create(
        Guid tenantId,
        Guid orgId,
        Guid companyTypeId,
        string name,
        Guid createdByUserId,
        Guid? linkedTenantId = null,
        string? addressLine1 = null,
        string? city = null,
        string? state = null,
        string? postalCode = null,
        string? phone = null,
        string? email = null)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (orgId == Guid.Empty) throw new ArgumentException("OrgId is required.", nameof(orgId));
        if (companyTypeId == Guid.Empty) throw new ArgumentException("CompanyTypeId is required.", nameof(companyTypeId));
        if (createdByUserId == Guid.Empty) throw new ArgumentException("CreatedByUserId is required.", nameof(createdByUserId));
        if (linkedTenantId == Guid.Empty) throw new ArgumentException("LinkedTenantId cannot be empty.", nameof(linkedTenantId));
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var now = DateTime.UtcNow;
        return new Company
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            OrgId = orgId,
            LinkedTenantId = linkedTenantId,
            CompanyTypeId = companyTypeId,
            Name = name.Trim(),
            NormalizedName = NormalizeName(name),
            AddressLine1 = addressLine1?.Trim(),
            City = city?.Trim(),
            State = state?.Trim(),
            PostalCode = postalCode?.Trim(),
            Phone = phone?.Trim(),
            Email = email?.Trim(),
            IsActive = true,
            CreatedByUserId = createdByUserId,
            UpdatedByUserId = createdByUserId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
    }

    public void Update(
        string name,
        Guid updatedByUserId,
        Guid? linkedTenantId = null,
        string? addressLine1 = null,
        string? city = null,
        string? state = null,
        string? postalCode = null,
        string? phone = null,
        string? email = null)
    {
        if (updatedByUserId == Guid.Empty) throw new ArgumentException("UpdatedByUserId is required.", nameof(updatedByUserId));
        if (linkedTenantId == Guid.Empty) throw new ArgumentException("LinkedTenantId cannot be empty.", nameof(linkedTenantId));
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name.Trim();
        NormalizedName = NormalizeName(name);
        LinkedTenantId = linkedTenantId;
        AddressLine1 = addressLine1?.Trim();
        City = city?.Trim();
        State = state?.Trim();
        PostalCode = postalCode?.Trim();
        Phone = phone?.Trim();
        Email = email?.Trim();
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Deactivate(Guid updatedByUserId) => SetActive(false, updatedByUserId);

    public void Reactivate(Guid updatedByUserId) => SetActive(true, updatedByUserId);

    public static string NormalizeName(string name) => name.Trim().ToUpperInvariant();

    private void SetActive(bool isActive, Guid updatedByUserId)
    {
        if (updatedByUserId == Guid.Empty) throw new ArgumentException("UpdatedByUserId is required.", nameof(updatedByUserId));
        IsActive = isActive;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
