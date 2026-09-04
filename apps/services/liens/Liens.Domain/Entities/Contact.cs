using BuildingBlocks.Domain;
using Liens.Domain.Enums;

namespace Liens.Domain.Entities;

public class Contact : AuditableEntity
{
    public Guid Id           { get; private set; }
    public Guid TenantId     { get; private set; }
    public Guid OrgId        { get; private set; }
    public Guid? FacilityId  { get; private set; }
    public Guid? LawFirmId   { get; private set; }

    public string ContactType { get; private set; } = Enums.ContactType.InternalUser;
    public string? ContactSubtype { get; private set; }

    public string FirstName   { get; private set; } = string.Empty;
    public string LastName    { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;

    public string? Title        { get; private set; }
    public string? Organization { get; private set; }
    public string? Email        { get; private set; }
    public string? Phone        { get; private set; }
    public string? PhoneExtension { get; private set; }
    public string? Fax          { get; private set; }
    public string? Website      { get; private set; }

    public string? AddressLine1 { get; private set; }
    public string? City         { get; private set; }
    public string? State        { get; private set; }
    public string? PostalCode   { get; private set; }

    public string? Notes    { get; private set; }
    public bool    IsActive { get; private set; }

    private Contact() { }

    public static Contact Create(
        Guid tenantId,
        Guid orgId,
        string contactType,
        string firstName,
        string lastName,
        Guid createdByUserId,
        Guid? facilityId = null,
        Guid? lawFirmId = null,
        string? contactSubtype = null,
        string? title = null,
        string? organization = null,
        string? email = null,
        string? phone = null,
        string? fax = null,
        string? website = null,
        string? addressLine1 = null,
        string? city = null,
        string? state = null,
        string? postalCode = null,
        string? notes = null,
        string? phoneExtension = null)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (orgId == Guid.Empty) throw new ArgumentException("OrgId is required.", nameof(orgId));
        if (createdByUserId == Guid.Empty) throw new ArgumentException("CreatedByUserId is required.", nameof(createdByUserId));
        ArgumentException.ThrowIfNullOrWhiteSpace(firstName);
        if (RequiresLastName(contactType, contactSubtype, lawFirmId))
            ArgumentException.ThrowIfNullOrWhiteSpace(lastName);

        if (!Enums.ContactType.All.Contains(contactType))
            throw new ArgumentException($"Invalid contact type: '{contactType}'.");
        if (!string.IsNullOrWhiteSpace(contactSubtype) && !Enums.ContactSubtype.All.Contains(contactSubtype))
            throw new ArgumentException($"Invalid contact subtype: '{contactSubtype}'.");
        if (facilityId.HasValue && facilityId.Value == Guid.Empty)
            throw new ArgumentException("FacilityId cannot be empty.", nameof(facilityId));
        if (lawFirmId.HasValue && lawFirmId.Value == Guid.Empty)
            throw new ArgumentException("LawFirmId cannot be empty.", nameof(lawFirmId));

        var now = DateTime.UtcNow;
        return new Contact
        {
            Id           = Guid.CreateVersion7(),
            TenantId     = tenantId,
            OrgId        = orgId,
            FacilityId   = facilityId,
            LawFirmId    = lawFirmId,
            ContactType  = contactType,
            ContactSubtype = string.IsNullOrWhiteSpace(contactSubtype) ? null : contactSubtype.Trim(),
            FirstName    = firstName.Trim(),
            LastName     = lastName.Trim(),
            DisplayName  = BuildDisplayName(firstName, lastName),
            Title        = title?.Trim(),
            Organization = organization?.Trim(),
            Email        = email?.Trim(),
            Phone        = phone?.Trim(),
            PhoneExtension = NormalizeOptional(phoneExtension),
            Fax          = fax?.Trim(),
            Website      = website?.Trim(),
            AddressLine1 = addressLine1?.Trim(),
            City         = city?.Trim(),
            State        = state?.Trim(),
            PostalCode   = postalCode?.Trim(),
            Notes        = notes?.Trim(),
            IsActive     = true,
            CreatedByUserId = createdByUserId,
            UpdatedByUserId = createdByUserId,
            CreatedAtUtc    = now,
            UpdatedAtUtc    = now,
        };
    }

    public void Update(
        string firstName,
        string lastName,
        string contactType,
        Guid updatedByUserId,
        Guid? facilityId = null,
        Guid? lawFirmId = null,
        string? contactSubtype = null,
        string? title = null,
        string? organization = null,
        string? email = null,
        string? phone = null,
        string? fax = null,
        string? website = null,
        string? addressLine1 = null,
        string? city = null,
        string? state = null,
        string? postalCode = null,
        string? notes = null,
        string? phoneExtension = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(firstName);
        if (RequiresLastName(contactType, contactSubtype, lawFirmId))
            ArgumentException.ThrowIfNullOrWhiteSpace(lastName);

        if (!Enums.ContactType.All.Contains(contactType))
            throw new ArgumentException($"Invalid contact type: '{contactType}'.");
        if (!string.IsNullOrWhiteSpace(contactSubtype) && !Enums.ContactSubtype.All.Contains(contactSubtype))
            throw new ArgumentException($"Invalid contact subtype: '{contactSubtype}'.");
        if (facilityId.HasValue && facilityId.Value == Guid.Empty)
            throw new ArgumentException("FacilityId cannot be empty.", nameof(facilityId));
        if (lawFirmId.HasValue && lawFirmId.Value == Guid.Empty)
            throw new ArgumentException("LawFirmId cannot be empty.", nameof(lawFirmId));

        FirstName    = firstName.Trim();
        LastName     = lastName.Trim();
        DisplayName  = BuildDisplayName(firstName, lastName);
        FacilityId   = facilityId;
        LawFirmId    = lawFirmId;
        ContactType  = contactType;
        ContactSubtype = string.IsNullOrWhiteSpace(contactSubtype) ? null : contactSubtype.Trim();
        Title        = title?.Trim();
        Organization = organization?.Trim();
        Email        = email?.Trim();
        Phone        = phone?.Trim();
        PhoneExtension = NormalizeOptional(phoneExtension);
        Fax          = fax?.Trim();
        Website      = website?.Trim();
        AddressLine1 = addressLine1?.Trim();
        City         = city?.Trim();
        State        = state?.Trim();
        PostalCode   = postalCode?.Trim();
        Notes        = notes?.Trim();
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc    = DateTime.UtcNow;
    }

    public void Deactivate(Guid updatedByUserId)
    {
        IsActive        = false;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc    = DateTime.UtcNow;
    }

    public void Reactivate(Guid updatedByUserId)
    {
        IsActive        = true;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc    = DateTime.UtcNow;
    }

    private static bool RequiresLastName(string contactType, string? contactSubtype, Guid? lawFirmId)
        => !IsStandaloneLawFirm(contactType, contactSubtype, lawFirmId);

    private static bool IsStandaloneLawFirm(string contactType, string? contactSubtype, Guid? lawFirmId)
        => string.Equals(contactType, Enums.ContactType.LawFirm, StringComparison.Ordinal)
           && string.IsNullOrWhiteSpace(contactSubtype)
           && !lawFirmId.HasValue;

    private static string BuildDisplayName(string firstName, string lastName)
        => string.Join(" ",
            new[] { firstName.Trim(), lastName.Trim() }
                .Where(part => !string.IsNullOrWhiteSpace(part)));

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
