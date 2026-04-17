using Flow.Application.Adapters.AuditAdapter;
using Flow.Application.Adapters.NotificationAdapter;
using Flow.Application.Events;
using Microsoft.Extensions.Logging;

namespace Flow.Infrastructure.Events;

/// <summary>
/// In-process dispatcher: fans out a Flow event to the audit + notification
/// adapter seams. Failures are swallowed and logged so adapter outages cannot
/// break the originating workflow/task operation.
/// </summary>
public sealed class FlowEventDispatcher : IFlowEventDispatcher
{
    private readonly IAuditAdapter _audit;
    private readonly INotificationAdapter _notifications;
    private readonly ILogger<FlowEventDispatcher> _log;

    public FlowEventDispatcher(
        IAuditAdapter audit,
        INotificationAdapter notifications,
        ILogger<FlowEventDispatcher> log)
    {
        _audit = audit;
        _notifications = notifications;
        _log = log;
    }

    public async Task PublishAsync(IFlowEvent flowEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            await _audit.WriteEventAsync(MapToAudit(flowEvent), cancellationToken);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Audit dispatch failed for {EventKey}", flowEvent.EventKey);
        }

        var notification = MapToNotification(flowEvent);
        if (notification is not null)
        {
            try
            {
                await _notifications.SendAsync(notification, cancellationToken);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Notification dispatch failed for {EventKey}", flowEvent.EventKey);
            }
        }
    }

    private static AuditEvent MapToAudit(IFlowEvent e) => e switch
    {
        WorkflowCreatedEvent w => new AuditEvent(
            "workflow.created", "Workflow", w.WorkflowId.ToString(),
            w.TenantId, w.UserId, $"Workflow '{w.Name}' created (productKey={w.ProductKey})",
            OccurredAtUtc: w.OccurredAtUtc),

        WorkflowStateChangedEvent w => new AuditEvent(
            "workflow.state_changed", "Workflow", w.WorkflowId.ToString(),
            w.TenantId, w.UserId, $"Workflow state {w.FromState} → {w.ToState}",
            OccurredAtUtc: w.OccurredAtUtc),

        WorkflowCompletedEvent w => new AuditEvent(
            "workflow.completed", "Workflow", w.WorkflowId.ToString(),
            w.TenantId, w.UserId, "Workflow completed",
            OccurredAtUtc: w.OccurredAtUtc),

        TaskAssignedEvent t => new AuditEvent(
            "task.assigned", "Task", t.TaskId.ToString(),
            t.TenantId, t.AssignedByUserId,
            $"Task assigned to user={t.AssignedToUserId} role={t.AssignedToRoleKey} org={t.AssignedToOrgId}",
            OccurredAtUtc: t.OccurredAtUtc),

        TaskCompletedEvent t => new AuditEvent(
            "task.completed", "Task", t.TaskId.ToString(),
            t.TenantId, t.CompletedByUserId, "Task completed",
            OccurredAtUtc: t.OccurredAtUtc),

        _ => new AuditEvent(e.EventKey, "Unknown", string.Empty, e.TenantId, null, null,
            OccurredAtUtc: e.OccurredAtUtc),
    };

    private static NotificationMessage? MapToNotification(IFlowEvent e) => e switch
    {
        TaskAssignedEvent t => new NotificationMessage(
            Channel: "system",
            EventKey: t.EventKey,
            TenantId: t.TenantId,
            RecipientUserId: t.AssignedToUserId,
            RecipientRoleKey: t.AssignedToRoleKey,
            Subject: "New task assigned",
            Body: $"Task {t.TaskId} has been assigned to you."),

        WorkflowCompletedEvent w => new NotificationMessage(
            Channel: "system",
            EventKey: w.EventKey,
            TenantId: w.TenantId,
            RecipientUserId: w.UserId,
            RecipientRoleKey: null,
            Subject: "Workflow completed",
            Body: $"Workflow {w.WorkflowId} has completed."),

        // workflow created/state change + task completed → audit only by default
        _ => null,
    };
}
