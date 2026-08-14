using Intake.Contracts.Matching;

namespace Intake.Application.Matching;

public interface IArtifactMatchingService
{
    Task<IReadOnlyList<MatchingProfileResponse>> ListProfilesAsync(
        CancellationToken cancellationToken);

    Task<ArtifactMatchResponse?> GetCurrentAsync(
        Guid tenantId,
        Guid artifactId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ArtifactMatchResponse>> GetHistoryAsync(
        Guid tenantId,
        Guid artifactId,
        CancellationToken cancellationToken);

    Task<ArtifactMatchResponse> MatchAsync(
        Guid tenantId,
        Guid artifactId,
        string? processingProfileCode,
        Guid? actorId,
        string? correlationId,
        CancellationToken cancellationToken);
}