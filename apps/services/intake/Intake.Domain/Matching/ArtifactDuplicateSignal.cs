namespace Intake.Domain.Matching;

public sealed class ArtifactDuplicateSignal
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ArtifactMatchRunId { get; set; }
    public string DuplicateType { get; set; } = string.Empty;
    public Guid? RelatedArtifactId { get; set; }
    public string? RelatedBusinessEntityType { get; set; }
    public Guid? RelatedBusinessEntityId { get; set; }
    public decimal Score { get; set; }
    public string Status { get; set; } = DuplicateStatuses.Possible;
    public string ReasonCode { get; set; } = string.Empty;
    public string EvidenceJson { get; set; } = "[]";
    public DateTimeOffset CreatedAt { get; set; }
}