using Intake.Domain.Extraction;

namespace Intake.Application.Extraction;

public interface IArtifactExtractionRepository
{
    Task<ExtractionProfileDefinition?> FindProfileAsync(
        string code,
        int? version,
        CancellationToken cancellationToken);

    Task<ExtractionSchemaDefinition?> FindSchemaAsync(
        string code,
        int version,
        string classificationCode,
        CancellationToken cancellationToken);

    Task<ExtractionPromptDefinition?> FindPromptAsync(
        string code,
        int version,
        string classificationCode,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ExtractionProfileDefinition>> ListProfilesAsync(
        CancellationToken cancellationToken);

    Task<ArtifactExtraction?> FindCurrentAsync(
        Guid tenantId,
        Guid artifactId,
        Guid classificationId,
        CancellationToken cancellationToken);

    Task<ArtifactExtraction?> FindByExecutionKeyAsync(
        Guid tenantId,
        string executionKey,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ArtifactExtraction>> ListHistoryAsync(
        Guid tenantId,
        Guid artifactId,
        CancellationToken cancellationToken);

    Task<bool> TryClaimAsync(
        Guid tenantId,
        Guid extractionId,
        bool retryFailed,
        CancellationToken cancellationToken);

    Task FinalizeCurrentAsync(
        Guid tenantId,
        Guid artifactId,
        ArtifactExtraction extraction,
        IReadOnlyList<ArtifactExtractedFact> facts,
        CancellationToken cancellationToken);

    Task<bool> TryAddExtractionAsync(
        ArtifactExtraction extraction,
        CancellationToken cancellationToken);

    Task SaveAsync(CancellationToken cancellationToken);
}