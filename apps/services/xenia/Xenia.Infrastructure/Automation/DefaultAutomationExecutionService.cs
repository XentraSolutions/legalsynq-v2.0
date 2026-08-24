using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Xenia.Application.Automation;
using Xenia.Application.Automation.Models;
using Xenia.Domain.Automation;

namespace Xenia.Infrastructure.Automation;

/// <summary>
/// Routes execution requests to the appropriate provider and records execution history.
/// Phase 1: in-memory execution history (bounded ring buffer, 1 000 entries).
/// Phase H will add EF persistence.
/// </summary>
internal sealed class DefaultAutomationExecutionService : IAutomationExecutionService
{
    private const int MaxHistoryEntries = 1_000;

    private readonly IAutomationRegistry _registry;
    private readonly IAutomationEventPublisher _events;
    private readonly IAutomationDeadLetterStore _dlq;
    private readonly ILogger<DefaultAutomationExecutionService> _logger;

    private readonly ConcurrentQueue<AutomationExecutionMetadata> _history = new();

    public DefaultAutomationExecutionService(
        IAutomationRegistry registry,
        IAutomationEventPublisher events,
        IAutomationDeadLetterStore dlq,
        ILogger<DefaultAutomationExecutionService> logger)
    {
        _registry = registry;
        _events   = events;
        _dlq      = dlq;
        _logger   = logger;
    }

    public async Task<AutomationExecutionResult> ExecuteAsync(
        AutomationExecutionRequest request,
        CancellationToken ct = default)
    {
        var executionId = Guid.CreateVersion7();
        var queuedAt    = DateTime.UtcNow;

        var provider = _registry.GetProvider(request.AutomationKey);
        if (provider is null)
        {
            return Failed(executionId, request, queuedAt,
                "PROVIDER_NOT_FOUND", $"Automation '{request.AutomationKey}' is not registered.");
        }

        var effectiveState = await _registry.GetEffectiveStateAsync(
            request.AutomationKey, request.Context.TenantId, ct);

        if (effectiveState is AutomationLifecycleState.Disabled or AutomationLifecycleState.Retired)
        {
            return Failed(executionId, request, queuedAt,
                "AUTOMATION_DISABLED", $"Automation '{request.AutomationKey}' is disabled.");
        }

        if (!provider.SupportsExecution(request))
        {
            return Failed(executionId, request, queuedAt,
                "UNSUPPORTED_EXECUTION", $"Provider '{request.AutomationKey}' does not support this execution type.");
        }

        await _events.PublishExecutionQueuedAsync(
            request.AutomationKey, provider.Version, executionId,
            request.Context.TenantId, request.Context.CorrelationId, ct);

        var startedAt = DateTime.UtcNow;
        AddHistory(executionId, request, queuedAt, AutomationExecutionStatus.Running);
        await _events.PublishExecutionStartedAsync(
            request.AutomationKey, provider.Version, executionId,
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
            await _events.PublishExecutionCancelledAsync(
                request.AutomationKey, provider.Version, executionId,
                request.Context.TenantId, ct);
            return Cancelled(executionId, request, startedAt);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unhandled exception in automation provider '{Key}'", request.AutomationKey);
            result = Failed(executionId, request, startedAt, "UNHANDLED_EXCEPTION", "An unexpected error occurred.");
        }

        UpdateHistory(executionId, result.Status);

        if (result.IsSuccess)
        {
            await _events.PublishExecutionCompletedAsync(
                request.AutomationKey, provider.Version, executionId,
                request.Context.TenantId, request.Context.CorrelationId, ct);
        }
        else
        {
            await _events.PublishExecutionFailedAsync(
                request.AutomationKey, provider.Version, executionId,
                request.Context.TenantId, request.Context.CorrelationId,
                result.FailureCategory ?? "UNKNOWN", ct);

            if (result.RetryCount >= request.MaxRetryAttempts)
            {
                var dlEntry = AutomationDeadLetterEntry.Create(
                    request.Context.TenantId,
                    request.AutomationKey,
                    provider.Version,
                    executionId,
                    request.TriggerType,
                    result.FailureCategory ?? "UNKNOWN",
                    result.SafeErrorSummary ?? "Execution failed.",
                    request.Context.CorrelationId,
                    startedAt);
                await _dlq.CreateAsync(dlEntry, ct);
                await _events.PublishDeadLetteredAsync(
                    request.AutomationKey, provider.Version, executionId,
                    request.Context.TenantId, result.FailureCategory ?? "UNKNOWN", ct);
            }
        }

        return result;
    }

    public async Task<bool> CancelAsync(string automationKey, Guid executionId, Guid? tenantId, CancellationToken ct = default)
    {
        var provider = _registry.GetProvider(automationKey);
        if (provider is null || !provider.SupportsCancellation) return false;
        return await provider.CancelAsync(executionId, tenantId, ct);
    }

    public Task<IReadOnlyList<AutomationExecutionMetadata>> GetExecutionHistoryAsync(
        string? automationKey, Guid? tenantId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _history.AsEnumerable();
        if (automationKey is not null)
            query = query.Where(h => h.AutomationKey.Equals(automationKey, StringComparison.OrdinalIgnoreCase));
        if (tenantId.HasValue)
            query = query.Where(h => h.TenantId == tenantId);

        IReadOnlyList<AutomationExecutionMetadata> result = [.. query
            .OrderByDescending(h => h.QueuedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)];
        return Task.FromResult(result);
    }

    public Task<AutomationExecutionMetadata?> GetExecutionAsync(Guid executionId, Guid? tenantId, CancellationToken ct = default)
    {
        var entry = _history.FirstOrDefault(h =>
            h.ExecutionId == executionId && (!tenantId.HasValue || h.TenantId == tenantId));
        return Task.FromResult<AutomationExecutionMetadata?>(entry);
    }

    private void AddHistory(Guid executionId, AutomationExecutionRequest req, DateTime queuedAt, AutomationExecutionStatus status)
    {
        while (_history.Count >= MaxHistoryEntries)
            _history.TryDequeue(out _);

        _history.Enqueue(new AutomationExecutionMetadata
        {
            ExecutionId    = executionId,
            AutomationKey  = req.AutomationKey,
            AutomationVersion = req.AutomationVersion ?? "unknown",
            TenantId       = req.Context.TenantId,
            TriggerType    = req.TriggerType,
            Status         = status,
            QueuedAt       = queuedAt,
            StartedAt      = DateTime.UtcNow,
            CorrelationId  = req.Context.CorrelationId,
        });
    }

    private void UpdateHistory(Guid executionId, AutomationExecutionStatus status)
    {
        var entries = _history.ToArray();
        foreach (var e in entries.Where(h => h.ExecutionId == executionId))
        {
            while (_history.TryDequeue(out _)) { }
            foreach (var x in entries)
            {
                _history.Enqueue(x.ExecutionId == executionId
                    ? x with { Status = status, CompletedAt = DateTime.UtcNow }
                    : x);
            }
            break;
        }
    }

    private static AutomationExecutionResult Failed(
        Guid executionId, AutomationExecutionRequest req, DateTime startedAt,
        string category, string summary) =>
        new()
        {
            ExecutionId      = executionId,
            AutomationKey    = req.AutomationKey,
            AutomationVersion = req.AutomationVersion ?? "unknown",
            Status           = AutomationExecutionStatus.Failed,
            StartedAt        = startedAt,
            CompletedAt      = DateTime.UtcNow,
            RetryCount       = 0,
            FailureCategory  = category,
            SafeErrorSummary = summary,
        };

    private static AutomationExecutionResult Cancelled(
        Guid executionId, AutomationExecutionRequest req, DateTime startedAt) =>
        new()
        {
            ExecutionId      = executionId,
            AutomationKey    = req.AutomationKey,
            AutomationVersion = req.AutomationVersion ?? "unknown",
            Status           = AutomationExecutionStatus.Cancelled,
            StartedAt        = startedAt,
            CompletedAt      = DateTime.UtcNow,
        };
}
