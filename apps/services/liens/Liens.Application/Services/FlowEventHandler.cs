using Liens.Application.Events;
using Liens.Application.Interfaces;
using Liens.Application.Repositories;
using Microsoft.Extensions.Logging;

namespace Liens.Application.Services;

/// <summary>
/// LS-LIENS-FLOW-009 — processes validated Flow step-change events.
///
/// Responsibilities:
/// 1. Batch-fetch all tasks linked to the emitted <c>WorkflowInstanceId</c>
/// 2. For each task delegate sync to <see cref="ITaskFlowSyncService"/>
/// 3. Emit <c>liens.task.flow_step_synced</c> audit event for actual changes (no-ops are silent)
///
/// The handler is intentionally narrow: it does not mutate task status, assignment, or priority.
/// Those remain Liens-owned. Flow owns step execution; Liens owns task runtime.
/// </summary>
public sealed class FlowEventHandler : IFlowEventHandler
{
    private const string ExpectedEventType = "workflow.step.changed";
    private const string ExpectedProductCode = "SYNQ_LIENS";

    private readonly ILienTaskRepository     _taskRepo;
    private readonly ITaskFlowSyncService    _sync;
    private readonly IAuditPublisher         _audit;
    private readonly ILogger<FlowEventHandler> _logger;

    public FlowEventHandler(
        ILienTaskRepository      taskRepo,
        ITaskFlowSyncService     sync,
        IAuditPublisher          audit,
        ILogger<FlowEventHandler> logger)
    {
        _taskRepo = taskRepo;
        _sync     = sync;
        _audit    = audit;
        _logger   = logger;
    }

    public async Task<FlowEventHandleResult> HandleStepChangedAsync(
        FlowStepChangedEvent evt,
        CancellationToken    ct = default)
    {
        // ── Sanity check (should not reach here if endpoint validation is thorough) ──
        if (!string.Equals(evt.EventType, ExpectedEventType, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(evt.ProductCode, ExpectedProductCode, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "FlowEventHandler: Unexpected event type '{Type}' / product '{Product}' — ignored.",
                evt.EventType, evt.ProductCode);
            return new FlowEventHandleResult { Processed = 0, NoOp = 0 };
        }

        // ── Batch-fetch all tasks for this workflow instance ─────────────────────────
        var tasks = await _taskRepo.GetByWorkflowInstanceIdAsync(evt.TenantId, evt.WorkflowInstanceId, ct);

        if (tasks.Count == 0)
        {
            _logger.LogDebug(
                "FlowEventHandler: No tasks linked to WorkflowInstanceId {InstanceId} for tenant {TenantId}.",
                evt.WorkflowInstanceId, evt.TenantId);
            return new FlowEventHandleResult { Processed = 0, NoOp = 0 };
        }

        int synced = 0, noOp = 0;

        foreach (var task in tasks)
        {
            var previousStepKey = task.WorkflowStepKey;

            SyncOutcome outcome;
            try
            {
                outcome = await _sync.SyncAsync(task, evt.CurrentStepKey, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "FlowEventHandler: Sync failed for task {TaskId} — skipping, other tasks unaffected.",
                    task.Id);
                continue;
            }

            if (outcome == SyncOutcome.Synced)
            {
                synced++;
                _audit.Publish(
                    eventType:   "liens.task.flow_step_synced",
                    action:      "update",
                    description: $"Task '{task.Title}' step synced from '{previousStepKey ?? "N/A"}' to '{evt.CurrentStepKey}' via Flow event",
                    tenantId:    evt.TenantId,
                    actorUserId: Guid.Empty,
                    entityType:  "LienTask",
                    entityId:    task.Id.ToString());
            }
            else
            {
                noOp++;
            }
        }

        _logger.LogInformation(
            "FlowEventHandler: WorkflowInstance {InstanceId} step='{Step}' — {Synced} synced, {NoOp} no-op.",
            evt.WorkflowInstanceId, evt.CurrentStepKey, synced, noOp);

        return new FlowEventHandleResult { Processed = synced, NoOp = noOp };
    }
}
