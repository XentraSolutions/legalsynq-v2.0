using Intake.Contracts.Review;
using Intake.Contracts.Snapshot;
using Intake.Domain.Snapshot;

namespace Intake.Application.Snapshot;

public sealed record ReviewedIntakeSnapshotSource(
    IntakeReviewWorkspaceResponse Workspace,
    ReviewedIntakeProjectionResponse Projection);

public interface IReviewedIntakeProjectionService
{
    Task<ReviewedIntakeSnapshotSource> GetAsync(
        Guid tenantId,
        Guid reviewId,
        CancellationToken cancellationToken);
}

public interface IApprovedSnapshotRepository
{
    Task<ApprovedSnapshotSchemaDefinition?> FindSchemaAsync(
        string code,
        int version,
        CancellationToken cancellationToken);

    Task<ApprovedIntakeSnapshot?> FindAsync(
        Guid tenantId,
        Guid snapshotId,
        CancellationToken cancellationToken);

    Task<ApprovedIntakeSnapshot?> FindByExecutionKeyAsync(
        Guid tenantId,
        string executionKey,
        CancellationToken cancellationToken);

    Task<ApprovedIntakeSnapshot?> FindCurrentAsync(
        Guid tenantId,
        Guid artifactId,
        CancellationToken cancellationToken);

    Task<(IReadOnlyList<ApprovedIntakeSnapshot> Items, long TotalCount)> ListByArtifactAsync(
        Guid tenantId,
        Guid artifactId,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<ApprovedIntakeSnapshot> PersistReadyAsync(
        ApprovedIntakeSnapshot snapshot,
        CancellationToken cancellationToken);
}

public interface IApprovedIntakeSnapshotService
{
    Task<ApprovedSnapshotResponse> CreateAsync(
        Guid tenantId,
        Guid reviewId,
        Guid actorUserId,
        string? correlationId,
        CancellationToken cancellationToken);

    Task<ApprovedSnapshotResponse?> GetAsync(
        Guid tenantId,
        Guid snapshotId,
        CancellationToken cancellationToken);

    Task<ApprovedSnapshotSummaryResponse> GetCurrentAsync(
        Guid tenantId,
        Guid artifactId,
        CancellationToken cancellationToken);

    Task<(IReadOnlyList<ApprovedSnapshotSummaryResponse> Items, long TotalCount)> ListByArtifactAsync(
        Guid tenantId,
        Guid artifactId,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}

public interface IIntakeDestinationAdapter
{
    AdapterDescriptor Descriptor { get; }

    AdapterValidationResult Validate(
        ApprovedIntakeSnapshotV1 snapshot,
        IntakeAdapterRequestContext context);

    Task<AdapterExecutionResult> ExecuteAsync(
        ApprovedIntakeSnapshotV1 snapshot,
        IntakeAdapterRequestContext context,
        CancellationToken cancellationToken);
}

public sealed record AdapterDescriptor(
    string AdapterCode,
    string AdapterVersion,
    IReadOnlyList<string> SupportedSnapshotSchemas,
    IReadOnlyList<string> SupportedProcessingProfiles,
    bool SupportsDryRun,
    bool SupportsRetry);

public sealed record IntakeAdapterRequestContext(
    Guid TenantId,
    Guid SnapshotId,
    string CorrelationId,
    string IdempotencyKey,
    Guid RequestedByUserId,
    bool DryRun);

public sealed record AdapterValidationResult(
    bool IsValid,
    string? FailureCode,
    string? FailureMessage);

public sealed record AdapterExternalReference(
    string ReferenceType,
    string ReferenceId);

public sealed record AdapterExecutionResult(
    bool Success,
    bool Retryable,
    string Status,
    string? FailureCode,
    string? FailureMessage,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<AdapterExternalReference> ExternalReferences);

public interface IIntakeDestinationAdapterRegistry
{
    IReadOnlyList<AdapterDescriptor> List();
    IIntakeDestinationAdapter GetRequired(string adapterCode);
}

public interface IAdapterExecutionRepository
{
    Task<IntakeAdapterExecution?> FindAsync(
        Guid tenantId,
        Guid executionId,
        CancellationToken cancellationToken);

    Task<IntakeAdapterExecution?> FindByExecutionKeyAsync(
        Guid tenantId,
        string executionKey,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<IntakeAdapterExecution>> ListBySnapshotAsync(
        Guid tenantId,
        Guid snapshotId,
        CancellationToken cancellationToken);

    Task<AdapterExecutionClaim> TryClaimAsync(
        Guid tenantId,
        Guid snapshotId,
        string adapterCode,
        string adapterVersion,
        string executionKey,
        string idempotencyKey,
        Guid requestedByUserId,
        bool retry,
        int maxAttempts,
        CancellationToken cancellationToken);

    Task FinalizeAsync(
        Guid tenantId,
        Guid executionId,
        string claimToken,
        int attemptNumber,
        string status,
        string? failureCode,
        string? failureMessage,
        string resultJson,
        IReadOnlyList<AdapterExternalReference> externalReferences,
        CancellationToken cancellationToken);
}

public sealed record AdapterExecutionClaim(
    IntakeAdapterExecution Execution,
    bool Claimed);

public sealed class SnapshotVersionConflictException : Exception
{
    public SnapshotVersionConflictException()
        : base("A newer approved snapshot version was created concurrently.")
    {
    }
}

public interface IIntakeAdapterExecutionService
{
    IReadOnlyList<AdapterDescriptor> ListAdapters();

    Task<AdapterExecutionResponse> ExecuteAsync(
        Guid tenantId,
        Guid snapshotId,
        string adapterCode,
        Guid actorUserId,
        bool dryRun,
        string? correlationId,
        CancellationToken cancellationToken);

    Task<AdapterExecutionResponse> RetryAsync(
        Guid tenantId,
        Guid snapshotId,
        Guid executionId,
        Guid actorUserId,
        string? correlationId,
        CancellationToken cancellationToken);

    Task<AdapterExecutionResponse?> GetAsync(
        Guid tenantId,
        Guid snapshotId,
        Guid executionId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AdapterExecutionResponse>> ListAsync(
        Guid tenantId,
        Guid snapshotId,
        CancellationToken cancellationToken);
}

public static class SnapshotJson
{
    public const string EmptyObject = "{}";
}