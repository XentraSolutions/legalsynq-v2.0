using BuildingBlocks.Domain;

namespace Liens.Domain.Entities;

public class CompanyContactPerson : AuditableEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid CompanyId { get; private set; }
    public Guid ContactPersonTypeId { get; private set; }
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string? AddressLine1 { get; private set; }
    public string? City { get; private set; }
    public string? State { get; private set; }
    public string? PostalCode { get; private set; }
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public bool IsActive { get; private set; }

    public Company? Company { get; private set; }
    public ContactPersonType? ContactPersonType { get; private set; }

    private CompanyContactPerson() { }

    public static CompanyContactPerson Create(
        Guid tenantId,
        Guid companyId,
        Guid contactPersonTypeId,
        string firstName,
        string lastName,
        Guid createdByUserId,
        string? addressLine1 = null,
        string? city = null,
        string? state = null,
        string? postalCode = null,
        string? phone = null,
        string? email = null)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (companyId == Guid.Empty) throw new ArgumentException("CompanyId is required.", nameof(companyId));
        if (contactPersonTypeId == Guid.Empty) throw new ArgumentException("ContactPersonTypeId is required.", nameof(contactPersonTypeId));
        if (createdByUserId == Guid.Empty) throw new ArgumentException("CreatedByUserId is required.", nameof(createdByUserId));
        ArgumentException.ThrowIfNullOrWhiteSpace(firstName);
        ArgumentException.ThrowIfNullOrWhiteSpace(lastName);

        var now = DateTime.UtcNow;
        return new CompanyContactPerson
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            CompanyId = companyId,
            ContactPersonTypeId = contactPersonTypeId,
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
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
        Guid contactPersonTypeId,
        string firstName,
        string lastName,
        Guid updatedByUserId,
        string? addressLine1 = null,
        string? city = null,
        string? state = null,
        string? postalCode = null,
        string? phone = null,
        string? email = null)
    {
        if (contactPersonTypeId == Guid.Empty) throw new ArgumentException("ContactPersonTypeId is required.", nameof(contactPersonTypeId));
        if (updatedByUserId == Guid.Empty) throw new ArgumentException("UpdatedByUserId is required.", nameof(updatedByUserId));
        ArgumentException.ThrowIfNullOrWhiteSpace(firstName);
        ArgumentException.ThrowIfNullOrWhiteSpace(lastName);

        ContactPersonTypeId = contactPersonTypeId;
        FirstName = firstName.Trim();
        LastName = lastName.Trim();
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

    public void ReassignCompany(Guid companyId, Guid updatedByUserId)
    {
        if (companyId == Guid.Empty) throw new ArgumentException("CompanyId is required.", nameof(companyId));
        if (updatedByUserId == Guid.Empty) throw new ArgumentException("UpdatedByUserId is required.", nameof(updatedByUserId));
        CompanyId = companyId;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    private void SetActive(bool isActive, Guid updatedByUserId)
    {
        if (updatedByUserId == Guid.Empty) throw new ArgumentException("UpdatedByUserId is required.", nameof(updatedByUserId));
        IsActive = isActive;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
