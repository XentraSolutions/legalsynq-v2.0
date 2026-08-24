namespace Intake.Application.Snapshot;

public sealed record SnapshotAuditEntry(
    string Action,
    Guid TenantId,
    Guid SnapshotId,
    Guid ArtifactId,
    Guid ReviewId,
    Guid? ActorUserId,
    string? Status,
    string? AdapterCode,
    Guid? ExecutionId,
    string? CorrelationId);

public interface ISnapshotAuditSink
{
    Task RecordAsync(SnapshotAuditEntry entry, CancellationToken cancellationToken);
}