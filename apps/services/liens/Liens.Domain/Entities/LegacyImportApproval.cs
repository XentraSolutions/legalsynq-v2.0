namespace Liens.Domain.Entities;

/// <summary>
/// Immutable release approval consumed by a controlled legacy import.
/// The approval is created by an Identity-owned release process, never by an
/// importer, and contains no source PII/PHI.
/// </summary>
public sealed class LegacyImportApproval
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid OrgId { get; private set; }
    public string SourceSystem { get; private set; } = string.Empty;
    public string SourceFingerprint { get; private set; } = string.Empty;
    public string LegacyProgram { get; private set; } = string.Empty;
    public string MappingVersion { get; private set; } = string.Empty;
    public string MappingManifestHash { get; private set; } = string.Empty;
    public string MappingApprovalReference { get; private set; } = string.Empty;
    public string LienAmountSource { get; private set; } = string.Empty;
    public string LegacyStatusOneTarget { get; private set; } = string.Empty;
    public string LegacyStatusTwoTarget { get; private set; } = string.Empty;
    public Guid MigrationUserId { get; private set; }
    public Guid ApprovedByUserId { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public DateTime ApprovedAtUtc { get; private set; }
    public DateTime? ExpiresAtUtc { get; private set; }
    public DateTime? ConsumedAtUtc { get; private set; }
    public Guid? ConsumedByRunId { get; private set; }

    private LegacyImportApproval() { }
}
