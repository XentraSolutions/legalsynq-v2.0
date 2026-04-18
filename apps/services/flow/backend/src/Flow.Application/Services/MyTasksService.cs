using System.Linq.Expressions;
using Flow.Application.DTOs;
using Flow.Application.Exceptions;
using Flow.Application.Interfaces;
using Flow.Domain.Common;
using Flow.Domain.Entities;
using Flow.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Flow.Application.Services;

/// <summary>
/// LS-FLOW-E11.5 / LS-FLOW-E15 — default implementation of
/// <see cref="IMyTasksService"/>.
///
/// <para>
/// E15 widened the surface from "list my direct tasks" to also include
/// "list the role-queue tasks I can claim", "list the org-queue tasks
/// I can claim", and "get a single task by id". All four queries share
/// the same <see cref="ProjectToDto"/> expression so the widened
/// <see cref="MyTaskDto"/> assignment fields are populated identically
/// everywhere — no chance of drift between list and detail.
/// </para>
///
/// <para>
/// <b>Tenant safety:</b> enforced by the global query filter on
/// <see cref="WorkflowTask"/>. Cross-tenant ids surface as
/// <see cref="NotFoundException"/> ⇒ 404, identical to a missing task.
/// </para>
///
/// <para>
/// <b>Eligibility filtering for queues:</b>
///   <list type="bullet">
///     <item>RoleQueue: <c>AssignedRole IN (caller.Roles)</c>. Platform
///       admins bypass this filter (they can see every queue for
///       support / on-call work).</item>
///     <item>OrgQueue: <c>AssignedOrgId = caller.OrgId</c>. Platform
///       admins bypass.</item>
///   </list>
/// Both queue queries are pinned to <c>Status = Open</c>: in our
/// model, only Open queue rows are claimable; an InProgress row has
/// already been claimed and would not benefit from being shown in the
/// queue surface.
/// </para>
/// </summary>
public sealed class MyTasksService : IMyTasksService
{
    private readonly IFlowDbContext _db;
    private readonly IFlowUserContext _user;
    private readonly ILogger<MyTasksService> _log;

    public MyTasksService(IFlowDbContext db, IFlowUserContext user, ILogger<MyTasksService> log)
    {
        _db = db;
        _user = user;
        _log = log;
    }

    // =========================================================
    // E11.5 — My direct tasks
    // =========================================================
    public async Task<PagedResponse<MyTaskDto>> ListMyTasksAsync(MyTasksQuery query, CancellationToken ct = default)
    {
        var userId = _user.UserId;
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ValidationException("Authenticated user id is required to list My Tasks.");
        }

        var (page, pageSize) = NormalizePage(query.Page, query.PageSize);
        var statusFilter = NormaliseStatusFilter(query.Status);

        var baseQuery = _db.WorkflowTasks.AsNoTracking()
            .Where(t => t.AssignedUserId == userId);

        if (statusFilter is not null)
        {
            baseQuery = baseQuery.Where(t => statusFilter.Contains(t.Status));
        }

        var totalCount = await baseQuery.CountAsync(ct);

        var items = await OrderActiveFirst(baseQuery)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ProjectToDto)
            .ToListAsync(ct);

        _log.LogDebug(
            "MyTasks query: UserId={UserId} Page={Page} PageSize={PageSize} StatusFilter={StatusFilter} Total={Total} Returned={Returned}",
            userId, page, pageSize,
            statusFilter is null ? "(all)" : string.Join(",", statusFilter),
            totalCount, items.Count);

        return new PagedResponse<MyTaskDto>
        {
            Items      = items,
            TotalCount = totalCount,
            Page       = page,
            PageSize   = pageSize,
        };
    }

    // =========================================================
    // E15 — Role Queue (claimable role-queue tasks for caller)
    // =========================================================
    public async Task<PagedResponse<MyTaskDto>> ListRoleQueueAsync(RoleQueueQuery query, CancellationToken ct = default)
    {
        // No caller-supplied role list — eligibility is always derived
        // from the auth context. This makes cross-role enumeration
        // impossible by API shape (not just by policy).
        var roles = _user.Roles;
        var isPlatformAdmin = _user.IsPlatformAdmin;

        var (page, pageSize) = NormalizePage(query.Page, query.PageSize);

        // Platform admins see all role-queue rows (still tenant-scoped).
        // Non-admins must hold at least one role; if they don't, the
        // result is necessarily empty — short-circuit so we don't spin
        // up a no-op SQL round-trip.
        if (!isPlatformAdmin && (roles is null || roles.Count == 0))
        {
            return EmptyPage(page, pageSize);
        }

        var baseQuery = _db.WorkflowTasks.AsNoTracking()
            .Where(t => t.AssignmentMode == WorkflowTaskAssignmentMode.RoleQueue
                     && t.Status == WorkflowTaskStatus.Open);

        if (!isPlatformAdmin)
        {
            // Materialise to a List<string> for EF's `Contains`
            // translation. AssignedRole is nullable; the IN list won't
            // include nulls so a row with a null role is naturally
            // excluded (which also happens to be our fail-closed
            // posture from E14.2).
            var rolesList = roles!.ToList();
            baseQuery = baseQuery.Where(t =>
                t.AssignedRole != null && rolesList.Contains(t.AssignedRole));
        }

        var totalCount = await baseQuery.CountAsync(ct);

        var items = await OrderActiveFirst(baseQuery)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ProjectToDto)
            .ToListAsync(ct);

        _log.LogDebug(
            "RoleQueue query: UserId={UserId} IsPlatformAdmin={IsAdmin} Roles={RoleCount} Page={Page}/{PageSize} Total={Total}",
            _user.UserId, isPlatformAdmin, roles?.Count ?? 0, page, pageSize, totalCount);

        return new PagedResponse<MyTaskDto>
        {
            Items      = items,
            TotalCount = totalCount,
            Page       = page,
            PageSize   = pageSize,
        };
    }

    // =========================================================
    // E15 — Org Queue (claimable org-queue tasks for caller)
    // =========================================================
    public async Task<PagedResponse<MyTaskDto>> ListOrgQueueAsync(OrgQueueQuery query, CancellationToken ct = default)
    {
        var orgId = _user.OrgId;
        var isPlatformAdmin = _user.IsPlatformAdmin;

        var (page, pageSize) = NormalizePage(query.Page, query.PageSize);

        if (!isPlatformAdmin && string.IsNullOrWhiteSpace(orgId))
        {
            return EmptyPage(page, pageSize);
        }

        var baseQuery = _db.WorkflowTasks.AsNoTracking()
            .Where(t => t.AssignmentMode == WorkflowTaskAssignmentMode.OrgQueue
                     && t.Status == WorkflowTaskStatus.Open);

        if (!isPlatformAdmin)
        {
            // Equality here intentionally — an OrgQueue row's
            // AssignedOrgId must match the caller's OrgId exactly.
            baseQuery = baseQuery.Where(t => t.AssignedOrgId == orgId);
        }

        var totalCount = await baseQuery.CountAsync(ct);

        var items = await OrderActiveFirst(baseQuery)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ProjectToDto)
            .ToListAsync(ct);

        _log.LogDebug(
            "OrgQueue query: UserId={UserId} OrgId={OrgId} IsPlatformAdmin={IsAdmin} Page={Page}/{PageSize} Total={Total}",
            _user.UserId, orgId, isPlatformAdmin, page, pageSize, totalCount);

        return new PagedResponse<MyTaskDto>
        {
            Items      = items,
            TotalCount = totalCount,
            Page       = page,
            PageSize   = pageSize,
        };
    }

    // =========================================================
    // E15 — Task detail (single row, widened DTO)
    // =========================================================
    /// <summary>
    /// Returns a single task by id, with eligibility-scoped
    /// authorisation.
    ///
    /// <para>
    /// <b>Eligibility rule</b> (a caller is allowed to read a task
    /// iff at least one of):
    ///   <list type="bullet">
    ///     <item>Platform admin (operations / on-call bypass).</item>
    ///     <item>The task is <c>DirectUser</c> assigned to the caller.</item>
    ///     <item>The task is an open <c>RoleQueue</c> for a role the caller holds.</item>
    ///     <item>The task is an open <c>OrgQueue</c> for the caller's org.</item>
    ///   </list>
    /// </para>
    ///
    /// <para>
    /// Without this guard a tenant user who learned a task GUID
    /// could read every task in the tenant (intra-tenant IDOR).
    /// We deliberately collapse "not found" and "not eligible" into
    /// a single <see cref="NotFoundException"/> so the response
    /// never leaks the difference between "no such id" and
    /// "exists but you can't see it".
    /// </para>
    /// </summary>
    public async Task<MyTaskDto> GetTaskDetailAsync(Guid taskId, CancellationToken ct = default)
    {
        var userId          = _user.UserId;
        var orgId           = _user.OrgId;
        var isPlatformAdmin = _user.IsPlatformAdmin;
        var roles           = _user.Roles ?? Array.Empty<string>();
        var rolesList       = roles.ToList();

        // Tenant filter is global on WorkflowTask, so a wrong-tenant
        // id naturally yields null (= NotFoundException). On top of
        // that, encode the eligibility rule as a SQL predicate so a
        // not-allowed row also yields null and surfaces as 404 — no
        // existence-leak.
        var query = _db.WorkflowTasks.AsNoTracking()
            .Where(t => t.Id == taskId);

        if (!isPlatformAdmin)
        {
            query = query.Where(t =>
                // Direct assignment to the caller.
                (t.AssignedUserId != null && t.AssignedUserId == userId)
                // Open role-queue task for a role the caller holds.
                || (t.AssignmentMode == WorkflowTaskAssignmentMode.RoleQueue
                    && t.Status == WorkflowTaskStatus.Open
                    && t.AssignedRole != null
                    && rolesList.Contains(t.AssignedRole))
                // Open org-queue task for the caller's org.
                || (t.AssignmentMode == WorkflowTaskAssignmentMode.OrgQueue
                    && t.Status == WorkflowTaskStatus.Open
                    && t.AssignedOrgId != null
                    && t.AssignedOrgId == orgId));
        }

        var dto = await query.Select(ProjectToDto).FirstOrDefaultAsync(ct);

        if (dto is null)
            throw new NotFoundException(nameof(WorkflowTask), taskId);

        return dto;
    }

    // =========================================================
    // Shared helpers
    // =========================================================

    /// <summary>
    /// Single source of truth for the <see cref="WorkflowTask"/> →
    /// <see cref="MyTaskDto"/> projection. Materialised as an
    /// expression so it can be reused inside multiple
    /// <c>IQueryable.Select</c> calls — EF translates it to SQL
    /// identically each time, so the four surfaces can never drift.
    /// </summary>
    private static readonly Expression<Func<WorkflowTask, MyTaskDto>> ProjectToDto = t => new MyTaskDto
    {
        TaskId             = t.Id,
        Title              = t.Title,
        Description        = t.Description,
        Status             = t.Status,
        Priority           = t.Priority,
        StepKey            = t.StepKey,

        AssignmentMode     = t.AssignmentMode,
        AssignedUserId     = t.AssignedUserId,
        AssignedRole       = t.AssignedRole,
        AssignedOrgId      = t.AssignedOrgId,
        AssignedAt         = t.AssignedAt,
        AssignedBy         = t.AssignedBy,
        AssignmentReason   = t.AssignmentReason,

        CreatedAt          = t.CreatedAt,
        UpdatedAt          = t.UpdatedAt,
        StartedAt          = t.StartedAt,
        CompletedAt        = t.CompletedAt,
        CancelledAt        = t.CancelledAt,

        // LS-FLOW-E10.3 task slice — SLA fields. Defaulting SlaStatus
        // to OnTrack when null is defensive: the column is NOT NULL in
        // the schema, but EF projection through navigation properties
        // would otherwise need an explicit non-null guarantee.
        DueAt              = t.DueAt,
        SlaStatus          = t.SlaStatus,
        SlaBreachedAt      = t.SlaBreachedAt,

        WorkflowInstanceId = t.WorkflowInstanceId,
        // LEFT-JOIN-style enrichment via navigation properties; EF
        // translates to a single SQL with LEFT JOINs (no N+1).
        WorkflowName       = t.WorkflowInstance != null && t.WorkflowInstance.WorkflowDefinition != null
                                ? t.WorkflowInstance.WorkflowDefinition.Name
                                : null,
        ProductKey         = t.WorkflowInstance != null
                                ? t.WorkflowInstance.ProductKey
                                : null,
    };

    /// <summary>
    /// LS-FLOW-E18 — shared deterministic ordering that places the most
    /// urgent work at the top of every queue and task list surface.
    ///
    /// <para><b>Documented sort hierarchy (innermost → outermost):</b></para>
    /// <list type="number">
    ///   <item>Active tasks (Open / InProgress) before terminal ones.</item>
    ///   <item>SLA urgency tier — Escalated (0) &gt; Overdue (1) &gt; DueSoon (2)
    ///     &gt; OnTrack (3) &gt; unknown/null (4).</item>
    ///   <item>Priority tier — Urgent (0) &gt; High (1) &gt; Normal (2) &gt; Low (3).</item>
    ///   <item>DueAt ascending, nulls last — earlier deadline first; items with no deadline
    ///     sort after items that have one.</item>
    ///   <item>CreatedAt ascending — older items before newer within the same urgency.</item>
    ///   <item>Id ascending — stable, deterministic tiebreaker; no randomness.</item>
    /// </list>
    ///
    /// <para>
    /// Invariant: no task may outrank a more urgent task solely because of
    /// recency. An Overdue-Normal task always appears above an OnTrack-Urgent
    /// task. Within the same SLA+Priority bucket, earlier deadlines surface
    /// first.
    /// </para>
    ///
    /// <para>
    /// EF Core translates the conditional expressions below to SQL
    /// <c>CASE WHEN … THEN … END</c> clauses; MySQL evaluates them
    /// efficiently on the tenant-scoped index.
    /// </para>
    /// </summary>
    private static IQueryable<WorkflowTask> OrderActiveFirst(IQueryable<WorkflowTask> q) =>
        q
        // 1. Active (Open/InProgress) before terminal (Completed/Cancelled).
        .OrderBy(t =>
            t.Status == WorkflowTaskStatus.Open ||
            t.Status == WorkflowTaskStatus.InProgress ? 0 : 1)
        // 2. SLA urgency tier (ascending = more urgent first).
        .ThenBy(t =>
            t.SlaStatus == WorkflowSlaStatus.Escalated ? 0 :
            t.SlaStatus == WorkflowSlaStatus.Overdue   ? 1 :
            t.SlaStatus == WorkflowSlaStatus.DueSoon   ? 2 :
            t.SlaStatus == WorkflowSlaStatus.OnTrack   ? 3 : 4)
        // 3. Priority tier (ascending = higher priority first).
        .ThenBy(t =>
            t.Priority == WorkflowTaskPriority.Urgent ? 0 :
            t.Priority == WorkflowTaskPriority.High   ? 1 :
            t.Priority == WorkflowTaskPriority.Normal ? 2 :
            t.Priority == WorkflowTaskPriority.Low    ? 3 : 2)
        // 4. DueAt nulls last — items without a deadline sort after items with one.
        .ThenBy(t => t.DueAt == null ? 1 : 0)
        // 5. DueAt ascending — earliest deadline first among items that have one.
        .ThenBy(t => t.DueAt)
        // 6. CreatedAt ascending — older items before newer within the same urgency band.
        .ThenBy(t => t.CreatedAt)
        // 7. Id ascending — perfectly stable tiebreaker; no randomness, no instability.
        .ThenBy(t => t.Id);

    private static (int Page, int PageSize) NormalizePage(int page, int pageSize)
    {
        var p = page < 1 ? 1 : page;
        var ps = pageSize < 1
            ? MyTasksDefaults.DefaultPageSize
            : Math.Min(pageSize, MyTasksDefaults.MaxPageSize);
        return (p, ps);
    }

    private static PagedResponse<MyTaskDto> EmptyPage(int page, int pageSize) => new()
    {
        Items      = Array.Empty<MyTaskDto>(),
        TotalCount = 0,
        Page       = page,
        PageSize   = pageSize,
    };

    private static IReadOnlyList<string>? NormaliseStatusFilter(IReadOnlyList<string>? status)
    {
        if (status is not { Count: > 0 }) return null;

        var cleaned = status
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var canonicalised = cleaned
            .Select(s =>
                s.Equals(WorkflowTaskStatus.Open,       StringComparison.OrdinalIgnoreCase) ? WorkflowTaskStatus.Open       :
                s.Equals(WorkflowTaskStatus.InProgress, StringComparison.OrdinalIgnoreCase) ? WorkflowTaskStatus.InProgress :
                s.Equals(WorkflowTaskStatus.Completed,  StringComparison.OrdinalIgnoreCase) ? WorkflowTaskStatus.Completed  :
                s.Equals(WorkflowTaskStatus.Cancelled,  StringComparison.OrdinalIgnoreCase) ? WorkflowTaskStatus.Cancelled  :
                                                                                              s)
            .ToArray();

        var unknown = canonicalised.Where(s => !WorkflowTaskStatus.IsKnown(s)).ToArray();
        if (unknown.Length > 0)
        {
            throw new ValidationException(
                $"Unknown WorkflowTaskStatus value(s): {string.Join(", ", unknown)}. " +
                $"Allowed: Open, InProgress, Completed, Cancelled.");
        }

        return canonicalised;
    }
}
