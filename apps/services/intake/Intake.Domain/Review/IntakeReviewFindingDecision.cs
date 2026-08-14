namespace Intake.Domain.Review;

public sealed class IntakeReviewFindingDecision
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid IntakeReviewId { get; set; }
    public Guid ArtifactPolicyFindingId { get; set; }
    public string Decision { get; set; } = string.Empty;
    public string ReasonCode { get; set; } = string.Empty;
    public string? Comment { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? SupersedesDecisionId { get; set; }
}