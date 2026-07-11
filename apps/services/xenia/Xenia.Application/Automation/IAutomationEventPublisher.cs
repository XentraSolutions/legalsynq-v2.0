namespace Xenia.Application.Automation;

/// <summary>
/// Publishes automation lifecycle events to the Xenia event framework.
/// Events are forwarded to the IAuditAdapter for durable storage.
/// No business payloads in events — safe metadata only.
/// </summary>
public interface IAutomationEventPublisher
{
    Task PublishRegisteredAsync(string automationKey, string version, CancellationToken ct = default);
    Task PublishEnabledAsync(string automationKey, string version, Guid? tenantId, Guid actorId, CancellationToken ct = default);
    Task PublishDisabledAsync(string automationKey, string version, Guid? tenantId, Guid actorId, CancellationToken ct = default);
    Task PublishExecutionQueuedAsync(string automationKey, string version, Guid executionId, Guid? tenantId, string? correlationId, CancellationToken ct = default);
    Task PublishExecutionStartedAsync(string automationKey, string version, Guid executionId, Guid? tenantId, string? correlationId, CancellationToken ct = default);
    Task PublishExecutionCompletedAsync(string automationKey, string version, Guid executionId, Guid? tenantId, string? correlationId, CancellationToken ct = default);
    Task PublishExecutionFailedAsync(string automationKey, string version, Guid executionId, Guid? tenantId, string? correlationId, string failureCategory, CancellationToken ct = default);
    Task PublishExecutionCancelledAsync(string automationKey, string version, Guid executionId, Guid? tenantId, CancellationToken ct = default);
    Task PublishDeadLetteredAsync(string automationKey, string version, Guid executionId, Guid? tenantId, string failureCategory, CancellationToken ct = default);
    Task PublishHealthChangedAsync(string automationKey, string version, string previousState, string currentState, CancellationToken ct = default);
    Task PublishConfigurationChangedAsync(string automationKey, Guid? tenantId, Guid actorId, CancellationToken ct = default);
}
