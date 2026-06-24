using BuildingBlocks.Domain;

namespace Liens.Domain.Entities;

public class FacilityContactPerson : AuditableEntity
{
    public Guid   Id         { get; private set; }
    public Guid   TenantId   { get; private set; }
    public Guid   FacilityId { get; private set; }

    public string  FirstName { get; private set; } = string.Empty;
    public string  LastName  { get; private set; } = string.Empty;
    public string? Position  { get; private set; }
    public string? Email     { get; private set; }
    public string? Phone     { get; private set; }
    public bool    IsActive  { get; private set; }

    // Navigation
    public Facility? Facility { get; private set; }

    private FacilityContactPerson() { }

    public static FacilityContactPerson Create(
        Guid tenantId,
        Guid facilityId,
        string firstName,
        string lastName,
        Guid createdByUserId,
        string? position = null,
        string? email    = null,
        string? phone    = null)
    {
        if (tenantId == Guid.Empty)    throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (facilityId == Guid.Empty)  throw new ArgumentException("FacilityId is required.", nameof(facilityId));
        if (createdByUserId == Guid.Empty) throw new ArgumentException("CreatedByUserId is required.", nameof(createdByUserId));
        ArgumentException.ThrowIfNullOrWhiteSpace(firstName);
        ArgumentException.ThrowIfNullOrWhiteSpace(lastName);

        var now = DateTime.UtcNow;
        return new FacilityContactPerson
        {
            Id              = Guid.CreateVersion7(),
            TenantId        = tenantId,
            FacilityId      = facilityId,
            FirstName       = firstName.Trim(),
            LastName        = lastName.Trim(),
            Position        = position?.Trim(),
            Email           = email?.Trim(),
            Phone           = phone?.Trim(),
            IsActive        = true,
            CreatedByUserId = createdByUserId,
            UpdatedByUserId = createdByUserId,
            CreatedAtUtc    = now,
            UpdatedAtUtc    = now,
        };
    }

    public void Update(
        string firstName,
        string lastName,
        Guid updatedByUserId,
        string? position = null,
        string? email    = null,
        string? phone    = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(firstName);
        ArgumentException.ThrowIfNullOrWhiteSpace(lastName);

        FirstName       = firstName.Trim();
        LastName        = lastName.Trim();
        Position        = position?.Trim();
        Email           = email?.Trim();
        Phone           = phone?.Trim();
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
}
