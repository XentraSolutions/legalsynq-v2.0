using Intake.Contracts.Snapshot;
using Intake.Domain.Snapshot;

namespace Intake.Application.Snapshot;

public static class DocumentAssociationPolicyCodes
{
    public const string V1 = "LIEN_INTAKE_DOCUMENT_ASSOCIATION_V1";
}

public sealed record DocumentAssociationItemResponse(
    Guid ItemId,
    Guid ArtifactId,
    Guid? DocumentId,
    string DocumentReference,
    string DocumentRole,
    string TargetType,
    Guid TargetId,
    string ItemKey,
    bool Required,
    string Status,
    int AttemptNumber,
    string? FailureCode,
    string? FailureMessage,
    string? DestinationReference);

public sealed record DocumentAssociationExecutionResponse(
    Guid ExecutionId,
    Guid SnapshotId,
    string PolicyCode,
    int PolicyVersion,
    string ExecutionKey,
    string IdempotencyKey,
    string Status,
    int AttemptNumber,
    Guid RequestedByUserId,
    DateTimeOffset RequestedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? FailureCode,
    string? FailureMessage,
    IReadOnlyList<DocumentAssociationItemResponse> Items);

public sealed record DocumentAssociationTarget(
    string TargetType,
    Guid TargetId,
    string Role,
    Guid? RelatedCaseId = null);

public sealed record DocumentAssociationPlanItem(
    ApprovedSnapshotDocument Document,
    DocumentAssociationTarget Target,
    bool Required);

public sealed record DocumentAssociationValidation(
    bool IsValid,
    string? FailureCode,
    string? FailureMessage,
    bool Retryable = false);

public sealed record DocumentMetadataResult(
    bool Found,
    Guid DocumentId,
    Guid TenantId,
    string Status,
    string MimeType,
    string? Sha256,
    bool IsDeleted);

public sealed record DocumentAssociationCallResult(
    bool Success,
    bool Retryable,
    int StatusCode,
    string? DestinationReference,
    string? FailureCode,
    string? FailureMessage);

public interface IDocumentAssociationPolicy
{
    IReadOnlyList<DocumentAssociationPlanItem> BuildPlan(ApprovedIntakeSnapshotV1 snapshot);
    int Version { get; }
}

public interface IDocumentAssociationDestinationClient
{
    Task<DocumentAssociationCallResult> AssociateAsync(
        Guid tenantId,
        Guid actingUserId,
        string idempotencyKey,
        string correlationId,
        string targetType,
        Guid targetId,
        Guid? relatedCaseId,
        Guid documentId,
        string documentRole,
        string documentReference,
        CancellationToken cancellationToken);
}

public interface IDocumentAssociationExecutionService
{
    Task<DocumentAssociationExecutionResponse> ExecuteAsync(
        Guid tenantId,
        Guid snapshotId,
        Guid actorUserId,
        string? correlationId,
        CancellationToken cancellationToken);

    Task<DocumentAssociationExecutionResponse> RetryAsync(
        Guid tenantId,
        Guid snapshotId,
        Guid executionId,
        Guid actorUserId,
        string? correlationId,
        CancellationToken cancellationToken);

    Task<DocumentAssociationExecutionResponse?> GetAsync(
        Guid tenantId,
        Guid snapshotId,
        Guid executionId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DocumentAssociationExecutionResponse>> ListAsync(
        Guid tenantId,
        Guid snapshotId,
        CancellationToken cancellationToken);
}

public interface IDocumentAssociationExecutionRepository
{
    Task<DocumentAssociationExecution?> FindAsync(Guid tenantId, Guid executionId, CancellationToken ct);
    Task<IReadOnlyList<DocumentAssociationExecution>> ListAsync(Guid tenantId, Guid snapshotId, CancellationToken ct);
    Task<DocumentAssociationExecution?> FindByExecutionKeyAsync(Guid tenantId, string executionKey, CancellationToken ct);
    Task SaveAsync(DocumentAssociationExecution execution, CancellationToken ct);
}