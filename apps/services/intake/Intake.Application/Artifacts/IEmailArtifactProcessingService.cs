namespace Intake.Application.Artifacts;

public interface IEmailArtifactProcessingService
{
    Task<EmailArtifactProcessingResponse> ProcessAsync(
        Guid tenantId,
        Guid emailId,
        Guid? actorId,
        string? correlationId,
        CancellationToken cancellationToken);

    Task<EmailArtifactProcessingResponse> RetryAsync(
        Guid tenantId,
        Guid emailId,
        Guid artifactId,
        Guid? actorId,
        string? correlationId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<IntakeArtifactResponse>> ListAsync(
        Guid tenantId,
        Guid emailId,
        CancellationToken cancellationToken);

    Task<IntakeArtifactReconciliationResponse?> ReconcileAsync(
        Guid tenantId,
        Guid emailId,
        CancellationToken cancellationToken);

    Task<IntakeArtifactAnalyticsResponse> GetAnalyticsAsync(
        Guid tenantId,
        Guid? emailId,
        CancellationToken cancellationToken);
}