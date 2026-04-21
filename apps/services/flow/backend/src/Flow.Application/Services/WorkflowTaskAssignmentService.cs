using Flow.Application.Adapters.AuditAdapter;
using Flow.Application.Exceptions;
using Flow.Application.Interfaces;
using Flow.Domain.Common;
using Flow.Domain.Entities;
using Flow.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Flow.Application.Services;

/// <summary>
/// LS-FLOW-E14.2 — sole entry point for user-driven assignment
/// transitions on <see cref="WorkflowTask"/>. Implements the
/// claim/reassign governance described in the E14.2 spec.
///
/// <para>
/// <b>Persistence pattern.</b> Same shape as
/// <see cref="WorkflowTaskLifecycleService"/>: a tenant-scoped
/// <c>AsNoTracking</c> read for pre-validation + a single
/// <c>ExecuteUpdateAsync</c> as the atomic write. The CAS WHERE
/// clause includes the previous (status, mode, user, role, org)
/// tuple — a competing claim or reassign that lands between our
/// read and our write necessarily changes at least one of those
/// columns and therefore cannot be silently overwritten; the
/// race-loser sees affected==0 and we throw
/// <see cref="WorkflowTaskConcurrencyException"/> ⇒ 409.
/// </para>
///
/// <para>
/// <b>E14.1 invariant re-application.</b> <c>ExecuteUpdateAsync</c>
/// bypasses <c>SaveChangesAsync</c>'s save-hook, so the canonical
/// <see cref="WorkflowTask.EnsureValid"/> single-mode rule does NOT
/// fire here. We instead construct the target tuple deterministically
/// from validated input AND re-assert the same single-mode rule
/// inline (<see cref="EnsureSingleModeShape"/>) before issuing the
/// UPDATE. This is the contract the E14.1 caveat docs in
/// <see cref="WorkflowTaskLifecycleService"/> ask of any future
/// helper that mutates assignment columns.
/// </para>
///
/// <para>
/// <b>Authorization model.</b>
///   <list type="bullet">
///     <item><b>Claim</b>: any authenticated user with a tenant.
///       Eligibility for the source queue is checked here against
///       <see cref="IFlowUserContext.Roles"/> for <c>RoleQueue</c>
///       and against <see cref="IFlowUserContext.OrgId"/> for
///       <c>OrgQueue</c>. <c>DirectUser</c> source rejected
///       (already assigned). <c>Unassigned</c> source rejected
///       (use reassign).</item>
///     <item><b>Reassign</b>: gated at the controller layer
///       (<c>Policies.PlatformOrTenantAdmin</c>) and re-asserted
///       here (<see cref="EnsureCallerIsAdmin"/>) so the service
///       remains authoritative if it is ever wired to a controller
///       with a more lax policy.</item>
///   </list>
/// </para>
///
/// <para>
/// <b>Audit.</b> Best-effort emission of
/// <c>workflow.task.claim</c> / <c>workflow.task.reassign</c> via
/// <see cref="IAuditAdapter"/>. Per the adapter's "fire-and-forget
/// safe" contract, audit failures are logged and swallowed so the
/// user-visible operation is not undone by an audit-pipeline outage.
/// </para>
/// </summary>
public sealed class WorkflowTaskAssignmentService : IWorkflowTaskAssignmentService
{
    private const int MaxReasonLength = 500;
    private const string DefaultClaimReason = "claimed from queue";

    // Role names recognised as supervisor authority for reassign.
    // Mirrored from BuildingBlocks.Authorization.Roles; duplicated
    // here as constants because Flow.Application deliberately does
    // not reference BuildingBlocks. If the platform renames these
    // role keys, both places must move together.
    private const string RolePlatformAdmin = "PlatformAdmin";
    private const string RoleTenantAdmin   = "TenantAdmin";

    private readonly IFlowDbContext _db;
    private readonly IFlowUserContext _user;
    private readonly IAuditAdapter _audit;
    // TASK-FLOW-01 — Task service client for dual-write delegation.
    private readonly IFlowTaskServiceClient _taskClient;
    private readonly ILogger<WorkflowTaskAssignmentService> _log;

    public WorkflowTaskAssignmentService(
        IFlowDbContext db,
        IFlowUserContext user,
        IAuditAdapter audit,
        IFlowTaskServiceClient taskClient,
        ILogger<WorkflowTaskAssignmentService> log)
    {
        _db = db;
        _user = user;
        _audit = audit;
        _taskClient = taskClient;
        _log = log;
    }

    // ====================== CLAIM ======================

    public async Task<WorkflowTaskAssignmentResult> ClaimAsync(
        Guid taskId,
        string? reason,
        CancellationToken ct = default)
    {
        var callerUserId = _user.UserId
            ?? throw new AssignmentForbiddenException(
                WorkflowTaskAssignmentErrorCodes.ForbiddenAssignmentAction,
                "Claim requires an authenticated caller.");

        var snapshot = await ReadSnapshotAsync(taskId, ct);

        // 1. State guard — only Open is claimable. InProgress means
        //    someone has already started; use reassign for redirect.
        if (!string.Equals(snapshot.Status, WorkflowTaskStatus.Open, StringComparison.Ordinal))
        {
            throw new AssignmentRuleException(
                WorkflowTaskAssignmentErrorCodes.TaskStateInvalid,
                $"Task status '{snapshot.Status}' is not claimable. Only 'Open' tasks can be claimed.");
        }

        // 2. Source-mode guard.
        switch (snapshot.AssignmentMode)
        {
            case WorkflowTaskAssignmentMode.DirectUser:
                throw new AssignmentRuleException(
                    WorkflowTaskAssignmentErrorCodes.TaskAlreadyAssigned,
                    string.Equals(snapshot.AssignedUserId, callerUserId, StringComparison.OrdinalIgnoreCase)
                        ? "Task is already directly assigned to you."
                        : "Task is already directly assigned to another user.");

            case WorkflowTaskAssignmentMode.Unassigned:
                throw new AssignmentRuleException(
                    WorkflowTaskAssignmentErrorCodes.TaskNotClaimable,
                    "Unassigned tasks cannot be self-claimed. An administrator must reassign the task first.");

            case WorkflowTaskAssignmentMode.RoleQueue:
                // Note: pass the nullable snapshot field directly.
                // EnsureCallerHoldsRole fails closed on a missing
                // role rather than treating "no role required" as a
                // free-for-all (defence against malformed rows from
                // legacy / hand-edited data).
                EnsureCallerHoldsRole(snapshot.AssignedRole);
                break;

            case WorkflowTaskAssignmentMode.OrgQueue:
                EnsureCallerInOrg(snapshot.AssignedOrgId);
                break;

            default:
                // Defence-in-depth — the row should never have an
                // unknown mode (E14.1 invariant), but guard anyway.
                throw new AssignmentRuleException(
                    WorkflowTaskAssignmentErrorCodes.AssignmentModeInvalid,
                    $"Task has an unknown AssignmentMode '{snapshot.AssignmentMode}'.");
        }

        // 3. Build target tuple — DirectUser to caller.
        var target = new AssignmentTarget(
            Mode: WorkflowTaskAssignmentMode.DirectUser,
            UserId: callerUserId,
            Role: null,
            OrgId: null);

        var trimmedReason = NormalizeReason(reason) ?? DefaultClaimReason;

        return await ApplyTransitionAsync(
            snapshot,
            target,
            trimmedReason,
            auditAction: "workflow.task.claim",
            auditDescription: $"Task claimed from {snapshot.AssignmentMode}",
            ct);
    }

    // ===================== REASSIGN =====================

    public async Task<WorkflowTaskAssignmentResult> ReassignAsync(
        Guid taskId,
        ReassignTaskRequest request,
        CancellationToken ct = default)
    {
        if (request is null)
            throw new AssignmentRuleException(
                WorkflowTaskAssignmentErrorCodes.AssignmentTargetInvalid,
                "Reassign request body is required.");

        EnsureCallerIsAdmin();

        // Reason is required for reassign — claim has implicit
        // semantics, reassign must always carry an explanation.
        var reason = NormalizeReason(request.Reason)
            ?? throw new AssignmentRuleException(
                WorkflowTaskAssignmentErrorCodes.MissingAssignmentReason,
                "Reassignment requires a non-empty reason.");

        var targetMode = (request.TargetMode ?? string.Empty).Trim();
        if (!WorkflowTaskAssignmentMode.IsKnown(targetMode))
        {
            throw new AssignmentRuleException(
                WorkflowTaskAssignmentErrorCodes.AssignmentModeInvalid,
                $"Target mode '{request.TargetMode}' is not recognised. " +
                $"Allowed: DirectUser, RoleQueue, OrgQueue, Unassigned.");
        }

        var target = BuildAndValidateReassignTarget(targetMode, request);

        var snapshot = await ReadSnapshotAsync(taskId, ct);

        // State guard — Open or InProgress only. Terminal rejected.
        if (WorkflowTaskStatus.IsTerminal(snapshot.Status))
        {
            throw new AssignmentRuleException(
                WorkflowTaskAssignmentErrorCodes.TaskStateInvalid,
                $"Task status '{snapshot.Status}' is terminal and cannot be reassigned.");
        }
        if (!string.Equals(snapshot.Status, WorkflowTaskStatus.Open, StringComparison.Ordinal) &&
            !string.Equals(snapshot.Status, WorkflowTaskStatus.InProgress, StringComparison.Ordinal))
        {
            // Defensive — IsTerminal already covers Completed/Cancelled
            // but a future status (Blocked, OnHold, …) would land here
            // until policy is decided.
            throw new AssignmentRuleException(
                WorkflowTaskAssignmentErrorCodes.TaskNotReassignable,
                $"Task status '{snapshot.Status}' is not reassignable.");
        }

        return await ApplyTransitionAsync(
            snapshot,
            target,
            reason,
            auditAction: "workflow.task.reassign",
            auditDescription: $"Task reassigned {snapshot.AssignmentMode} → {target.Mode}",
            ct);
    }

    // =================== Internals: read + write ===================

    private async Task<TaskSnapshot> ReadSnapshotAsync(Guid taskId, CancellationToken ct)
    {
        // Tenant filter is global on WorkflowTask, so a wrong-tenant
        // id naturally yields null (= NotFoundException) — no special
        // cross-tenant branch needed and no id leakage.
        var snapshot = await _db.WorkflowTasks
            .AsNoTracking()
            .Where(t => t.Id == taskId)
            .Select(t => new TaskSnapshot(
                t.Id,
                t.WorkflowInstanceId,
                t.Status,
                t.AssignmentMode,
                t.AssignedUserId,
                t.AssignedRole,
                t.AssignedOrgId))
            .FirstOrDefaultAsync(ct);

        if (snapshot is null)
            throw new NotFoundException(nameof(WorkflowTask), taskId);

        return snapshot;
    }

    /// <summary>
    /// Atomic CAS write + audit. Shared by claim and reassign so the
    /// stamping rules and concurrency primitive live in exactly one
    /// place.
    /// </summary>
    private async Task<WorkflowTaskAssignmentResult> ApplyTransitionAsync(
        TaskSnapshot snapshot,
        AssignmentTarget target,
        string? reason,
        string auditAction,
        string auditDescription,
        CancellationToken ct)
    {
        // Re-assert E14.1 single-mode shape on the target we are
        // about to persist. EnsureValid will not run for
        // ExecuteUpdateAsync, so this is the canonical guard.
        EnsureSingleModeShape(target);

        var now = DateTime.UtcNow;
        var actor = _user.UserId;

        // For Unassigned, AssignedAt / AssignedBy / AssignmentReason
        // MUST be null per the E14.1 invariant — there is no
        // assignment event to record on the row itself. The audit
        // event still captures who performed the un-assignment and
        // why (reason is still required at the request layer for
        // reassign, optional for claim — but claim never targets
        // Unassigned).
        var isUnassignedTarget = target.Mode == WorkflowTaskAssignmentMode.Unassigned;
        DateTime? assignedAtForRow = isUnassignedTarget ? null : now;
        string? assignedByForRow = isUnassignedTarget ? null : actor;
        string? reasonForRow = isUnassignedTarget ? null : reason;

        // Capture loop-local snapshot values for the EF expression
        // tree (Where clause). EF cannot translate property accesses
        // on a record from the outer scope inside ExecuteUpdateAsync.
        var taskId = snapshot.Id;
        var prevStatus = snapshot.Status;
        var prevMode = snapshot.AssignmentMode;
        var prevUserId = snapshot.AssignedUserId;
        var prevRole = snapshot.AssignedRole;
        var prevOrgId = snapshot.AssignedOrgId;

        var newMode = target.Mode;
        var newUserId = target.UserId;
        var newRole = target.Role;
        var newOrgId = target.OrgId;

        // TASK-FLOW-02 — delegate assignment to Task service for ALL modes
        // (DirectUser, RoleQueue, OrgQueue, Unassigned) via the internal
        // flow-queue-assign endpoint (service token auth).
        // Phase 1 limitation of DirectUser-only is now removed.
        Guid? assignedUserGuid = null;
        if (!string.IsNullOrWhiteSpace(newUserId))
        {
            if (Guid.TryParse(newUserId, out var parsed))
                assignedUserGuid = parsed;
            else
                _log.LogWarning(
                    "WorkflowTaskAssignmentService: AssignedUserId '{UserId}' is not a valid Guid — assignedUserId will be null in Task service.",
                    newUserId);
        }

        Guid tenantGuid = Guid.Empty;
        if (!string.IsNullOrWhiteSpace(_user.TenantId) && !Guid.TryParse(_user.TenantId, out tenantGuid))
        {
            _log.LogWarning(
                "WorkflowTaskAssignmentService: TenantId '{TenantId}' is not a valid Guid — queue assignment to Task service skipped.",
                _user.TenantId);
        }

        if (tenantGuid != Guid.Empty)
        {
            try
            {
                await _taskClient.SetQueueAssignmentAsync(
                    tenantId:        tenantGuid,
                    taskId:          taskId,
                    assignmentMode:  newMode,
                    assignedUserId:  assignedUserGuid,
                    assignedRole:    newRole,
                    assignedOrgId:   newOrgId,
                    assignedBy:      actor,
                    assignmentReason: reasonForRow,
                    ct:              ct);
            }
            catch (Exception ex)
            {
                _log.LogError(ex,
                    "WorkflowTaskAssignmentService: Task service SetQueueAssignment FAILED for task {TaskId} mode={Mode}. Shadow CAS skipped; propagating error.",
                    taskId, newMode);
                throw;
            }
        }

        var affected = await _db.WorkflowTasks
            .Where(t => t.Id == taskId
                     && t.Status == prevStatus
                     && t.AssignmentMode == prevMode
                     && t.AssignedUserId == prevUserId
                     && t.AssignedRole == prevRole
                     && t.AssignedOrgId == prevOrgId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.AssignmentMode, newMode)
                .SetProperty(t => t.AssignedUserId, newUserId)
                .SetProperty(t => t.AssignedRole, newRole)
                .SetProperty(t => t.AssignedOrgId, newOrgId)
                .SetProperty(t => t.AssignedAt, assignedAtForRow)
                .SetProperty(t => t.AssignedBy, assignedByForRow)
                .SetProperty(t => t.AssignmentReason, reasonForRow)
                .SetProperty(t => t.UpdatedAt, now)
                .SetProperty(t => t.UpdatedBy, t => actor ?? t.UpdatedBy), ct);

        if (affected == 0)
        {
            // CAS lost — another claim/reassign landed between our
            // read and our write. Status is the field
            // WorkflowTaskConcurrencyException already advertises;
            // any of the five guarded columns could have changed,
            // but Status is the most actionable for the caller.
            throw new WorkflowTaskConcurrencyException(taskId, prevStatus);
        }

        _log.LogInformation(
            "WorkflowTask assignment transition: TaskId={TaskId} {PrevMode}→{NewMode} (Action={Action}, By={By})",
            taskId, prevMode, newMode, auditAction, actor);

        await EmitAuditAsync(
            taskId,
            snapshot.WorkflowInstanceId,
            prevMode,
            prevUserId,
            prevRole,
            prevOrgId,
            target,
            reason,
            auditAction,
            auditDescription,
            occurredAtUtc: now,
            ct);

        return new WorkflowTaskAssignmentResult(
            TaskId: taskId,
            WorkflowInstanceId: snapshot.WorkflowInstanceId,
            Status: prevStatus,
            AssignmentMode: target.Mode,
            AssignedUserId: target.UserId,
            AssignedRole: target.Role,
            AssignedOrgId: target.OrgId,
            AssignedAt: assignedAtForRow,
            AssignedBy: assignedByForRow,
            AssignmentReason: reasonForRow,
            OccurredAtUtc: now);
    }

    // ============== Internals: validation helpers ==============

    /// <summary>
    /// Re-asserts <see cref="WorkflowTask.EnsureValid"/>'s single-mode
    /// rule against a constructed target. Belt-and-braces: the public
    /// methods build the target deterministically, but this is the
    /// last guard before persistence and never gets bypassed.
    /// </summary>
    private static void EnsureSingleModeShape(AssignmentTarget target)
    {
        if (!WorkflowTaskAssignmentMode.IsKnown(target.Mode))
            throw new AssignmentRuleException(
                WorkflowTaskAssignmentErrorCodes.AssignmentModeInvalid,
                $"Target mode '{target.Mode}' is not a known mode.");

        switch (target.Mode)
        {
            case WorkflowTaskAssignmentMode.DirectUser:
                if (string.IsNullOrWhiteSpace(target.UserId) ||
                    target.Role is not null || target.OrgId is not null)
                    throw new AssignmentRuleException(
                        WorkflowTaskAssignmentErrorCodes.AssignmentTargetInvalid,
                        "DirectUser target requires AssignedUserId and forbids AssignedRole / AssignedOrgId.");
                break;

            case WorkflowTaskAssignmentMode.RoleQueue:
                if (string.IsNullOrWhiteSpace(target.Role) ||
                    target.UserId is not null || target.OrgId is not null)
                    throw new AssignmentRuleException(
                        WorkflowTaskAssignmentErrorCodes.AssignmentTargetInvalid,
                        "RoleQueue target requires AssignedRole and forbids AssignedUserId / AssignedOrgId.");
                break;

            case WorkflowTaskAssignmentMode.OrgQueue:
                if (string.IsNullOrWhiteSpace(target.OrgId) ||
                    target.UserId is not null || target.Role is not null)
                    throw new AssignmentRuleException(
                        WorkflowTaskAssignmentErrorCodes.AssignmentTargetInvalid,
                        "OrgQueue target requires AssignedOrgId and forbids AssignedUserId / AssignedRole.");
                break;

            case WorkflowTaskAssignmentMode.Unassigned:
                if (target.UserId is not null || target.Role is not null || target.OrgId is not null)
                    throw new AssignmentRuleException(
                        WorkflowTaskAssignmentErrorCodes.AssignmentTargetInvalid,
                        "Unassigned target forbids AssignedUserId / AssignedRole / AssignedOrgId.");
                break;
        }
    }

    private static AssignmentTarget BuildAndValidateReassignTarget(
        string targetMode,
        ReassignTaskRequest req)
    {
        // Trim string fields once so empty-string and whitespace
        // payloads are normalised to null and don't accidentally pass
        // an "is set" check.
        var userId = NullIfBlank(req.AssignedUserId);
        var role = NullIfBlank(req.AssignedRole);
        var orgId = NullIfBlank(req.AssignedOrgId);

        return new AssignmentTarget(targetMode, userId, role, orgId);
    }

    private void EnsureCallerHoldsRole(string? requiredRole)
    {
        // Fail closed on malformed queue rows. A RoleQueue task with
        // a blank AssignedRole is a data-integrity violation (the
        // E14.1 save-hook would never write one, but migration
        // drift / hand-edited rows / future producers could). We
        // refuse to claim it rather than treat the absent role as
        // "no role required" — the latter would let any
        // authenticated user grab it.
        if (string.IsNullOrWhiteSpace(requiredRole))
        {
            throw new AssignmentRuleException(
                WorkflowTaskAssignmentErrorCodes.AssignmentTargetInvalid,
                "Task is in RoleQueue mode but its AssignedRole is missing; cannot evaluate claim eligibility.");
        }

        var heldByCaller = _user.Roles
            .Any(r => string.Equals(r, requiredRole, StringComparison.OrdinalIgnoreCase));

        // Platform admins are explicitly allowed to act as any
        // queue eligible — convenience for support / on-call work.
        if (!heldByCaller && !_user.IsPlatformAdmin)
        {
            throw new AssignmentForbiddenException(
                WorkflowTaskAssignmentErrorCodes.ForbiddenAssignmentAction,
                $"Caller does not hold the role '{requiredRole}' required to claim this task.");
        }
    }

    private void EnsureCallerInOrg(string? requiredOrgId)
    {
        // Same fail-closed rationale as EnsureCallerHoldsRole — see
        // the comment there. A blank AssignedOrgId on an OrgQueue
        // task is a malformed row, not "any org may claim".
        if (string.IsNullOrWhiteSpace(requiredOrgId))
        {
            throw new AssignmentRuleException(
                WorkflowTaskAssignmentErrorCodes.AssignmentTargetInvalid,
                "Task is in OrgQueue mode but its AssignedOrgId is missing; cannot evaluate claim eligibility.");
        }

        var callerOrg = _user.OrgId;
        var matches = !string.IsNullOrWhiteSpace(callerOrg)
            && string.Equals(callerOrg, requiredOrgId, StringComparison.OrdinalIgnoreCase);

        if (!matches && !_user.IsPlatformAdmin)
        {
            throw new AssignmentForbiddenException(
                WorkflowTaskAssignmentErrorCodes.ForbiddenAssignmentAction,
                "Caller is not a member of the organization that owns this task queue.");
        }
    }

    private void EnsureCallerIsAdmin()
    {
        var isAdmin = _user.IsPlatformAdmin
            || _user.Roles.Any(r =>
                string.Equals(r, RolePlatformAdmin, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(r, RoleTenantAdmin, StringComparison.OrdinalIgnoreCase));

        if (!isAdmin)
        {
            throw new AssignmentForbiddenException(
                WorkflowTaskAssignmentErrorCodes.ForbiddenAssignmentAction,
                "Reassign requires platform-admin or tenant-admin authority.");
        }
    }

    private static string? NormalizeReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) return null;
        var trimmed = reason.Trim();
        // Reject rather than truncate. Silent truncation would let a
        // 10kb dump through as a 500-char prefix and the audit
        // record would lie about what the caller actually said.
        // Better to fail loudly so the client either shortens or
        // moves the detail to a linked artefact.
        if (trimmed.Length > MaxReasonLength)
        {
            throw new AssignmentRuleException(
                WorkflowTaskAssignmentErrorCodes.AssignmentTargetInvalid,
                $"Reason is too long ({trimmed.Length} chars). Maximum is {MaxReasonLength}.");
        }
        return trimmed;
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    // ===================== Internals: audit =====================

    private async Task EmitAuditAsync(
        Guid taskId,
        Guid workflowInstanceId,
        string prevMode,
        string? prevUserId,
        string? prevRole,
        string? prevOrgId,
        AssignmentTarget target,
        string? reason,
        string action,
        string description,
        DateTime occurredAtUtc,
        CancellationToken ct)
    {
        try
        {
            var metadata = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["taskId"] = taskId.ToString("D"),
                ["workflowInstanceId"] = workflowInstanceId.ToString("D"),
                ["prevMode"] = prevMode,
                ["prevAssignedUserId"] = prevUserId,
                ["prevAssignedRole"] = prevRole,
                ["prevAssignedOrgId"] = prevOrgId,
                ["newMode"] = target.Mode,
                ["newAssignedUserId"] = target.UserId,
                ["newAssignedRole"] = target.Role,
                ["newAssignedOrgId"] = target.OrgId,
                ["reason"] = reason,
                ["performedBy"] = _user.UserId,
            };

            var evt = new AuditEvent(
                Action: action,
                EntityType: nameof(WorkflowTask),
                EntityId: taskId.ToString("D"),
                TenantId: _user.TenantId,
                UserId: _user.UserId,
                Description: description,
                Metadata: metadata,
                OccurredAtUtc: occurredAtUtc);

            await _audit.WriteEventAsync(evt, ct);
        }
        catch (Exception ex)
        {
            // IAuditAdapter is documented as fire-and-forget safe:
            // a downstream audit-pipeline outage MUST NOT undo a
            // successful assignment transition. Log and move on.
            _log.LogWarning(ex,
                "Audit emission failed for {Action} on TaskId={TaskId}; persisted state is unaffected.",
                action, taskId);
        }
    }

    // ===================== Internal records =====================

    private sealed record TaskSnapshot(
        Guid Id,
        Guid WorkflowInstanceId,
        string Status,
        string AssignmentMode,
        string? AssignedUserId,
        string? AssignedRole,
        string? AssignedOrgId);

    private sealed record AssignmentTarget(
        string Mode,
        string? UserId,
        string? Role,
        string? OrgId);
}
