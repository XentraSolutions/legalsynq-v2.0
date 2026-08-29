namespace Liens.Domain.Entities;

/// <summary>
/// Append-only, tenant-scoped history imported from a legacy update log.
/// This entity preserves the source text and actor display name as evidence.
/// </summary>
public sealed class LegacyUpdateEvent
{
    public const string CaseScope = "Case";
    public const string LienScope = "Lien";

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid OrgId { get; private set; }
    public Guid CaseId { get; private set; }
    public Guid? LienId { get; private set; }
    public string Scope { get; private set; } = string.Empty;
    public string Action { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string? ActorDisplayName { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }
    public DateTime ImportedAtUtc { get; private set; }
    public Guid ImportRunId { get; private set; }
    public string SourceSystem { get; private set; } = string.Empty;
    public string SourceTable { get; private set; } = string.Empty;
    public string LegacyId { get; private set; } = string.Empty;
    public long LegacySequence { get; private set; }

    private LegacyUpdateEvent() { }

    public static LegacyUpdateEvent Create(
        Guid tenantId,
        Guid orgId,
        Guid caseId,
        Guid? lienId,
        string scope,
        string action,
        string? description,
        string? actorDisplayName,
        DateTime occurredAtUtc,
        DateTime importedAtUtc,
        Guid importRunId,
        string sourceSystem,
        string sourceTable,
        string legacyId,
        long legacySequence)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (orgId == Guid.Empty) throw new ArgumentException("OrgId is required.", nameof(orgId));
        if (caseId == Guid.Empty) throw new ArgumentException("CaseId is required.", nameof(caseId));
        if (importRunId == Guid.Empty) throw new ArgumentException("ImportRunId is required.", nameof(importRunId));
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceSystem);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceTable);
        ArgumentException.ThrowIfNullOrWhiteSpace(legacyId);

        if (scope is not (CaseScope or LienScope))
            throw new ArgumentException("Scope must be Case or Lien.", nameof(scope));
        if (scope == CaseScope && lienId is not null)
            throw new ArgumentException("Case-scoped events cannot reference a lien.", nameof(lienId));
        if (scope == LienScope && (lienId is null || lienId == Guid.Empty))
            throw new ArgumentException("Lien-scoped events require a lien.", nameof(lienId));
        if (occurredAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("OccurredAtUtc must be UTC.", nameof(occurredAtUtc));
        if (importedAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("ImportedAtUtc must be UTC.", nameof(importedAtUtc));

        return new LegacyUpdateEvent
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            OrgId = orgId,
            CaseId = caseId,
            LienId = lienId,
            Scope = scope,
            Action = action,
            Description = description,
            ActorDisplayName = actorDisplayName,
            OccurredAtUtc = occurredAtUtc,
            ImportedAtUtc = importedAtUtc,
            ImportRunId = importRunId,
            SourceSystem = sourceSystem,
            SourceTable = sourceTable,
            LegacyId = legacyId,
            LegacySequence = legacySequence,
        };
    }
}
