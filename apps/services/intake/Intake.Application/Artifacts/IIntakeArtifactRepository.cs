using Intake.Domain.Artifacts;

namespace Intake.Application.Artifacts;

public interface IIntakeArtifactRepository
{
    Task<IReadOnlyList<IntakeArtifact>> ListByEmailAsync(
        Guid tenantId,
        Guid emailId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<IntakeArtifact>> ListByManualSubmissionAsync(
        Guid tenantId,
        Guid submissionId,
        CancellationToken cancellationToken);

    Task<IntakeArtifact?> FindByManualKeyAsync(
        Guid tenantId,
        Guid submissionId,
        string artifactKey,
        CancellationToken cancellationToken);

    Task UpdateManualSubmissionStatusAsync(
        Guid tenantId,
        Guid submissionId,
        string status,
        string? failureMessage,
        DateTimeOffset? completedAt,
        CancellationToken cancellationToken);

    Task<IntakeArtifact?> FindAsync(
        Guid tenantId,
        Guid artifactId,
        CancellationToken cancellationToken);

    Task<IntakeArtifact?> FindByKeyAsync(
        Guid tenantId,
        Guid emailId,
        string artifactKey,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<IntakeArtifact>> ListBySha256Async(
        Guid tenantId,
        string sha256,
        Guid excludedArtifactId,
        CancellationToken cancellationToken);

    Task<IntakeArtifact> AddOrGetAsync(
        IntakeArtifact artifact,
        CancellationToken cancellationToken);

    Task<bool> TryClaimAsync(
        Guid tenantId,
        Guid artifactId,
        bool retryFailed,
        CancellationToken cancellationToken);

    Task SaveAsync(CancellationToken cancellationToken);

    Task UpdateEmailProcessingStatusAsync(
        Guid tenantId,
        Guid emailId,
        string status,
        CancellationToken cancellationToken);

    Task<IntakeArtifactAnalyticsResponse> GetAnalyticsAsync(
        Guid tenantId,
        Guid? emailId,
        CancellationToken cancellationToken);
}