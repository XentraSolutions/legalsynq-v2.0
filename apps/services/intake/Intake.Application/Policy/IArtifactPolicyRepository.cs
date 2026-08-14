using Intake.Domain.Policy;

namespace Intake.Application.Policy;

public interface IArtifactPolicyRepository
{
    Task<PolicyProfileDefinition?> FindProfileAsync(
        string code,
        int? version,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<PolicyProfileDefinition>> ListProfilesAsync(
        CancellationToken cancellationToken);

    Task<ArtifactPolicyEvaluation?> FindCurrentAsync(
        Guid tenantId,
        Guid artifactId,
        CancellationToken cancellationToken);

    Task<ArtifactPolicyEvaluation?> FindByExecutionKeyAsync(
        Guid tenantId,
        string executionKey,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ArtifactPolicyEvaluation>> ListHistoryAsync(
        Guid tenantId,
        Guid artifactId,
        CancellationToken cancellationToken);

    Task<bool> TryAddEvaluationAsync(
        ArtifactPolicyEvaluation evaluation,
        CancellationToken cancellationToken);

    Task FinalizeCurrentAsync(
        Guid tenantId,
        Guid artifactId,
        ArtifactPolicyEvaluation evaluation,
        IReadOnlyList<ArtifactPolicyFinding> findings,
        CancellationToken cancellationToken);

    Task SaveAsync(CancellationToken cancellationToken);
}