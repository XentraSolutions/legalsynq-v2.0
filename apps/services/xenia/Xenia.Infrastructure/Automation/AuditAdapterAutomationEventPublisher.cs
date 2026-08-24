using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xenia.Application.Adapters.Interfaces;
using Xenia.Application.Automation;

namespace Xenia.Infrastructure.Automation;

/// <summary>
/// Publishes automation lifecycle events by writing to the IAuditAdapter.
/// All events use the "xenia.automation." prefix to distinguish from email events.
/// Fail-silent: audit failures never surface as execution failures.
///
/// Uses IServiceScopeFactory to resolve the scoped IAuditAdapter from a singleton
/// context — prevents the captive dependency anti-pattern where a singleton holds
/// a reference to a scoped service beyond the scope's lifetime.
/// </summary>
internal sealed class AuditAdapterAutomationEventPublisher : IAutomationEventPublisher
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AuditAdapterAutomationEventPublisher> _logger;

    public AuditAdapterAutomationEventPublisher(
        IServiceScopeFactory scopeFactory,
        ILogger<AuditAdapterAutomationEventPublisher> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    public Task PublishRegisteredAsync(string key, string version, CancellationToken ct = default) =>
        TryEmitAsync("xenia.automation.registered", "automation", key, null, null, null, $"version={version}", ct);

    public Task PublishEnabledAsync(string key, string version, Guid? tenantId, Guid actorId, CancellationToken ct = default) =>
        TryEmitAsync("xenia.automation.enabled", "automation", key, tenantId, actorId, null, $"version={version}", ct);

    public Task PublishDisabledAsync(string key, string version, Guid? tenantId, Guid actorId, CancellationToken ct = default) =>
        TryEmitAsync("xenia.automation.disabled", "automation", key, tenantId, actorId, null, $"version={version}", ct);

    public Task PublishExecutionQueuedAsync(string key, string version, Guid executionId, Guid? tenantId, string? correlationId, CancellationToken ct = default) =>
        TryEmitAsync("xenia.automation.execution.queued", "automation_execution", executionId.ToString(), tenantId, null, correlationId, $"key={key} version={version}", ct);

    public Task PublishExecutionStartedAsync(string key, string version, Guid executionId, Guid? tenantId, string? correlationId, CancellationToken ct = default) =>
        TryEmitAsync("xenia.automation.execution.started", "automation_execution", executionId.ToString(), tenantId, null, correlationId, $"key={key} version={version}", ct);

    public Task PublishExecutionCompletedAsync(string key, string version, Guid executionId, Guid? tenantId, string? correlationId, CancellationToken ct = default) =>
        TryEmitAsync("xenia.automation.execution.completed", "automation_execution", executionId.ToString(), tenantId, null, correlationId, $"key={key} version={version}", ct);

    public Task PublishExecutionFailedAsync(string key, string version, Guid executionId, Guid? tenantId, string? correlationId, string failureCategory, CancellationToken ct = default) =>
        TryEmitAsync("xenia.automation.execution.failed", "automation_execution", executionId.ToString(), tenantId, null, correlationId, $"key={key} version={version} category={failureCategory}", ct);

    public Task PublishExecutionCancelledAsync(string key, string version, Guid executionId, Guid? tenantId, CancellationToken ct = default) =>
        TryEmitAsync("xenia.automation.execution.cancelled", "automation_execution", executionId.ToString(), tenantId, null, null, $"key={key} version={version}", ct);

    public Task PublishDeadLetteredAsync(string key, string version, Guid executionId, Guid? tenantId, string failureCategory, CancellationToken ct = default) =>
        TryEmitAsync("xenia.automation.execution.dead_lettered", "automation_execution", executionId.ToString(), tenantId, null, null, $"key={key} version={version} category={failureCategory}", ct);

    public Task PublishHealthChangedAsync(string key, string version, string previousState, string currentState, CancellationToken ct = default) =>
        TryEmitAsync("xenia.automation.health_changed", "automation", key, null, null, null, $"version={version} previous={previousState} current={currentState}", ct);

    public Task PublishConfigurationChangedAsync(string key, Guid? tenantId, Guid actorId, CancellationToken ct = default) =>
        TryEmitAsync("xenia.automation.configuration_changed", "automation", key, tenantId, actorId, null, null, ct);

    private async Task TryEmitAsync(
        string action, string resourceType, string? resourceId,
        Guid? tenantId, Guid? actorId, string? correlationId, string? detail,
        CancellationToken ct)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var auditAdapter = scope.ServiceProvider.GetRequiredService<IAuditAdapter>();
            await auditAdapter.RecordEventAsync(new XeniaAuditEvent
            {
                Action        = action,
                ResourceType  = resourceType,
                ResourceId    = resourceId,
                Result        = "emitted",
                TenantId      = tenantId,
                ActorId       = actorId,
                CorrelationId = correlationId,
                OccurredAt    = DateTime.UtcNow,
                Detail        = detail,
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Automation event emit failed: action={Action}", action);
        }
    }
}
