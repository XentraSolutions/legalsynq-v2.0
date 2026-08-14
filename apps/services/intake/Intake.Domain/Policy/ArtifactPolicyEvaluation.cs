namespace Intake.Domain.Policy;

public sealed class ArtifactPolicyEvaluation
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ArtifactId { get; set; }
    public Guid? ClassificationId { get; set; }
    public Guid? ArtifactExtractionId { get; set; }
    public Guid? ArtifactNormalizationId { get; set; }
    public Guid? ArtifactMatchRunId { get; set; }
    public string PolicyProfileCode { get; set; } = string.Empty;
    public int PolicyProfileVersion { get; set; }
    public string Status { get; set; } = PolicyEvaluationStatuses.Processing;
    public string Disposition { get; set; } = PolicyDispositionCodes.ReviewRequired;
    public decimal OverallConfidence { get; set; }
    public string ReviewPriority { get; set; } = PolicyReviewPriorities.Normal;
    public string ExecutionKey { get; set; } = string.Empty;
    public bool IsCurrent { get; set; }
    public string? CurrentResultMarker { get; set; }
    public string? FailureCode { get; set; }
    public string? FailureMessage { get; set; }
    public DateTimeOffset RequestedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<ArtifactPolicyFinding> Findings { get; set; } = [];
}