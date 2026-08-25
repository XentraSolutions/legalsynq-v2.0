namespace Liens.Domain.Entities;

/// <summary>
/// Tenant-scoped, non-sensitive evidence for an idempotent legacy field wave.
/// Hashes identify source/preimage/applied values without storing PII or PHI.
/// </summary>
public sealed class LegacyFieldMigrationState
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string SourceSystem { get; private set; } = string.Empty;
    public string SourceTable { get; private set; } = string.Empty;
    public string LegacyId { get; private set; } = string.Empty;
    public string MappingVersion { get; private set; } = string.Empty;
    public string FieldGroup { get; private set; } = string.Empty;
    public string TargetEntity { get; private set; } = string.Empty;
    public Guid TargetId { get; private set; }
    public string SourceHash { get; private set; } = string.Empty;
    public string? TargetPreimageHash { get; private set; }
    public string? AppliedValueHash { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public Guid ImportRunId { get; private set; }
    public DateTime? AppliedAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private LegacyFieldMigrationState() { }
}
