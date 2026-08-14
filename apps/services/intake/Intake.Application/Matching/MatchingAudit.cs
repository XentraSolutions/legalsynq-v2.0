namespace Intake.Application.Matching;

public sealed record MatchingAuditEntry(
    string Action,
    Guid TenantId,
    Guid ArtifactId,
    Guid ArtifactNormalizationId,
    Guid MatchRunId,
    IReadOnlyList<string> EntityTypesProcessed,
    int CandidateCount,
    int DuplicateCount,
    string Status,
    string? FailureCode,
    string? CorrelationId,
    Guid? ActorId);

public interface IMatchingAuditSink
{
    Task RecordAsync(MatchingAuditEntry entry, CancellationToken cancellationToken);
}