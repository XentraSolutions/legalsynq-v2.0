using Flow.Application.Exceptions;
using Flow.Application.Interfaces;
using Flow.Domain.Common;
using Flow.Domain.Entities;
using Flow.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Flow.Application.Services;

/// <summary>
/// LS-FLOW-E11.4 — default implementation of
/// <see cref="IWorkflowTaskLifecycleService"/>.
///
/// <para>
/// <b>Persistence pattern:</b> a two-step pre-check + atomic
/// compare-and-swap. The pre-check (an <c>AsNoTracking</c> read through
/// the global tenant query filter) gives callers a precise error
/// (<see cref="NotFoundException"/> vs.
/// <see cref="InvalidStateTransitionException"/>) instead of a bare
/// "0 rows affected" failure. The conditional UPDATE
/// (<c>ExecuteUpdateAsync</c> with <c>WHERE Id = … AND Status = expected</c>)
/// is the actual concurrency primitive: it returns 1 on success and 0
/// when another caller has already moved the task, in which case we
/// raise <see cref="WorkflowTaskConcurrencyException"/>.
/// </para>
///
/// <para>
/// <b>Why <c>ExecuteUpdateAsync</c> rather than load-and-track:</b>
/// <list type="bullet">
///   <item>Atomic in a single round-trip — no need to add a RowVersion
///         column or a new EF migration.</item>
///   <item>Honours the existing tenant <c>HasQueryFilter</c> on
///         <see cref="WorkflowTask"/> automatically (the underlying
///         <c>IQueryable</c> goes through the filter).</item>
///   <item>Cannot accidentally flush unrelated tracked changes — this
///         service is laser-focused on a single row.</item>
/// </list>
/// The trade-off is that EF's save-hook (audit-column stamping,
/// <c>EnsureValid</c>) does NOT run for these updates. We compensate
/// explicitly: this service sets <c>UpdatedAt</c> and <c>UpdatedBy</c>
/// in the <c>SetProperty</c> chain, and re-asserts the same status /
/// terminal-timestamp invariants <see cref="WorkflowTask.EnsureValid"/>
/// enforces.
/// </para>
///
/// <para>
/// <b>Out of scope (intentionally not touched):</b> WorkflowInstance,
/// WorkflowEngine, outbox, SLA, notifications, assignment columns
/// (<c>AssignedUserId</c> / <c>AssignedRole</c> / <c>AssignedOrgId</c> /
/// <c>AssignmentMode</c> / <c>AssignedAt</c> / <c>AssignedBy</c> /
/// <c>AssignmentReason</c>), reassignment.
/// </para>
///
/// <para>
/// <b>E14.1 caveat — bypass of assignment-mode invariants.</b> Because
/// <c>ExecuteUpdateAsync</c> skips the save-hook, the new single-mode
/// invariants enforced by <see cref="WorkflowTask.EnsureValid"/>
/// (<c>DirectUser</c> / <c>RoleQueue</c> / <c>OrgQueue</c> /
/// <c>Unassigned</c> consistency between <c>AssignmentMode</c> and the
/// assignment-target columns) are NOT re-checked here. This service is
/// currently safe because it only mutates status / lifecycle
/// timestamps and never touches the assignment columns. Any future
/// helper that uses <c>ExecuteUpdateAsync</c> to mutate assignment
/// columns MUST either (a) load + track + <c>SaveChangesAsync</c> so
/// the save-hook fires, or (b) re-apply the same single-mode rule
/// inline before issuing the UPDATE.
/// </para>
/// </summary>
public sealed class WorkflowTaskLifecycleService : IWorkflowTaskLifecycleService
{
    private readonly IFlowDbContext _db;
    private readonly IFlowUserContext _user;
    private readonly ILogger<WorkflowTaskLifecycleService> _log;

    public WorkflowTaskLifecycleService(
        IFlowDbContext db,
        IFlowUserContext user,
        ILogger<WorkflowTaskLifecycleService> log)
    {
        _db = db;
        _user = user;
        _log = log;
    }

    public Task<WorkflowTaskTransitionResult> StartTaskAsync(Guid taskId, CancellationToken ct = default) =>
        TransitionAsync(
            taskId,
            expectedStatus: WorkflowTaskStatus.Open,
            newStatus: WorkflowTaskStatus.InProgress,
            timestampField: TimestampField.Started,
            ct);

    public Task<WorkflowTaskTransitionResult> CompleteTaskAsync(Guid taskId, CancellationToken ct = default) =>
        TransitionAsync(
            taskId,
            expectedStatus: WorkflowTaskStatus.InProgress,
            newStatus: WorkflowTaskStatus.Completed,
            timestampField: TimestampField.Completed,
            ct);

    public async Task<WorkflowTaskTransitionResult> CancelTaskAsync(Guid taskId, CancellationToken ct = default)
    {
        // Cancel is the only transition with two valid source states
        // (Open and InProgress). Single read drives both the validation
        // (terminal-state rejection) and the chosen `expected` for the
        // CAS. We must NOT re-read inside ApplyCasAsync — if the row
        // races from Open to InProgress between the read and the
        // UPDATE, that interleaving is *contention* (InProgress →
        // Cancelled is itself valid), not a "wrong source state". The
        // CAS will fail with affected==0 and surface as
        // WorkflowTaskConcurrencyException, which is the correct
        // taxonomy — caller may safely retry with a fresh read.
        var current = await ReadCurrentStatusAsync(taskId, ct);
        if (current is not (WorkflowTaskStatus.Open or WorkflowTaskStatus.InProgress))
        {
            throw new InvalidStateTransitionException(current, WorkflowTaskStatus.Cancelled);
        }

        return await ApplyCasAsync(
            taskId,
            expectedStatus: current,
            newStatus: WorkflowTaskStatus.Cancelled,
            timestampField: TimestampField.Cancelled,
            ct);
    }

    // ---------------- internals -----------------

    private enum TimestampField { Started, Completed, Cancelled }

    /// <summary>
    /// Pre-check + atomic CAS for the single-source transitions
    /// (Start, Complete). Cancel does its own pre-check because it has
    /// two valid source states and must classify a between-read race as
    /// concurrency rather than as an invalid source.
    /// </summary>
    private async Task<WorkflowTaskTransitionResult> TransitionAsync(
        Guid taskId,
        string expectedStatus,
        string newStatus,
        TimestampField timestampField,
        CancellationToken ct)
    {
        var current = await ReadCurrentStatusAsync(taskId, ct);
        if (!string.Equals(current, expectedStatus, StringComparison.Ordinal))
        {
            // Pre-check fail: the row is in a state from which this
            // single-source transition is never valid (e.g. Complete
            // called against an Open or terminal task). Reported as a
            // source-state error, not a concurrency error.
            throw new InvalidStateTransitionException(current, newStatus);
        }

        return await ApplyCasAsync(taskId, expectedStatus, newStatus, timestampField, ct);
    }

    /// <summary>
    /// Pure atomic compare-and-swap with NO pre-check. Used both as the
    /// final step of <see cref="TransitionAsync"/> and directly by
    /// <see cref="CancelTaskAsync"/>'s two-source path so a
    /// between-read race surfaces as
    /// <see cref="WorkflowTaskConcurrencyException"/> (caller-retryable)
    /// rather than as a misclassified
    /// <see cref="InvalidStateTransitionException"/>.
    /// </summary>
    private async Task<WorkflowTaskTransitionResult> ApplyCasAsync(
        Guid taskId,
        string expectedStatus,
        string newStatus,
        TimestampField timestampField,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var updatedBy = _user.UserId;

        var affected = timestampField switch
        {
            TimestampField.Started => await _db.WorkflowTasks
                .Where(t => t.Id == taskId && t.Status == expectedStatus)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(t => t.Status, newStatus)
                    // First start sets StartedAt; a subsequent re-entry
                    // would not reach this branch (Open is single-source
                    // for InProgress) but we still preserve any existing
                    // value defensively.
                    .SetProperty(t => t.StartedAt, t => t.StartedAt ?? now)
                    .SetProperty(t => t.UpdatedAt, now)
                    .SetProperty(t => t.UpdatedBy, t => updatedBy ?? t.UpdatedBy), ct),

            TimestampField.Completed => await _db.WorkflowTasks
                .Where(t => t.Id == taskId && t.Status == expectedStatus)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(t => t.Status, newStatus)
                    .SetProperty(t => t.CompletedAt, (DateTime?)now)
                    .SetProperty(t => t.UpdatedAt, now)
                    .SetProperty(t => t.UpdatedBy, t => updatedBy ?? t.UpdatedBy), ct),

            TimestampField.Cancelled => await _db.WorkflowTasks
                .Where(t => t.Id == taskId && t.Status == expectedStatus)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(t => t.Status, newStatus)
                    .SetProperty(t => t.CancelledAt, (DateTime?)now)
                    .SetProperty(t => t.UpdatedAt, now)
                    .SetProperty(t => t.UpdatedBy, t => updatedBy ?? t.UpdatedBy), ct),

            _ => throw new InvalidOperationException($"Unknown timestamp field: {timestampField}"),
        };

        if (affected == 0)
        {
            // Pre-check passed but the conditional UPDATE matched nothing
            // → the row was mutated between the read and the write.
            throw new WorkflowTaskConcurrencyException(taskId, expectedStatus);
        }

        _log.LogInformation(
            "WorkflowTask lifecycle transition: TaskId={TaskId} {From}→{To}",
            taskId, expectedStatus, newStatus);

        return new WorkflowTaskTransitionResult(taskId, expectedStatus, newStatus, now);
    }

    /// <summary>
    /// Existence + tenant-scoped read of the current status. Returns the
    /// status string on success; throws <see cref="NotFoundException"/>
    /// when the task does not exist OR is owned by a different tenant
    /// (the global query filter cannot tell the two cases apart, and
    /// surfacing them identically prevents cross-tenant id probing).
    /// </summary>
    private async Task<string> ReadCurrentStatusAsync(Guid taskId, CancellationToken ct)
    {
        var status = await _db.WorkflowTasks
            .AsNoTracking()
            .Where(t => t.Id == taskId)
            .Select(t => t.Status)
            .FirstOrDefaultAsync(ct);

        if (status is null)
        {
            throw new NotFoundException(nameof(WorkflowTask), taskId);
        }

        return status;
    }
}
