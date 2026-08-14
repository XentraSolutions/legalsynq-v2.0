namespace Intake.Application.Extraction;

public sealed record ExtractionAuditEntry(
    Guid TenantId,
    Guid ArtifactId,
    Guid ExtractionId,
    string Action,
    string Status,
    string? FailureCode,
    string? ClassificationCode,
    string? CorrelationId,
    Guid? ActorId);

public interface IExtractionAuditSink
{
    Task RecordAsync(
        ExtractionAuditEntry entry,
        CancellationToken cancellationToken);
}