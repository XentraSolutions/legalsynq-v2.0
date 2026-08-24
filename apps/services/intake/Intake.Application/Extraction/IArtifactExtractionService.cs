using Intake.Contracts.Extraction;

namespace Intake.Application.Extraction;

public interface IArtifactExtractionService
{
    Task<IReadOnlyList<ExtractionProfileResponse>> ListProfilesAsync(
        CancellationToken cancellationToken);

    Task<ArtifactExtractionResponse?> GetCurrentAsync(
        Guid tenantId,
        Guid artifactId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ArtifactExtractionResponse>> GetHistoryAsync(
        Guid tenantId,
        Guid artifactId,
        CancellationToken cancellationToken);

    Task<ArtifactExtractionResponse> ExtractAsync(
        Guid tenantId,
        Guid artifactId,
        string? processingProfileCode,
        Guid? actorId,
        string? correlationId,
        bool retry,
        CancellationToken cancellationToken);
}