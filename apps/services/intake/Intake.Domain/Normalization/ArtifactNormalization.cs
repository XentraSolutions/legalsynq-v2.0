namespace Intake.Domain.Normalization;

public sealed class ArtifactNormalization
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid IntakeArtifactId { get; set; }
    public Guid ArtifactExtractionId { get; set; }
    public string NormalizationProfileCode { get; set; } = string.Empty;
    public int NormalizationProfileVersion { get; set; }
    public string NormalizationVersion { get; set; } = "1";
    public string ExecutionKey { get; set; } = string.Empty;
    public string Status { get; set; } = NormalizationRunStatuses.Processing;
    public bool IsCurrent { get; set; }
    public string? CurrentResultMarker { get; set; }
    public string? FailureCode { get; set; }
    public string? FailureMessage { get; set; }
    public DateTimeOffset RequestedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<ArtifactNormalizedFact> Facts { get; set; } = [];
}