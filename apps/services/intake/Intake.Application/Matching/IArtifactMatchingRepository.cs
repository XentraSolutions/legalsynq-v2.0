using Intake.Domain.Matching;

namespace Intake.Application.Matching;

public interface IArtifactMatchingRepository
{
    Task<MatchingProfileDefinition?> FindProfileAsync(
        string code,
        int? version,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<MatchingProfileDefinition>> ListProfilesAsync(
        CancellationToken cancellationToken);

    Task<ArtifactMatchRun?> FindCurrentAsync(
        Guid tenantId,
        Guid artifactId,
        Guid normalizationId,
        CancellationToken cancellationToken);

    Task<ArtifactMatchRun?> FindByExecutionKeyAsync(
        Guid tenantId,
        string executionKey,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ArtifactMatchRun>> ListHistoryAsync(
        Guid tenantId,
        Guid artifactId,
        CancellationToken cancellationToken);

    Task<ArtifactMatchRun?> FindBusinessDuplicateRunAsync(
        Guid tenantId,
        string businessKeyFingerprint,
        Guid excludedArtifactId,
        CancellationToken cancellationToken);

    Task<bool> TryAddMatchRunAsync(
        ArtifactMatchRun run,
        CancellationToken cancellationToken);

    Task FinalizeCurrentAsync(
        Guid tenantId,
        Guid artifactId,
        ArtifactMatchRun run,
        IReadOnlyList<ArtifactEntityMatch> entityMatches,
        IReadOnlyList<ArtifactMatchField> fields,
        IReadOnlyList<ArtifactDuplicateSignal> duplicateSignals,
        CancellationToken cancellationToken);

    Task SaveAsync(CancellationToken cancellationToken);
}

public static class MatchingRepositoryExtensions
{
    public static Task<IReadOnlyList<ArtifactMatchRun>> ListHistoryAsync(
        this IArtifactMatchingRepository repository,
        Guid tenantId,
        Guid artifactId,
        CancellationToken cancellationToken) =>
        repository.ListHistoryAsync(tenantId, artifactId, cancellationToken);
}