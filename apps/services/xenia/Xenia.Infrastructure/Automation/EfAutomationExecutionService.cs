using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xenia.Application.Automation;
using Xenia.Application.Automation.Models;
using Xenia.Domain.Automation;
using Xenia.Infrastructure.Persistence;

namespace Xenia.Infrastructure.Automation;

/// <summary>
/// EF Core–backed implementation of <see cref="IAutomationExecutionService"/>.
///
/// Replaces <see cref="DefaultAutomationExecutionService"/>'s in-memory ring buffer
/// with durable persistence to xn_automation_executions.
///
/// Execution lifecycle:
///   1. Check idempotency key (duplicate → return existing execution metadata).
///   2. Create execution record in Queued state.
///   3. Bind idempotency record to execution ID.
///   4. Atomically transition to Running via MarkRunning().
///   5. Invoke provider.
///   6. Persist final status (Completed / Failed / Cancelled / DeadLettered).
///   7. Update runtime state counters.
///
/// Scoped — injected with a single <see cref="XeniaDbContext"/> instance per request.
/// Uses <see cref="IAutomationIdempotencyService"/> (singleton) for deduplication.
/// </summary>
internal sealed class EfAutomationExecutionService : IAutomationExecutionService
{
    private readonly XeniaDbContext _ctx;
    private readonly IAutomationRegistry _registry;
    private readonly IAutomationEventPublisher _events;
    private readonly IAutomationDeadLetterStore _dlq;
    private readonly IAutomationIdempotencyService _idempotency;
    private readonly ILogger<EfAutomationExecutionService> _logger;

    public EfAutomationExecutionService(
        XeniaDbContext ctx,
        IAutomationRegistry registry,
        IAutomationEventPublisher events,
        IAutomationDeadLetterStore dlq,
        IAutomationIdempotencyService idempotency,
        ILogger<EfAutomationExecutionService> logger)
    {
        _ctx        = ctx;
        _registry   = registry;
        _events     = events;
        _dlq        = dlq;
        _idempotency = idempotency;
        _logger     = logger;
    }

    public async Task<AutomationExecutionResult> ExecuteAsync(
        AutomationExecutionRequest request,
        CancellationToken ct = default)
    {
        var provider = _registry.GetProvider(request.AutomationKey);
        if (provider is null)
            return BuildFailed(Guid.CreateVersion7(), request, DateTime.UtcNow,
                "PROVIDER_NOT_FOUND", $"Automation '{request.AutomationKey}' is not registered.");

        var effectiveState = await _registry.GetEffectiveStateAsync(
            request.AutomationKey, request.Context.TenantId, ct);

        if (effectiveState is AutomationLifecycleState.Disabled or AutomationLifecycleState.Retired)
            return BuildFailed(Guid.CreateVersion7(), request, DateTime.UtcNow,
                "AUTOMATION_DISABLED", $"Automation '{request.AutomationKey}' is disabled.");

        if (!provider.SupportsExecution(request))
            return BuildFailed(Guid.CreateVersion7(), request, DateTime.UtcNow,
                "UNSUPPORTED_EXECUTION",
                $"Provider '{request.AutomationKey}' does not support this execution type.");

        var tenantId = request.Context.TenantId ?? Guid.Empty;

        if (!string.IsNullOrEmpty(request.IdempotencyKey))
        {
            var expiresAt  = DateTime.UtcNow.AddDays(1);
            var fingerprint = ComputeFingerprint(request);
            var reservation = await _idempotency.TryReserveAsync(
                tenantId, request.AutomationKey, request.IdempotencyKey,
                fingerprint, expiresAt, ct);

            if (reservation.Result == IdempotencyReservationResult.AlreadyExists)
            {
                _logger.LogDebug(
                    "Idempotency hit: key={Key} ikey={IKey} existingExecId={ExecId}",
                    request.AutomationKey, request.IdempotencyKey, reservation.ExistingExecutionId);

                if (reservation.ExistingExecutionId.HasValue)
                {
                    var existing = await GetExecutionAsync(
                        reservation.ExistingExecutionId.Value, request.Context.TenantId, ct);
                    if (existing is not null)
                        return BuildResultFromMetadata(existing);
                }
                return BuildFailed(Guid.CreateVersion7(), request, DateTime.UtcNow,
                    "IDEMPOTENT_DUPLICATE", "Duplicate request — original execution is in progress.");
            }

            if (reservation.Result == IdempotencyReservationResult.Conflict)
                return BuildFailed(Guid.CreateVersion7(), request, DateTime.UtcNow,
                    "IDEMPOTENCY_CONFLICT",
                    "Idempotency key reuse with a different request fingerprint is not allowed.");
        }

        var correlationId = request.Context.CorrelationId is { } cid &&
                            Guid.TryParse(cid, out var cidGuid) ? cidGuid : (Guid?)null;

        var execRecord = AutomationExecutionRecord.Create(
            tenantId,
            request.AutomationKey,
            request.AutomationVersion ?? provider.Version,
            request.TriggerType,
            string.IsNullOrEmpty(request.IdempotencyKey) ? null : request.IdempotencyKey,
            correlationId,
            request.Context.ActorId?.ToString());

        _ctx.AutomationExecutions.Add(execRecord);
        await _ctx.SaveChangesAsync(ct);

        if (!string.IsNullOrEmpty(request.IdempotencyKey))
        {
            await _idempotency.BindExecutionAsync(
                tenantId, request.AutomationKey, request.IdempotencyKey,
                execRecord.ExecutionId, ct);
        }

        await _events.PublishExecutionQueuedAsync(
            request.AutomationKey, provider.Version, execRecord.ExecutionId,
            request.Context.TenantId, request.Context.CorrelationId, ct);

        execRecord.MarkRunning();
        _ctx.AutomationExecutions.Update(execRecord);
        await _ctx.SaveChangesAsync(ct);

        await _events.PublishExecutionStartedAsync(
            request.AutomationKey, provider.Version, execRecord.ExecutionId,
            request.Context.TenantId, request.Context.CorrelationId, ct);

        AutomationExecutionResult result;
        try
        {
            using var timeout = request.Timeout.HasValue
                ? CancellationTokenSource.CreateLinkedTokenSource(ct)
                : null;
            timeout?.CancelAfter(request.Timeout!.Value);
            var effectiveCt = timeout?.Token ?? ct;

            result = await provider.ExecuteAsync(request, effectiveCt);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            execRecord.MarkCancelled();
            _ctx.AutomationExecutions.Update(execRecord);
            await _ctx.SaveChangesAsync(CancellationToken.None);

            await _events.PublishExecutionCancelledAsync(
                request.AutomationKey, provider.Version, execRecord.ExecutionId,
                request.Context.TenantId, CancellationToken.None);

            return BuildCancelled(execRecord);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Unhandled exception in automation provider '{Key}'", request.AutomationKey);
            result = BuildFailed(execRecord.ExecutionId, request, execRecord.StartedAt ?? DateTime.UtcNow,
                "UNHANDLED_EXCEPTION", "An unexpected error occurred.");
        }

        if (result.IsSuccess)
        {
            execRecord.MarkCompleted(
                result.SafeErrorSummary is null ? "Completed successfully." : null);
            await _events.PublishExecutionCompletedAsync(
                request.AutomationKey, provider.Version, execRecord.ExecutionId,
                request.Context.TenantId, request.Context.CorrelationId, ct);
        }
        else
        {
            execRecord.MarkFailed(result.FailureCategory, result.SafeErrorSummary);
            await _events.PublishExecutionFailedAsync(
                request.AutomationKey, provider.Version, execRecord.ExecutionId,
                request.Context.TenantId, request.Context.CorrelationId,
                result.FailureCategory ?? "UNKNOWN", ct);

            if (result.RetryCount >= request.MaxRetryAttempts)
            {
                var dlEntry = AutomationDeadLetterEntry.Create(
                    request.Context.TenantId,
                    request.AutomationKey,
                    provider.Version,
                    execRecord.ExecutionId,
                    request.TriggerType,
                    result.FailureCategory ?? "UNKNOWN",
                    result.SafeErrorSummary ?? "Execution failed.",
                    request.Context.CorrelationId,
                    execRecord.StartedAt ?? DateTime.UtcNow);
                var dlRecord = await _dlq.CreateAsync(dlEntry, ct);
                execRecord.MarkDeadLettered(dlRecord.Id);
                await _events.PublishDeadLetteredAsync(
                    request.AutomationKey, provider.Version, execRecord.ExecutionId,
                    request.Context.TenantId, result.FailureCategory ?? "UNKNOWN", ct);
            }
        }

        _ctx.AutomationExecutions.Update(execRecord);
        await _ctx.SaveChangesAsync(ct);

        return result with { ExecutionId = execRecord.ExecutionId };
    }

    public async Task<bool> CancelAsync(
        string automationKey, Guid executionId, Guid? tenantId, CancellationToken ct = default)
    {
        var provider = _registry.GetProvider(automationKey);
        if (provider is null || !provider.SupportsCancellation) return false;
        return await provider.CancelAsync(executionId, tenantId, ct);
    }

    public async Task<IReadOnlyList<AutomationExecutionMetadata>> GetExecutionHistoryAsync(
        string? automationKey, Guid? tenantId, int page, int pageSize, CancellationToken ct = default)
    {
        IQueryable<AutomationExecutionRecord> query =
            _ctx.AutomationExecutions.AsNoTracking();

        if (tenantId.HasValue)
            query = query.Where(e => e.TenantId == tenantId.Value);

        if (automationKey is not null)
            query = query.Where(e => e.AutomationKey == automationKey);

        var records = await query
            .OrderByDescending(e => e.QueuedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return records.ConvertAll(ToMetadata);
    }

    public async Task<AutomationExecutionMetadata?> GetExecutionAsync(
        Guid executionId, Guid? tenantId, CancellationToken ct = default)
    {
        var record = await _ctx.AutomationExecutions
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.ExecutionId == executionId, ct);

        if (record is null) return null;
        if (tenantId.HasValue && record.TenantId != tenantId.Value) return null;
        return ToMetadata(record);
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    private static AutomationExecutionMetadata ToMetadata(AutomationExecutionRecord r) =>
        new()
        {
            ExecutionId       = r.ExecutionId,
            AutomationKey     = r.AutomationKey,
            AutomationVersion = r.AutomationVersion,
            TenantId          = r.TenantId == Guid.Empty ? null : r.TenantId,
            TriggerType       = r.TriggerType,
            Status            = r.Status,
            QueuedAt          = r.QueuedAt,
            StartedAt         = r.StartedAt,
            CompletedAt       = r.CompletedAt,
            RetryCount        = r.RetryCount,
            CorrelationId     = r.CorrelationId?.ToString(),
            SafeErrorSummary  = r.SafeErrorSummary,
            FailureCategory   = r.SafeErrorCategory,
            IsDeadLettered    = r.Status == AutomationExecutionStatus.DeadLettered,
        };

    private static AutomationExecutionResult BuildResultFromMetadata(AutomationExecutionMetadata m) =>
        new()
        {
            ExecutionId       = m.ExecutionId,
            AutomationKey     = m.AutomationKey,
            AutomationVersion = m.AutomationVersion,
            Status            = m.Status,
            StartedAt         = m.StartedAt ?? m.QueuedAt,
            CompletedAt       = m.CompletedAt,
            RetryCount        = m.RetryCount,
            SafeErrorSummary  = m.SafeErrorSummary,
            FailureCategory   = m.FailureCategory,
        };

    private static AutomationExecutionResult BuildFailed(
        Guid executionId, AutomationExecutionRequest req, DateTime startedAt,
        string category, string summary) =>
        new()
        {
            ExecutionId       = executionId,
            AutomationKey     = req.AutomationKey,
            AutomationVersion = req.AutomationVersion ?? "unknown",
            Status            = AutomationExecutionStatus.Failed,
            StartedAt         = startedAt,
            CompletedAt       = DateTime.UtcNow,
            RetryCount        = 0,
            FailureCategory   = category,
            SafeErrorSummary  = summary,
        };

    private static AutomationExecutionResult BuildCancelled(AutomationExecutionRecord r) =>
        new()
        {
            ExecutionId       = r.ExecutionId,
            AutomationKey     = r.AutomationKey,
            AutomationVersion = r.AutomationVersion,
            Status            = AutomationExecutionStatus.Cancelled,
            StartedAt         = r.StartedAt ?? r.QueuedAt,
            CompletedAt       = r.CompletedAt,
        };

    private static string ComputeFingerprint(AutomationExecutionRequest req)
    {
        var raw = $"{req.AutomationKey}|{req.AutomationVersion}|{req.TriggerType}";
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes).ToLowerInvariant()[..64];
    }
}
