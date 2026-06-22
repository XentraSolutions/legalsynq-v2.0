using BuildingBlocks.Domain;

namespace Liens.Domain.Entities;

public class ManualMedicalCode : AuditableEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string FacilityType { get; private set; } = "ASC";
    public decimal Cost { get; private set; }
    public decimal Copay { get; private set; }
    public decimal FacilityTotal { get; private set; }
    public decimal PhysicianTotal { get; private set; }
    public decimal Total { get; private set; }
    public string Status { get; private set; } = "A";

    private ManualMedicalCode() { }

    public static ManualMedicalCode Create(
        Guid tenantId,
        string code,
        string? description,
        string? facilityType,
        decimal cost,
        decimal copay,
        decimal facilityTotal,
        decimal physicianTotal,
        decimal total,
        Guid createdByUserId)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (createdByUserId == Guid.Empty) throw new ArgumentException("CreatedByUserId is required.", nameof(createdByUserId));
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        var now = DateTime.UtcNow;
        return new ManualMedicalCode
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            Code = code.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            FacilityType = string.IsNullOrWhiteSpace(facilityType) ? "ASC" : facilityType.Trim(),
            Cost = cost,
            Copay = copay,
            FacilityTotal = facilityTotal,
            PhysicianTotal = physicianTotal,
            Total = total,
            Status = "A",
            CreatedByUserId = createdByUserId,
            UpdatedByUserId = createdByUserId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
    }

    public void Update(
        string code,
        string? description,
        string? facilityType,
        decimal cost,
        decimal copay,
        decimal facilityTotal,
        decimal physicianTotal,
        decimal total,
        Guid updatedByUserId)
    {
        if (updatedByUserId == Guid.Empty) throw new ArgumentException("UpdatedByUserId is required.", nameof(updatedByUserId));
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        Code = code.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        FacilityType = string.IsNullOrWhiteSpace(facilityType) ? "ASC" : facilityType.Trim();
        Cost = cost;
        Copay = copay;
        FacilityTotal = facilityTotal;
        PhysicianTotal = physicianTotal;
        Total = total;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
