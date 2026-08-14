namespace Intake.Application.Normalization;

public sealed record NormalizationAuditEntry(
    Guid TenantId,
    Guid ArtifactId,
    Guid ArtifactExtractionId,
    Guid NormalizationId,
    string Action,
    string Status,
    string ProfileCode,
    int FactCount,
    int NormalizedCount,
    int InvalidCount,
    int AmbiguousCount,
    string? CorrelationId,
    Guid? ActorId);

public interface INormalizationAuditSink
{
    Task RecordAsync(
        NormalizationAuditEntry entry,
        CancellationToken cancellationToken);
}