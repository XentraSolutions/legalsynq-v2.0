using Intake.Contracts.Classification;

namespace Intake.Application.Classification;

public interface IClassificationService
{
    Task<TenantAiPolicyResponse?> GetPolicyAsync(
        Guid tenantId,
        CancellationToken cancellationToken);

    Task<TenantAiPolicyResponse> UpsertPolicyAsync(
        Guid tenantId,
        UpsertTenantAiPolicyRequest request,
        Guid? actorId,
        string? correlationId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ClassificationProfileResponse>> ListProfilesAsync(
        CancellationToken cancellationToken);

    Task<ArtifactClassificationResponse> ClassifyAsync(
        Guid tenantId,
        Guid artifactId,
        string? processingProfileCode,
        Guid? actorId,
        string? correlationId,
        bool retry,
        CancellationToken cancellationToken);

    Task<ArtifactClassificationResponse?> GetCurrentAsync(
        Guid tenantId,
        Guid artifactId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ArtifactClassificationResponse>> GetHistoryAsync(
        Guid tenantId,
        Guid artifactId,
        CancellationToken cancellationToken);
}