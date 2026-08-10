using BuildingBlocks.Domain;

namespace Liens.Domain.Entities;

public sealed class SellingPartyBackfillCheckpoint : AuditableEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Workflow { get; private set; } = string.Empty;
    public Guid LastExternalId { get; private set; }
    public string Status { get; private set; } = SellingPartyBackfillStatuses.Pending;
    public int ProcessedCount { get; private set; }
    public int QuarantinedCount { get; private set; }
    public string? LastError { get; private set; }

    private SellingPartyBackfillCheckpoint() { }

    public static SellingPartyBackfillCheckpoint Create(Guid tenantId, string workflow, Guid actorUserId)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (actorUserId == Guid.Empty) throw new ArgumentException("ActorUserId is required.", nameof(actorUserId));
        ArgumentException.ThrowIfNullOrWhiteSpace(workflow);
        var now = DateTime.UtcNow;
        return new SellingPartyBackfillCheckpoint
        {
            Id = Guid.CreateVersion7(), TenantId = tenantId, Workflow = workflow.Trim(),
            Status = SellingPartyBackfillStatuses.Pending, CreatedByUserId = actorUserId,
            UpdatedByUserId = actorUserId, CreatedAtUtc = now, UpdatedAtUtc = now,
        };
    }

    public void Advance(Guid lastExternalId, int processed, int quarantined, bool completed, Guid actorUserId)
    {
        LastExternalId = lastExternalId;
        ProcessedCount += processed;
        QuarantinedCount += quarantined;
        Status = completed ? SellingPartyBackfillStatuses.Completed : SellingPartyBackfillStatuses.Running;
        LastError = null;
        UpdatedByUserId = actorUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Fail(string error, Guid actorUserId)
    {
        Status = SellingPartyBackfillStatuses.Failed;
        LastError = error.Length <= 2000 ? error : error[..2000];
        UpdatedByUserId = actorUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
