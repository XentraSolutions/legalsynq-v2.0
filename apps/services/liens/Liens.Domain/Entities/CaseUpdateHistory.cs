using BuildingBlocks.Domain;

namespace Liens.Domain.Entities;

public sealed class CaseUpdateHistory : AuditableEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid CaseId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public Guid ActorUserId { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }

    private CaseUpdateHistory() { }

    public static CaseUpdateHistory Create(
        Guid tenantId,
        Guid caseId,
        string action,
        string description,
        Guid actorUserId,
        DateTime? occurredAtUtc = null)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (caseId == Guid.Empty) throw new ArgumentException("CaseId is required.", nameof(caseId));
        if (actorUserId == Guid.Empty) throw new ArgumentException("ActorUserId is required.", nameof(actorUserId));
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        var timestamp = occurredAtUtc ?? DateTime.UtcNow;
        return new CaseUpdateHistory
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            CaseId = caseId,
            Action = action.Trim(),
            Description = description.Trim(),
            ActorUserId = actorUserId,
            OccurredAtUtc = timestamp,
            CreatedByUserId = actorUserId,
            UpdatedByUserId = actorUserId,
            CreatedAtUtc = timestamp,
            UpdatedAtUtc = timestamp,
        };
    }
}
