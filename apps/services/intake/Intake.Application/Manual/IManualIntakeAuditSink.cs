namespace Intake.Application.Manual;

public sealed record ManualIntakeAuditEntry(
    Guid TenantId,
    Guid SubmissionId,
    Guid? ActorId,
    string Action,
    string Status,
    int ArtifactCount,
    int CompletedCount,
    int FailedCount,
    int SkippedCount,
    string? CorrelationId);

public interface IManualIntakeAuditSink
{
    Task RecordAsync(
        ManualIntakeAuditEntry entry,
        CancellationToken cancellationToken);
}