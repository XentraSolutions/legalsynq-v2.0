namespace Intake.Domain.Review;

public sealed class IntakeReviewMatchDecision
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid IntakeReviewId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public Guid? ArtifactEntityMatchId { get; set; }
    public Guid? CandidateEntityId { get; set; }
    public string Decision { get; set; } = string.Empty;
    public bool IsManualSelection { get; set; }
    public string ReasonCode { get; set; } = string.Empty;
    public string? Comment { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? SupersedesDecisionId { get; set; }
}