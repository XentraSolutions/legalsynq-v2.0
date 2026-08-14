using Intake.Contracts.Policy;

namespace Intake.Application.Policy;

public interface IArtifactPolicyService
{
    Task<IReadOnlyList<PolicyProfileResponse>> ListProfilesAsync(
        CancellationToken cancellationToken);

    Task<ArtifactPolicyResponse?> GetCurrentAsync(
        Guid tenantId,
        Guid artifactId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ArtifactPolicyResponse>> GetHistoryAsync(
        Guid tenantId,
        Guid artifactId,
        CancellationToken cancellationToken);

    Task<ArtifactPolicyResponse> EvaluateAsync(
        Guid tenantId,
        Guid artifactId,
        string? processingProfileCode,
        Guid? actorId,
        string? correlationId,
        bool retry,
        CancellationToken cancellationToken);
}