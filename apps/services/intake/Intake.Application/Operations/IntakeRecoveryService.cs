using System.Diagnostics;
using Intake.Application.Snapshot;
using Intake.Domain.Operations;
using Intake.Domain.Snapshot;
using Microsoft.Extensions.Logging;

namespace Intake.Application.Operations;

public sealed class RecoveryWorkerState(IntakeRecoveryOptions options)
{
    private readonly object gate = new();
    private DateTimeOffset? lastScanAt;
    private DateTimeOffset? lastSuccessfulScanAt;
    private long itemsScanned;
    private long staleItemsFound;
    private long recoveredCount;
    private long failedCount;
    private string? lastFailureCode;

    public RecoveryWorkerHealthResponse Snapshot() 
    {
        lock (gate)
        {
            return new(
                options.Enabled,
                lastScanAt,
                lastSuccessfulScanAt,
                itemsScanned,
                staleItemsFound,
                recoveredCount,
                failedCount,
                lastFailureCode);
        }
    }

    public void ScanStarted() { lock (gate) lastScanAt = DateTimeOffset.UtcNow; }
    public void ScanSucceeded(long scanned, long stale)
    {
        lock (gate)
        {
            lastSuccessfulScanAt = DateTimeOffset.UtcNow;
            itemsScanned += scanned;
            staleItemsFound += stale;
        }
    }
    public void Recovered() { lock (gate) recoveredCount++; }
    public void Failed(string? code)
    {
        lock (gate)
        {
            failedCount++;
            lastFailureCode = code;
        }
    }
}

public sealed class IntakeRecoveryRegistry(
    IEnumerable<IIntakeRecoveryHandler> handlers) : IIntakeRecoveryRegistry
{
    private readonly IReadOnlyDictionary<string, IIntakeRecoveryHandler> handlers =
        handlers.ToDictionary(x => x.Stage, StringComparer.OrdinalIgnoreCase);

    public IIntakeRecoveryHandler GetRequired(string stage) =>
        handlers.TryGetValue(stage, out var handler)
            ? handler
            : throw new InvalidOperationException($"No recovery handler is registered for stage '{stage}'.");
}

public sealed class IntakeRecoveryService(
    IIntakeRecoveryRepository repository,
    IIntakeRecoveryRegistry registry,
    IntakeRecoveryOptions options,
    RecoveryWorkerState workerState,
    IntakeMetrics metrics,
    IRecoveryAuditSink auditSink,
    ILogger<IntakeRecoveryService> logger) : IIntakeRecoveryService
{
    public Task<RecoveryWorkerHealthResponse> GetWorkerHealthAsync() =>
        Task.FromResult(workerState.Snapshot());

    public async Task<RecoveryWorkerHealthResponse> RunScanAsync(
        CancellationToken cancellationToken)
    {
        if (!options.Enabled)
            return workerState.Snapshot();

        workerState.ScanStarted();
        var staleBefore = DateTimeOffset.UtcNow.AddMinutes(
            -Math.Clamp(options.ProcessingStaleAfterMinutes, 1, 1440));
        try
        {
            var candidates = await repository.DiscoverAsync(
                staleBefore,
                Math.Clamp(options.MaxItemsPerScan, 1, 500),
                cancellationToken);
            foreach (var candidate in candidates)
            {
                await repository.EnsureDiscoveredAsync(candidate, cancellationToken);
                metrics.Stale(candidate.Stage);
            }

            var eligible = await repository.ListEligibleAsync(
                null,
                DateTimeOffset.UtcNow,
                Math.Clamp(options.MaxItemsPerScan, 1, 500),
                cancellationToken);
            using var limiter = new SemaphoreSlim(
                Math.Clamp(options.MaxConcurrentRecoveries, 1, 32));
            var tasks = eligible.Select(async item =>
            {
                await limiter.WaitAsync(cancellationToken);
                try
                {
                    await RecoverInternalAsync(
                        item.TenantId,
                        item.Id,
                        options.RecoveryActorId,
                        item.CorrelationId,
                        false,
                        cancellationToken);
                }
                finally
                {
                    limiter.Release();
                }
            });
            await Task.WhenAll(tasks);
            workerState.ScanSucceeded(candidates.Count, candidates.Count);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var safe = FailureSanitizer.FromException(exception, "RECOVERY_SCAN_FAILED");
            workerState.Failed(safe.Code);
            logger.LogError(
                "Intake recovery scan failed. Stage={Stage} FailureCode={FailureCode}",
                "RECOVERY_SCAN",
                safe.Code);
        }
        return workerState.Snapshot();
    }

    public async Task<RecoveryWorkItemResponse?> GetAsync(
        Guid tenantId,
        Guid workItemId,
        CancellationToken cancellationToken) =>
        (await repository.FindAsync(tenantId, workItemId, cancellationToken)) is { } item
            ? Map(item)
            : null;

    public async Task<(IReadOnlyList<RecoveryWorkItemResponse> Items, long TotalCount)> ListAsync(
        Guid tenantId,
        RecoveryQuery query,
        CancellationToken cancellationToken)
    {
        var result = await repository.ListAsync(tenantId, query, cancellationToken);
        return (result.Items.Select(Map).ToArray(), result.TotalCount);
    }

    public Task<RecoveryWorkItemResponse?> RecoverAsync(
        Guid tenantId,
        Guid workItemId,
        Guid actorUserId,
        string? correlationId,
        CancellationToken cancellationToken) =>
        RecoverInternalAsync(
            tenantId, workItemId, actorUserId, correlationId, true, cancellationToken);

    public async Task<bool> CancelAsync(
        Guid tenantId,
        Guid workItemId,
        Guid actorUserId,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var previous = await repository.FindAsync(tenantId, workItemId, cancellationToken);
        if (previous is null)
            return false;
        var cancelled = await repository.CancelAsync(
            tenantId, workItemId, actorUserId, cancellationToken);
        if (cancelled)
        {
            await auditSink.RecordAsync(
                new RecoveryAuditEntry(
                    "INTAKE_RECOVERY_CANCELLED",
                    tenantId,
                    previous.Stage,
                    previous.ObjectId,
                    actorUserId,
                    previous.RecoveryStatus,
                    IntakeRecoveryStatuses.Cancelled,
                    "RECOVERY_CANCELLED",
                    correlationId),
                CancellationToken.None);
        }
        return cancelled;
    }

    public Task<OperationsSummaryResponse> GetSummaryAsync(
        Guid tenantId,
        DateTimeOffset from,
        CancellationToken cancellationToken) =>
        repository.GetSummaryAsync(tenantId, from, cancellationToken);

    public Task<IReadOnlyList<StageCountResponse>> GetStageFunnelAsync(
        Guid tenantId,
        DateTimeOffset from,
        CancellationToken cancellationToken) =>
        repository.GetStageFunnelAsync(tenantId, from, cancellationToken);

    public Task<IReadOnlyList<FailureAggregateResponse>> GetFailuresAsync(
        Guid tenantId,
        DateTimeOffset from,
        CancellationToken cancellationToken) =>
        repository.GetFailuresAsync(tenantId, from, cancellationToken);

    public Task<RecoveryAnalyticsResponse> GetRecoveryAnalyticsAsync(
        Guid tenantId,
        DateTimeOffset from,
        CancellationToken cancellationToken) =>
        repository.GetRecoveryAnalyticsAsync(tenantId, from, cancellationToken);

    private async Task<RecoveryWorkItemResponse?> RecoverInternalAsync(
        Guid tenantId,
        Guid workItemId,
        Guid actorUserId,
        string? correlationId,
        bool manual,
        CancellationToken cancellationToken)
    {
        var current = await repository.FindAsync(tenantId, workItemId, cancellationToken);
        if (current is null)
            return null;
        var claimed = await repository.TryClaimAsync(
            tenantId,
            workItemId,
            DateTimeOffset.UtcNow,
            Math.Clamp(options.MaxRecoveryAttempts, 1, 50),
            manual,
            cancellationToken);
        if (claimed is null)
            return Map(current);

        if (manual)
            metrics.Manual(claimed.Stage);
        var stopwatch = Stopwatch.StartNew();
        RecoveryHandlerResult result;
        try
        {
            result = await registry.GetRequired(claimed.Stage).RecoverAsync(
                claimed, correlationId ?? claimed.CorrelationId, cancellationToken);
        }
        catch (Exception exception)
        {
            var safe = FailureSanitizer.FromException(exception);
            result = new(false, safe.Retryable, safe.Code, safe.Message, safe.Category);
            logger.LogWarning(
                "Intake recovery handler failed. TenantId={TenantId} Stage={Stage} ObjectId={ObjectId} FailureCode={FailureCode}",
                tenantId, claimed.Stage, claimed.ObjectId, safe.Code);
        }
        stopwatch.Stop();
        metrics.Duration(claimed.Stage, stopwatch.Elapsed.TotalMilliseconds);
        DateTimeOffset? retryAt = result.Retryable
            ? DateTimeOffset.UtcNow.AddSeconds(
                Math.Min(
                    Math.Clamp(options.RetryBackoffMaxSeconds, 1, 86400),
                    Math.Clamp(options.RetryBackoffBaseSeconds, 1, 3600) *
                    Math.Pow(2, Math.Min(claimed.AttemptCount - 1, 8))))
            : null;
        await repository.CompleteAsync(claimed, result, retryAt, CancellationToken.None);
        var completed = await repository.FindAsync(tenantId, workItemId, CancellationToken.None);
        var newStatus = completed?.RecoveryStatus ?? IntakeRecoveryStatuses.Failed;
        if (result.Recovered)
        {
            workerState.Recovered();
            metrics.Recovered(claimed.Stage);
        }
        else
        {
            workerState.Failed(result.FailureCode);
            metrics.Failed(claimed.Stage, result.FailureCategory);
            if (newStatus == IntakeRecoveryStatuses.Exhausted)
                metrics.Exhausted(claimed.Stage);
        }
        await auditSink.RecordAsync(
            new RecoveryAuditEntry(
                manual ? "INTAKE_RECOVERY_MANUAL_RETRY" : "INTAKE_RECOVERY_ATTEMPTED",
                tenantId,
                claimed.Stage,
                claimed.ObjectId,
                actorUserId,
                claimed.RecoveryStatus,
                newStatus,
                result.FailureCode,
                correlationId ?? claimed.CorrelationId),
            CancellationToken.None);
        return completed is null ? null : Map(completed);
    }

    private static RecoveryWorkItemResponse Map(IntakeRecoveryWorkItem x) =>
        new(x.Id, x.TenantId, x.Stage, x.ObjectId, x.DomainStatus, x.RecoveryStatus,
            x.LastFailureCode, x.FailureCategory, x.LastSafeMessage, x.Retryable,
            x.AttemptCount, x.LastRecoveryAttemptAt, x.NextRetryAt, x.StaleSince,
            x.CreatedAt, x.CorrelationId);
}

public sealed class SnapshotRecoveryHandler(
    IIntakeRecoveryRepository repository) : IIntakeRecoveryHandler
{
    public string Stage => IntakeRecoveryStages.Snapshot;

    public async Task<RecoveryHandlerResult> RecoverAsync(
        IntakeRecoveryWorkItem item,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var changed = await repository.MarkCreatingSnapshotFailedAsync(
            item.TenantId, item.ObjectId, cancellationToken);
        return changed
            ? new(false, false, "SNAPSHOT_CREATION_STALE",
                "The stale snapshot was safely marked failed and requires a new approved snapshot request.",
                IntakeFailureCategories.Data)
            : new(false, false, "SNAPSHOT_NOT_CREATING",
                "The snapshot was no longer in a recoverable creating state.",
                IntakeFailureCategories.Concurrency);
    }
}

public sealed class AdapterRecoveryHandler(
    IIntakeRecoveryRepository repository,
    IIntakeAdapterExecutionService service) : IIntakeRecoveryHandler
{
    public string Stage => IntakeRecoveryStages.AdapterExecution;

    public async Task<RecoveryHandlerResult> RecoverAsync(
        IntakeRecoveryWorkItem item,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var snapshotId = await repository.FindAdapterSnapshotIdAsync(
            item.TenantId, item.ObjectId, cancellationToken);
        if (!snapshotId.HasValue)
            return new(false, false, "ADAPTER_NOT_FOUND",
                "The adapter execution no longer exists in the tenant scope.",
                IntakeFailureCategories.Data);
        var response = await service.RetryAsync(
            item.TenantId,
            snapshotId.Value,
            item.ObjectId,
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            correlationId,
            cancellationToken);
        var success = response.Status == IntakeAdapterExecutionStatuses.Succeeded;
        return success
            ? new(true, false, null, "The adapter execution was reconciled or retried safely.",
                IntakeFailureCategories.Integrity)
            : new(false, response.Status == IntakeAdapterExecutionStatuses.Retryable,
                response.FailureCode ?? "ADAPTER_RECOVERY_FAILED",
                response.FailureMessage ?? "The adapter did not complete successfully.",
                IntakeFailureCategories.Dependency);
    }
}

public sealed class DocumentAssociationRecoveryHandler(
    IIntakeRecoveryRepository repository,
    IDocumentAssociationExecutionService service) : IIntakeRecoveryHandler
{
    public string Stage => IntakeRecoveryStages.DocumentAssociation;

    public async Task<RecoveryHandlerResult> RecoverAsync(
        IntakeRecoveryWorkItem item,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var executionId = await repository.FindExecutionIdByDocumentAssociationItemAsync(
            item.TenantId, item.ObjectId, cancellationToken) ?? item.ObjectId;
        var snapshotId = await repository.FindDocumentAssociationSnapshotIdAsync(
            item.TenantId, executionId, cancellationToken);
        if (!snapshotId.HasValue)
            return new(false, false, "DOCUMENT_ASSOCIATION_NOT_FOUND",
                "The document-association execution no longer exists in the tenant scope.",
                IntakeFailureCategories.Data);
        var response = await service.RetryAsync(
            item.TenantId,
            snapshotId.Value,
            executionId,
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            correlationId,
            cancellationToken);
        var success = response.Status == DocumentAssociationExecutionStatuses.Succeeded;
        return success
            ? new(true, false, null, "Document associations were reconciled or retried safely.",
                IntakeFailureCategories.Integrity)
            : new(false, response.Status is DocumentAssociationExecutionStatuses.Retryable
                or DocumentAssociationExecutionStatuses.PartiallySucceeded,
                response.FailureCode ?? "DOCUMENT_ASSOCIATION_RECOVERY_FAILED",
                response.FailureMessage ?? "Document association did not complete successfully.",
                IntakeFailureCategories.Dependency);
    }
}

public sealed class DeterministicAttentionRecoveryHandler(string stage)
    : IIntakeRecoveryHandler
{
    public string Stage => stage;

    public Task<RecoveryHandlerResult> RecoverAsync(
        IntakeRecoveryWorkItem item,
        string? correlationId,
        CancellationToken cancellationToken) =>
        Task.FromResult(new RecoveryHandlerResult(
            false,
            false,
            "UPSTREAM_REPLAY_REQUIRES_OPERATOR",
            "This historical stage is immutable and has no safe replay command; inspect the recorded lineage.",
            IntakeFailureCategories.Integrity));
}