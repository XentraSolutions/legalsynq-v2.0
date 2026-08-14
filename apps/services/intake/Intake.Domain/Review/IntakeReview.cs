namespace Intake.Domain.Review;

public sealed class IntakeReview
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ArtifactId { get; set; }
    public Guid? ClassificationId { get; set; }
    public Guid? ArtifactExtractionId { get; set; }
    public Guid? ArtifactNormalizationId { get; set; }
    public Guid? ArtifactMatchRunId { get; set; }
    public Guid ArtifactPolicyEvaluationId { get; set; }

    public string Status { get; set; } = IntakeReviewStatuses.Pending;
    public string Priority { get; set; } = IntakeReviewPriorities.Normal;
    public string ReviewOutcome { get; set; } = string.Empty;
    public string B11Disposition { get; set; } = string.Empty;
    public string ClassificationCode { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;

    public Guid? AssignedToUserId { get; set; }
    public DateTimeOffset? AssignedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public Guid? CompletedByUserId { get; set; }
    public string? CompletionReasonCode { get; set; }
    public string? CompletionComment { get; set; }

    public int RevisionNumber { get; set; } = 1;
    public Guid? SupersedesReviewId { get; set; }
    public string? ActiveContextKey { get; set; }
    public int Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<IntakeReviewCorrection> Corrections { get; set; } = [];
    public ICollection<IntakeReviewMatchDecision> MatchDecisions { get; set; } = [];
    public ICollection<IntakeReviewDuplicateDecision> DuplicateDecisions { get; set; } = [];
    public ICollection<IntakeReviewFindingDecision> FindingDecisions { get; set; } = [];
    public ICollection<IntakeReviewActivity> Activities { get; set; } = [];
}