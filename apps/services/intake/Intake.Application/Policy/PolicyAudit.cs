namespace Intake.Application.Policy;

public sealed record PolicyAuditEntry(
    string Action,
    Guid TenantId,
    Guid ArtifactId,
    Guid EvaluationId,
    string PolicyProfileCode,
    string Status,
    string Disposition,
    string ReviewPriority,
    int FindingCount,
    string? FailureCode,
    string? CorrelationId,
    Guid? ActorId);

public interface IPolicyAuditSink
{
    Task RecordAsync(
        PolicyAuditEntry entry,
        CancellationToken cancellationToken);
}