using BuildingBlocks.Domain;

namespace Liens.Domain.Entities;

public sealed class SellingPartyBackfillQuarantine : AuditableEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Namespace { get; private set; } = string.Empty;
    public string WorkflowProvenance { get; private set; } = string.Empty;
    public Guid ExternalId { get; private set; }
    public string ReasonCode { get; private set; } = string.Empty;
    public string Details { get; private set; } = string.Empty;

    private SellingPartyBackfillQuarantine() { }

    public static SellingPartyBackfillQuarantine Create(
        Guid tenantId, string aliasNamespace, string workflowProvenance,
        Guid externalId, string reasonCode, string details, Guid actorUserId)
    {
        if (tenantId == Guid.Empty || externalId == Guid.Empty || actorUserId == Guid.Empty)
            throw new ArgumentException("Tenant, external, and actor IDs are required.");
        ArgumentException.ThrowIfNullOrWhiteSpace(aliasNamespace);
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowProvenance);
        ArgumentException.ThrowIfNullOrWhiteSpace(reasonCode);
        var now = DateTime.UtcNow;
        return new SellingPartyBackfillQuarantine
        {
            Id = Guid.CreateVersion7(), TenantId = tenantId, Namespace = aliasNamespace.Trim(),
            WorkflowProvenance = workflowProvenance.Trim(), ExternalId = externalId,
            ReasonCode = reasonCode.Trim(), Details = details.Length <= 4000 ? details : details[..4000],
            CreatedByUserId = actorUserId, UpdatedByUserId = actorUserId,
            CreatedAtUtc = now, UpdatedAtUtc = now,
        };
    }
}
