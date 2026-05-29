using BuildingBlocks.Domain;

namespace Liens.Domain.Entities;

public class LienReduction : AuditableEntity
{
    public Guid    Id           { get; private set; }
    public Guid    TenantId     { get; private set; }
    public Guid    CaseId       { get; private set; }
    public Guid    LienId       { get; private set; }
    public DateOnly ReductionDate { get; private set; }
    public decimal Amount       { get; private set; }
    public string? Note         { get; private set; }
    public bool    IsDeleted    { get; private set; }

    private LienReduction() { }

    public static LienReduction Create(
        Guid tenantId, Guid caseId, Guid lienId,
        DateOnly reductionDate, decimal amount, Guid createdByUserId, string? note = null)
    {
        if (tenantId == Guid.Empty)   throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (caseId == Guid.Empty)     throw new ArgumentException("CaseId is required.", nameof(caseId));
        if (lienId == Guid.Empty)     throw new ArgumentException("LienId is required.", nameof(lienId));
        if (createdByUserId == Guid.Empty) throw new ArgumentException("CreatedByUserId is required.", nameof(createdByUserId));

        var now = DateTime.UtcNow;
        return new LienReduction
        {
            Id              = Guid.CreateVersion7(),
            TenantId        = tenantId,
            CaseId          = caseId,
            LienId          = lienId,
            ReductionDate   = reductionDate,
            Amount          = amount,
            Note            = note?.Trim(),
            IsDeleted       = false,
            CreatedByUserId = createdByUserId,
            UpdatedByUserId = createdByUserId,
            CreatedAtUtc    = now,
            UpdatedAtUtc    = now,
        };
    }

    public void Update(DateOnly reductionDate, decimal amount, Guid updatedByUserId, string? note = null)
    {
        ReductionDate   = reductionDate;
        Amount          = amount;
        Note            = note?.Trim();
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc    = DateTime.UtcNow;
    }

    public void SoftDelete(Guid updatedByUserId)
    {
        IsDeleted       = true;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc    = DateTime.UtcNow;
    }
}
