namespace Intake.Application.Review;

public sealed record ReviewAuditEntry(
    string Action,
    Guid TenantId,
    Guid ReviewId,
    Guid ArtifactId,
    Guid? ActorUserId,
    string? Status,
    string? DecisionCode,
    string? Outcome,
    string? FactCode,
    string? EntityType,
    string? CorrelationId);

public interface IReviewAuditSink
{
    Task RecordAsync(ReviewAuditEntry entry, CancellationToken cancellationToken);
}