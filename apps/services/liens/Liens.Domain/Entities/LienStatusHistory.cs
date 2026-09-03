using BuildingBlocks.Domain;

namespace Liens.Domain.Entities;

public sealed class LienStatusHistory : AuditableEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid? CaseId { get; private set; }
    public Guid LienId { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public Guid ChangedByUserId { get; private set; }
    public DateTime ChangedAtUtc { get; private set; }

    private LienStatusHistory() { }

    public static LienStatusHistory Create(
        Guid tenantId,
        Guid lienId,
        Guid? caseId,
        string description,
        Guid changedByUserId)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (lienId == Guid.Empty) throw new ArgumentException("LienId is required.", nameof(lienId));
        if (changedByUserId == Guid.Empty) throw new ArgumentException("ChangedByUserId is required.", nameof(changedByUserId));
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        var now = DateTime.UtcNow;
        return new LienStatusHistory
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            CaseId = caseId,
            LienId = lienId,
            Description = description.Trim(),
            ChangedByUserId = changedByUserId,
            ChangedAtUtc = now,
            CreatedByUserId = changedByUserId,
            UpdatedByUserId = changedByUserId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
    }

    public void ReplacePendingDescription(string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Description = description.Trim();
    }
}
