using BuildingBlocks.Domain;
using Liens.Domain.Enums;

namespace Liens.Domain.Entities;

public class Contact : AuditableEntity
{
    public Guid Id           { get; private set; }
    public Guid TenantId     { get; private set; }
    public Guid OrgId        { get; private set; }

    public string ContactType { get; private set; } = Enums.ContactType.InternalUser;

    public string FirstName   { get; private set; } = string.Empty;
    public string LastName    { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;

    public string? Title        { get; private set; }
    public string? Organization { get; private set; }
    public string? Email        { get; private set; }
    public string? Phone        { get; private set; }
    public string? Fax          { get; private set; }
    public string? Website      { get; private set; }

    public string? AddressLine1 { get; private set; }
    public string? AddressLine2 { get; private set; }
    public string? City         { get; private set; }
    public string? State        { get; private set; }
    public string? PostalCode   { get; private set; }

    // Facility-specific (only populated when ContactType == Facility)
    public string? Code              { get; private set; }
    public string? ExternalReference { get; private set; }

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
        string? title = null,
        string? organization = null,
        string? email = null,
        string? phone = null,
        string? fax = null,
        string? website = null,
        string? addressLine1 = null,
        string? addressLine2 = null,
        string? city = null,
        string? state = null,
        string? postalCode = null,
        string? notes = null,
        string? code = null,
        string? externalReference = null)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (orgId == Guid.Empty) throw new ArgumentException("OrgId is required.", nameof(orgId));
        if (createdByUserId == Guid.Empty) throw new ArgumentException("CreatedByUserId is required.", nameof(createdByUserId));
        ArgumentException.ThrowIfNullOrWhiteSpace(firstName);

        bool isFacility = contactType == Enums.ContactType.Facility;
        if (!isFacility)
            ArgumentException.ThrowIfNullOrWhiteSpace(lastName);

        if (!Enums.ContactType.All.Contains(contactType))
            throw new ArgumentException($"Invalid contact type: '{contactType}'.");

        var trimFirst = firstName.Trim();
        var trimLast  = lastName?.Trim() ?? string.Empty;
        var displayName = isFacility ? trimFirst : $"{trimFirst} {trimLast}";

        var now = DateTime.UtcNow;
        return new Contact
        {
            Id               = Guid.NewGuid(),
            TenantId         = tenantId,
            OrgId            = orgId,
            ContactType      = contactType,
            FirstName        = trimFirst,
            LastName         = trimLast,
            DisplayName      = displayName,
            Title            = title?.Trim(),
            Organization     = organization?.Trim(),
            Email            = email?.Trim(),
            Phone            = phone?.Trim(),
            Fax              = fax?.Trim(),
            Website          = website?.Trim(),
            AddressLine1     = addressLine1?.Trim(),
            AddressLine2     = addressLine2?.Trim(),
            City             = city?.Trim(),
            State            = state?.Trim(),
            PostalCode       = postalCode?.Trim(),
            Notes            = notes?.Trim(),
            Code             = code?.Trim(),
            ExternalReference = externalReference?.Trim(),
            IsActive         = true,
            CreatedByUserId  = createdByUserId,
            UpdatedByUserId  = createdByUserId,
            CreatedAtUtc     = now,
            UpdatedAtUtc     = now,
        };
    }

    public void Update(
        string firstName,
        string lastName,
        string contactType,
        Guid updatedByUserId,
        string? title = null,
        string? organization = null,
        string? email = null,
        string? phone = null,
        string? fax = null,
        string? website = null,
        string? addressLine1 = null,
        string? addressLine2 = null,
        string? city = null,
        string? state = null,
        string? postalCode = null,
        string? notes = null,
        string? code = null,
        string? externalReference = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(firstName);

        bool isFacility = contactType == Enums.ContactType.Facility;
        if (!isFacility)
            ArgumentException.ThrowIfNullOrWhiteSpace(lastName);

        if (!Enums.ContactType.All.Contains(contactType))
            throw new ArgumentException($"Invalid contact type: '{contactType}'.");

        var trimFirst = firstName.Trim();
        var trimLast  = lastName?.Trim() ?? string.Empty;

        FirstName        = trimFirst;
        LastName         = trimLast;
        DisplayName      = isFacility ? trimFirst : $"{trimFirst} {trimLast}";
        ContactType      = contactType;
        Title            = title?.Trim();
        Organization     = organization?.Trim();
        Email            = email?.Trim();
        Phone            = phone?.Trim();
        Fax              = fax?.Trim();
        Website          = website?.Trim();
        AddressLine1     = addressLine1?.Trim();
        AddressLine2     = addressLine2?.Trim();
        City             = city?.Trim();
        State            = state?.Trim();
        PostalCode       = postalCode?.Trim();
        Notes            = notes?.Trim();
        Code             = code?.Trim();
        ExternalReference = externalReference?.Trim();
        UpdatedByUserId  = updatedByUserId;
        UpdatedAtUtc     = DateTime.UtcNow;
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
}
