namespace Intake.Domain.Snapshot;

public sealed class ApprovedIntakeSnapshot
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ArtifactId { get; set; }
    public Guid ReviewId { get; set; }
    public Guid PolicyEvaluationId { get; set; }
    public Guid? ClassificationId { get; set; }
    public Guid? ArtifactExtractionId { get; set; }
    public Guid? ArtifactNormalizationId { get; set; }
    public Guid? ArtifactMatchRunId { get; set; }
    public string ProcessingProfileCode { get; set; } = string.Empty;
    public string SchemaCode { get; set; } = string.Empty;
    public int SchemaVersion { get; set; }
    public int SnapshotVersion { get; set; }
    public string Status { get; set; } = ApprovedSnapshotStatuses.Creating;
    public string PayloadJson { get; set; } = string.Empty;
    public string SnapshotHash { get; set; } = string.Empty;
    public string ExecutionKey { get; set; } = string.Empty;
    public bool IsCurrent { get; set; }
    public string? ActiveCurrentKey { get; set; }
    public Guid ApprovedByUserId { get; set; }
    public DateTimeOffset ApprovedAt { get; set; }
    public Guid? SupersedesSnapshotId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}