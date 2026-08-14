using Intake.Domain.Operations;

namespace Intake.Application.Operations;

public sealed class IntakeRecoveryOptions
{
    public const string SectionName = "Intake:Recovery";
    public bool Enabled { get; set; } = true;
    public int ScanIntervalSeconds { get; set; } = 30;
    public int ProcessingStaleAfterMinutes { get; set; } = 10;
    public int MaxItemsPerScan { get; set; } = 100;
    public int MaxRecoveryAttempts { get; set; } = 5;
    public int MaxConcurrentRecoveries { get; set; } = 4;
    public int RetryBackoffBaseSeconds { get; set; } = 30;
    public int RetryBackoffMaxSeconds { get; set; } = 1800;
    public Guid RecoveryActorId { get; set; } = new("00000000-0000-0000-0000-000000000001");
}

public sealed record RecoveryCandidate(
    Guid TenantId,
    string Stage,
    Guid ObjectId,
    string DomainStatus,
    bool Retryable,
    string? FailureCode,
    string? SafeMessage,
    DateTimeOffset UpdatedAt,
    DateTimeOffset CreatedAt,
    string? CorrelationId = null);

public sealed record RecoveryWorkItemResponse(
    Guid Id,
    Guid TenantId,
    string Stage,
    Guid ObjectId,
    string DomainStatus,
    string RecoveryStatus,
    string? FailureCode,
    string? FailureCategory,
    string? SafeMessage,
    bool Retryable,
    int AttemptCount,
    DateTimeOffset? LastAttemptAt,
    DateTimeOffset? NextRetryAt,
    DateTimeOffset? StaleSince,
    DateTimeOffset CreatedAt,
    string? CorrelationId);

public sealed record RecoveryFailure(
    string? Code,
    string Category,
    string Message,
    bool Retryable);

public sealed record RecoveryHandlerResult(
    bool Recovered,
    bool Retryable,
    string? FailureCode,
    string SafeMessage,
    string FailureCategory);

public sealed record RecoveryQuery(
    string? Stage = null,
    string? Status = null,
    string? FailureCategory = null,
    bool? Retryable = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    int Page = 1,
    int PageSize = 50);

public sealed record OperationsSummaryResponse(
    long Received,
    long Processing,
    long WaitingReview,
    long Completed,
    long Failed,
    long Retryable,
    long Stale,
    long Recovered,
    long AdapterSuccess,
    long AdapterFailure,
    long DocumentAssociationSuccess,
    long DocumentAssociationFailure);

public sealed record StageCountResponse(string Stage, long Count);

public sealed record FailureAggregateResponse(
    string Stage,
    string Category,
    string Code,
    bool Retryable,
    long Count);

public sealed record RecoveryAnalyticsResponse(
    long Stale,
    long Recovered,
    long Failed,
    long Exhausted,
    double AverageAttempts,
    IReadOnlyList<RecoveryWorkItemResponse> RecentActivity);

public sealed record RecoveryWorkerHealthResponse(
    bool Enabled,
    DateTimeOffset? LastScanAt,
    DateTimeOffset? LastSuccessfulScanAt,
    long ItemsScanned,
    long StaleItemsFound,
    long RecoveredCount,
    long FailedCount,
    string? LastFailureCode);

public sealed record RecoveryAuditEntry(
    string Action,
    Guid TenantId,
    string Stage,
    Guid ObjectId,
    Guid ActorUserId,
    string PreviousStatus,
    string NewStatus,
    string? FailureCode,
    string? CorrelationId);

public interface IRecoveryAuditSink
{
    Task RecordAsync(RecoveryAuditEntry entry, CancellationToken cancellationToken);
}

public interface IIntakeRecoveryHandler
{
    string Stage { get; }
    Task<RecoveryHandlerResult> RecoverAsync(
        IntakeRecoveryWorkItem item,
        string? correlationId,
        CancellationToken cancellationToken);
}

public interface IIntakeRecoveryRegistry
{
    IIntakeRecoveryHandler GetRequired(string stage);
}

public interface IIntakeRecoveryRepository
{
    Task<IReadOnlyList<RecoveryCandidate>> DiscoverAsync(
        DateTimeOffset staleBefore,
        int maxItems,
        CancellationToken cancellationToken);
    Task<IntakeRecoveryWorkItem> EnsureDiscoveredAsync(
        RecoveryCandidate candidate,
        CancellationToken cancellationToken);
    Task<IntakeRecoveryWorkItem?> FindAsync(
        Guid tenantId,
        Guid workItemId,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<IntakeRecoveryWorkItem>> ListEligibleAsync(
        Guid? tenantId,
        DateTimeOffset now,
        int maxItems,
        CancellationToken cancellationToken);
    Task<IntakeRecoveryWorkItem?> TryClaimAsync(
        Guid tenantId,
        Guid workItemId,
        DateTimeOffset now,
        int maxAttempts,
        bool manual,
        CancellationToken cancellationToken);
    Task CompleteAsync(
        IntakeRecoveryWorkItem item,
        RecoveryHandlerResult result,
        DateTimeOffset? nextRetryAt,
        CancellationToken cancellationToken);
    Task<bool> CancelAsync(
        Guid tenantId,
        Guid workItemId,
        Guid actorUserId,
        CancellationToken cancellationToken);
    Task<bool> MarkCreatingSnapshotFailedAsync(
        Guid tenantId,
        Guid snapshotId,
        CancellationToken cancellationToken);
    Task<Guid?> FindExecutionIdByDocumentAssociationItemAsync(
        Guid tenantId,
        Guid itemId,
        CancellationToken cancellationToken);
    Task<Guid?> FindAdapterSnapshotIdAsync(
        Guid tenantId,
        Guid executionId,
        CancellationToken cancellationToken);
    Task<Guid?> FindDocumentAssociationSnapshotIdAsync(
        Guid tenantId,
        Guid executionId,
        CancellationToken cancellationToken);
    Task<(IReadOnlyList<IntakeRecoveryWorkItem> Items, long TotalCount)> ListAsync(
        Guid tenantId,
        RecoveryQuery query,
        CancellationToken cancellationToken);
    Task<OperationsSummaryResponse> GetSummaryAsync(
        Guid tenantId,
        DateTimeOffset from,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<StageCountResponse>> GetStageFunnelAsync(
        Guid tenantId,
        DateTimeOffset from,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<FailureAggregateResponse>> GetFailuresAsync(
        Guid tenantId,
        DateTimeOffset from,
        CancellationToken cancellationToken);
    Task<RecoveryAnalyticsResponse> GetRecoveryAnalyticsAsync(
        Guid tenantId,
        DateTimeOffset from,
        CancellationToken cancellationToken);
}

public interface IIntakeRecoveryService
{
    Task<RecoveryWorkerHealthResponse> GetWorkerHealthAsync();
    Task<RecoveryWorkerHealthResponse> RunScanAsync(CancellationToken cancellationToken);
    Task<RecoveryWorkItemResponse?> GetAsync(
        Guid tenantId,
        Guid workItemId,
        CancellationToken cancellationToken);
    Task<(IReadOnlyList<RecoveryWorkItemResponse> Items, long TotalCount)> ListAsync(
        Guid tenantId,
        RecoveryQuery query,
        CancellationToken cancellationToken);
    Task<RecoveryWorkItemResponse?> RecoverAsync(
        Guid tenantId,
        Guid workItemId,
        Guid actorUserId,
        string? correlationId,
        CancellationToken cancellationToken);
    Task<bool> CancelAsync(
        Guid tenantId,
        Guid workItemId,
        Guid actorUserId,
        string? correlationId,
        CancellationToken cancellationToken);
    Task<OperationsSummaryResponse> GetSummaryAsync(
        Guid tenantId,
        DateTimeOffset from,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<StageCountResponse>> GetStageFunnelAsync(
        Guid tenantId,
        DateTimeOffset from,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<FailureAggregateResponse>> GetFailuresAsync(
        Guid tenantId,
        DateTimeOffset from,
        CancellationToken cancellationToken);
    Task<RecoveryAnalyticsResponse> GetRecoveryAnalyticsAsync(
        Guid tenantId,
        DateTimeOffset from,
        CancellationToken cancellationToken);
}