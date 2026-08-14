using Intake.Domain.Artifacts;
using Intake.Domain.Classification;

namespace Intake.Application.Classification;

public interface IClassificationRepository
{
    Task<TenantAiPolicy?> FindPolicyAsync(Guid tenantId, CancellationToken cancellationToken);
    Task SavePolicyAsync(TenantAiPolicy policy, CancellationToken cancellationToken);

    Task<ClassificationProfileDefinition?> FindProfileAsync(
        string code,
        int? version,
        CancellationToken cancellationToken);

    Task<ClassificationTaxonomyDefinition?> FindTaxonomyAsync(
        string code,
        int version,
        CancellationToken cancellationToken);

    Task<ClassificationPromptDefinition?> FindPromptAsync(
        string code,
        int version,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ClassificationProfileDefinition>> ListProfilesAsync(
        CancellationToken cancellationToken);

    Task<IntakeArtifact?> FindArtifactAsync(
        Guid tenantId,
        Guid artifactId,
        CancellationToken cancellationToken);

    Task<ArtifactClassification?> FindCurrentAsync(
        Guid tenantId,
        Guid artifactId,
        CancellationToken cancellationToken);

    Task<ArtifactClassification?> FindByExecutionKeyAsync(
        Guid tenantId,
        string executionKey,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ArtifactClassification>> ListHistoryAsync(
        Guid tenantId,
        Guid artifactId,
        CancellationToken cancellationToken);

    Task<bool> TryClaimAsync(
        Guid tenantId,
        Guid classificationId,
        bool retryFailed,
        CancellationToken cancellationToken);

    Task ClearCurrentAsync(
        Guid tenantId,
        Guid artifactId,
        Guid replacementClassificationId,
        CancellationToken cancellationToken);

    Task FinalizeCurrentAsync(
        Guid tenantId,
        Guid artifactId,
        ArtifactClassification classification,
        CancellationToken cancellationToken);

    Task<bool> TryAddClassificationAsync(
        ArtifactClassification classification,
        CancellationToken cancellationToken);

    Task SaveAsync(CancellationToken cancellationToken);
}