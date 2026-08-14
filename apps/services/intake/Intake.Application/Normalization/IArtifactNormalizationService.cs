using Intake.Contracts.Normalization;

namespace Intake.Application.Normalization;

public interface IArtifactNormalizationService
{
    Task<IReadOnlyList<NormalizationProfileResponse>> ListProfilesAsync(
        CancellationToken cancellationToken);

    Task<ArtifactNormalizationResponse?> GetCurrentAsync(
        Guid tenantId,
        Guid artifactId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ArtifactNormalizationResponse>> GetHistoryAsync(
        Guid tenantId,
        Guid artifactId,
        CancellationToken cancellationToken);

    Task<ArtifactNormalizationResponse> NormalizeAsync(
        Guid tenantId,
        Guid artifactId,
        string? processingProfileCode,
        Guid? actorId,
        string? correlationId,
        CancellationToken cancellationToken);
}