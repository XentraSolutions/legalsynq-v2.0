namespace Intake.Domain.Review;

public sealed class IntakeReviewActivity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid IntakeReviewId { get; set; }
    public string ActivityType { get; set; } = string.Empty;
    public Guid? ActorUserId { get; set; }
    public string SafeMetadataJson { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; }
}