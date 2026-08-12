namespace Liens.Domain.Entities;

/// <summary>
/// Durable, tenant-scoped evidence for a legacy data import execution.
/// This is operational metadata only; it must not contain source PII/PHI.
/// </summary>
public sealed class LegacyImportRun
{
    public Guid Id { get; private set; }
    public Guid? ApprovalId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid OrgId { get; private set; }
    public string SourceSystem { get; private set; } = string.Empty;
    public string SourceFingerprint { get; private set; } = string.Empty;
    public string LegacyProgram { get; private set; } = string.Empty;
    public string MappingVersion { get; private set; } = string.Empty;
    public string MappingManifestHash { get; private set; } = string.Empty;
    public string MappingApprovalReference { get; private set; } = string.Empty;
    public string Status { get; private set; } = string.Empty;
    public DateTime StartedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public string? SummaryJson { get; private set; }
    public string? ErrorSummary { get; private set; }

    private LegacyImportRun() { }
}
