using Intake.Domain.Normalization;

namespace Intake.Application.Normalization;

public interface IArtifactNormalizationRepository
{
    Task<NormalizationProfileDefinition?> FindProfileAsync(
        string code,
        int? version,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<NormalizationProfileDefinition>> ListProfilesAsync(
        CancellationToken cancellationToken);

    Task<ArtifactNormalization?> FindCurrentAsync(
        Guid tenantId,
        Guid artifactId,
        Guid artifactExtractionId,
        CancellationToken cancellationToken);

    Task<ArtifactNormalization?> FindByExecutionKeyAsync(
        Guid tenantId,
        string executionKey,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ArtifactNormalization>> ListHistoryAsync(
        Guid tenantId,
        Guid artifactId,
        CancellationToken cancellationToken);

    Task<bool> TryAddNormalizationAsync(
        ArtifactNormalization normalization,
        CancellationToken cancellationToken);

    Task FinalizeCurrentAsync(
        Guid tenantId,
        Guid artifactId,
        ArtifactNormalization normalization,
        IReadOnlyList<ArtifactNormalizedFact> facts,
        CancellationToken cancellationToken);

    Task SaveAsync(CancellationToken cancellationToken);
}