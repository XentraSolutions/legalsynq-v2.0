namespace Intake.Application.Manual;

public interface IManualIntakeService
{
    Task<ManualIntakeSubmissionResponse> CreateAndSubmitAsync(
        Guid tenantId,
        Guid? orgId,
        Guid? actorId,
        string? correlationId,
        CreateManualIntakeRequest request,
        CancellationToken cancellationToken);

    Task<ManualIntakeListResponse> ListAsync(
        Guid tenantId,
        ManualIntakeListQuery query,
        CancellationToken cancellationToken);

    Task<ManualIntakeSubmissionResponse?> GetAsync(
        Guid tenantId,
        Guid submissionId,
        CancellationToken cancellationToken);

    Task<ManualIntakeSubmissionResponse> RetryArtifactAsync(
        Guid tenantId,
        Guid submissionId,
        Guid artifactId,
        ManualIntakeFile file,
        Guid? actorId,
        string? correlationId,
        CancellationToken cancellationToken);

    Task<ManualIntakeSubmissionResponse> CancelAsync(
        Guid tenantId,
        Guid submissionId,
        int expectedVersion,
        Guid? actorId,
        string? correlationId,
        CancellationToken cancellationToken);

    Task<ManualIntakeAnalyticsResponse> GetAnalyticsAsync(
        Guid tenantId,
        CancellationToken cancellationToken);
}