namespace Liens.Domain.Entities;

/// <summary>
/// Maps an immutable legacy primary key to its LegalSynq GUID without
/// repurposing a business-facing external reference.
/// </summary>
public sealed class LegacyIdCrosswalk
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string SourceSystem { get; private set; } = string.Empty;
    public string SourceTable { get; private set; } = string.Empty;
    public string LegacyId { get; private set; } = string.Empty;
    public string TargetEntity { get; private set; } = string.Empty;
    public Guid TargetId { get; private set; }
    public string SourceHash { get; private set; } = string.Empty;
    public Guid ImportRunId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private LegacyIdCrosswalk() { }
}
