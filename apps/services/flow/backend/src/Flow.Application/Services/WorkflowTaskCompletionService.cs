using Flow.Application.DTOs;
using Flow.Application.Engines.WorkflowEngine;
using Flow.Application.Exceptions;
using Flow.Application.Interfaces;
using Flow.Domain.Common;
using Flow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Flow.Application.Services;

/// <summary>
/// LS-FLOW-E11.7 — default implementation of
/// <see cref="IWorkflowTaskCompletionService"/>.
///
/// <para>
/// <b>Orchestration shape</b>:
/// </para>
/// <list type="number">
///   <item>Pre-load the task: existence + tenant scoping (via global query
///         filter), <c>StepKey</c>, <c>WorkflowInstanceId</c>, current
///         <c>Status</c>. A non-existent / cross-tenant task surfaces as
///         <see cref="NotFoundException"/>; a task already in a terminal
///         state — or still <c>Open</c> — surfaces as
///         <see cref="InvalidStateTransitionException"/>. We deliberately
///         pre-validate before opening a transaction so the common
///         "wrong-state" path stays cheap and observable.</item>
///   <item>Open a transaction through the database's
///         <see cref="Microsoft.EntityFrameworkCore.Storage.IExecutionStrategy"/>
///         (so transient connection errors trigger a single coordinated
///         retry instead of partial-commit risk).</item>
///   <item>Run the task lifecycle CAS via
///         <see cref="IWorkflowTaskLifecycleService.CompleteTaskAsync"/>
///         (atomic <c>InProgress → Completed</c>).</item>
///   <item>Run the workflow advance via
///         <see cref="IWorkflowEngine.AdvanceAsync"/>, passing the task's
///         <c>StepKey</c> as <c>expectedCurrentStepKey</c>. The engine
///         performs the step-match check itself; mismatch surfaces as
///         <see cref="InvalidWorkflowTransitionException"/> and rolls
///         back the whole transaction (including the task's CAS write).
///         <c>toStepKey</c> is intentionally <c>null</c> — at this phase
///         we let the engine pick the unique outbound transition;
///         ambiguous-fan-out steps surface a clear error rather than
///         being silently resolved.</item>
///   <item>Commit. The persisted state is therefore atomic: either both
///         changes are visible or neither is.</item>
/// </list>
///
/// <para>
/// <b>Why pre-load the task even though both downstream services check
/// state independently?</b> Two reasons:
/// </para>
/// <list type="bullet">
///   <item>Consistent error taxonomy — a task that does not exist or
///         has already been cancelled never needs to enter a transaction
///         at all; the caller gets the same 404/422 it would have got
///         from the old <c>POST /workflow-tasks/{id}/complete</c>.</item>
///   <item>Lets us read the task's <c>WorkflowInstanceId</c> and
///         <c>StepKey</c> once and pass them to the engine (the engine
///         takes <c>workflowInstanceId</c> + <c>expectedCurrentStepKey</c>
///         positional arguments and does not load the task itself).</item>
/// </list>
///
/// <para>
/// <b>What this service deliberately does NOT do:</b> notification
/// fan-out, task reassignment, SLA re-evaluation triggering, admin
/// auditing — all of those remain owned by their existing dispatchers
/// (outbox / SLA evaluator / admin actions). The engine still enqueues
/// its <c>workflow.advance</c> / <c>workflow.complete</c> outbox events
/// during the same transaction, so downstream consumers light up
/// automatically.
/// </para>
/// </summary>
public sealed class WorkflowTaskCompletionService : IWorkflowTaskCompletionService
{
    private readonly IFlowDbContext _db;
    private readonly IWorkflowTaskLifecycleService _lifecycle;
    private readonly IWorkflowEngine _engine;
    private readonly ILogger<WorkflowTaskCompletionService> _log;

    public WorkflowTaskCompletionService(
        IFlowDbContext db,
        IWorkflowTaskLifecycleService lifecycle,
        IWorkflowEngine engine,
        ILogger<WorkflowTaskCompletionService> log)
    {
        _db = db;
        _lifecycle = lifecycle;
        _engine = engine;
        _log = log;
    }

    public async Task<WorkflowTaskCompletionResult> CompleteAndProgressAsync(
        Guid taskId, CancellationToken ct = default)
    {
        // ---- Phase 1: cheap pre-validation outside any transaction ----
        var snapshot = await _db.WorkflowTasks
            .AsNoTracking()
            .Where(t => t.Id == taskId)
            .Select(t => new { t.Id, t.Status, t.StepKey, t.WorkflowInstanceId })
            .FirstOrDefaultAsync(ct);

        if (snapshot is null)
        {
            // Includes both "doesn't exist" and "exists in another tenant"
            // — the global query filter cannot tell the difference, and
            // we MUST surface them identically to prevent cross-tenant
            // id probing.
            throw new NotFoundException(nameof(WorkflowTask), taskId);
        }

        // We disallow "complete a never-started task" at the orchestration
        // level too. The lifecycle service would also reject this with
        // InvalidStateTransitionException, but doing it here keeps the
        // error path uniform and avoids opening a transaction for a
        // request that can never succeed.
        if (!string.Equals(snapshot.Status, WorkflowTaskStatus.InProgress, StringComparison.Ordinal))
        {
            throw new InvalidStateTransitionException(snapshot.Status, WorkflowTaskStatus.Completed);
        }

        if (string.IsNullOrWhiteSpace(snapshot.StepKey))
        {
            // A WorkflowTask is created by the engine factory in E11.2 and
            // its StepKey is mirrored from the workflow's current step at
            // creation time. A blank StepKey here would mean a malformed
            // row (or a task created outside the documented path) — we
            // refuse to drive the engine with no step context.
            throw new ValidationException(
                $"WorkflowTask {taskId} has no StepKey; cannot bind to workflow progression.");
        }

        // ---- Phase 2: atomic completion + advancement ----
        //
        // TASK-FLOW-01 dual-write sequencing:
        //   2a. Task service complete (primary authority) — runs OUTSIDE any
        //       DB transaction so the HTTP call is not bounded by the DB
        //       execution strategy's retry scope.
        //   2b. DB execution strategy: shadow CAS (lifecycle.CompleteTaskAsync)
        //       + engine advance in one DB transaction.
        //
        // On step 2b failure after 2a succeeded: the task is Completed in
        // Task service but the workflow has not advanced. This inconsistency
        // is logged and surfaced to the caller. The caller may safely retry
        // because Task service's status transition is idempotent for terminal
        // statuses, and the engine's step-match check prevents double-advance.

        // 2a. Task service complete (primary write authority, no DB tx).
        // This call goes through WorkflowTaskLifecycleService which will
        // itself delegate to Task service as part of its own dual-write.
        // No separate direct call needed here — the lifecycle service handles
        // both the Task service delegation and the shadow CAS below.

        var strategy = _db.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.BeginTransactionAsync(ct);

            // 2b-i. Shadow CAS via lifecycle service (InProgress → Completed).
            // WorkflowTaskLifecycleService.CompleteTaskAsync calls the Task
            // service first (TASK-FLOW-01) and then applies the shadow CAS.
            // If another caller already completed (or cancelled) the task
            // between phase-1 read and here, this throws
            // WorkflowTaskConcurrencyException ⇒ 409.
            var taskResult = await _lifecycle.CompleteTaskAsync(taskId, ct);

            // 2b-ii. Drive the workflow engine. The engine is the sole
            //     execution authority and does its own checks:
            //       - workflow must be Active                 ⇒ 409
            //       - workflow.CurrentStepKey must equal
            //         expectedCurrentStepKey (= task.StepKey) ⇒ 409 (stale)
            //       - exactly one outbound transition or
            //         caller-named target                     ⇒ 409 if ambiguous
            //     Any failure throws and the using-await above rolls back
            //     the shadow CAS — but the Task service write in step 2b-i
            //     has already committed (no shared tx). Log the inconsistency.
            WorkflowInstanceResponse engineResult;
            try
            {
                engineResult = await _engine.AdvanceAsync(
                    workflowInstanceId:      snapshot.WorkflowInstanceId,
                    expectedCurrentStepKey:  snapshot.StepKey,
                    toStepKey:               null,
                    ct:                      ct);
            }
            catch (NotFoundException)
            {
                // The owning workflow vanished between phase-1 read and
                // the engine load (effectively impossible under FK
                // RESTRICT — but defensive). Surfacing as
                // InvalidWorkflowTransitionException keeps the caller's
                // error model consistent and aborts the transaction.
                throw new InvalidWorkflowTransitionException(
                    $"Owning workflow instance {snapshot.WorkflowInstanceId} for task {taskId} could not be loaded.",
                    "owning_workflow_missing");
            }

            await tx.CommitAsync(ct);

            _log.LogInformation(
                "WorkflowTaskCompletion bound task {TaskId} → workflow {InstanceId} advance ({From} → {To}, status={WfStatus})",
                taskId, snapshot.WorkflowInstanceId,
                snapshot.StepKey, engineResult.CurrentStepKey, engineResult.Status);

            return new WorkflowTaskCompletionResult(
                TaskId:             taskId,
                // Echo legacy lifecycle fields verbatim so existing
                // clients of the original POST /complete response
                // (E11.5 contract) continue to deserialise without
                // changes — the upgrade is purely additive.
                PreviousStatus:     taskResult.PreviousStatus,
                NewStatus:          taskResult.NewStatus,
                WorkflowInstanceId: snapshot.WorkflowInstanceId,
                FromStepKey:        snapshot.StepKey,
                ToStepKey:          engineResult.CurrentStepKey ?? snapshot.StepKey,
                WorkflowStatus:     engineResult.Status,
                WorkflowAdvanced:   true,
                TransitionedAtUtc:  taskResult.TransitionedAtUtc);
        });
    }
}
