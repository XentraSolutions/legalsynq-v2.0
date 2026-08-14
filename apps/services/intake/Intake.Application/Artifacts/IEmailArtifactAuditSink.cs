namespace Intake.Application.Artifacts;

public sealed record EmailArtifactAuditEntry(
    Guid TenantId,
    Guid EmailId,
    Guid? ActorId,
    string Status,
    int ArtifactCount,
    int CompletedCount,
    int FailedCount,
    int SkippedCount,
    string? CorrelationId);

public interface IEmailArtifactAuditSink
{
    Task RecordAsync(
        EmailArtifactAuditEntry entry,
        CancellationToken cancellationToken);
}