namespace Intake.Application.Classification;

public sealed record ClassificationAuditEntry(
    Guid TenantId,
    Guid ArtifactId,
    Guid ClassificationId,
    string Action,
    string Status,
    string? FailureCode,
    string? CorrelationId,
    Guid? ActorId);

public interface IClassificationAuditSink
{
    Task RecordAsync(
        ClassificationAuditEntry entry,
        CancellationToken cancellationToken);
}