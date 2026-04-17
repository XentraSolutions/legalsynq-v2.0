using Flow.Application.DTOs;
using Flow.Application.Exceptions;
using Flow.Application.Interfaces;
using Flow.Domain.Common;
using Flow.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Flow.Application.Services;

/// <summary>
/// LS-FLOW-E11.5 — default implementation of
/// <see cref="IMyTasksService"/>.
///
/// <para>
/// Single EF query: filters on
/// <c>(TenantId via query filter) + AssignedUserId == currentUserId
/// (+ optional Status IN (...))</c>, joins the owning <c>WorkflowInstance</c>
/// and its <c>FlowDefinition</c> via a projected LEFT JOIN to enrich the
/// response with <c>WorkflowName</c> and <c>ProductKey</c>, paginates,
/// and returns a <see cref="PagedResponse{T}"/>. Both the count and the
/// page slice run through the same predicate so the count is always
/// consistent with the returned items.
/// </para>
///
/// <para>
/// <b>Index usage:</b> the <c>(TenantId, AssignedUserId, Status)</c>
/// composite index shipped in E11.1 is the perfect cover for this
/// query — predicate columns are leftmost-prefix and the optional
/// status filter narrows the scan further.
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

    public async Task<PagedResponse<MyTaskDto>> ListMyTasksAsync(MyTasksQuery query, CancellationToken ct = default)
    {
        // ------- Identity / tenant resolution -------
        var userId = _user.UserId;
        if (string.IsNullOrWhiteSpace(userId))
        {
            // The endpoint is [Authorize]'d so this should not happen via
            // the normal HTTP pipeline; treat it as a hard validation
            // failure so the controller can map to 401/400 rather than
            // silently returning an empty (and misleading) page.
            throw new ValidationException("Authenticated user id is required to list My Tasks.");
        }

        // ------- Page-shape normalisation -------
        var page     = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize < 1
            ? MyTasksDefaults.DefaultPageSize
            : Math.Min(query.PageSize, MyTasksDefaults.MaxPageSize);

        // ------- Status filter normalisation -------
        // Accept multi-value status, drop blanks, dedupe ordinal-IC,
        // reject unknown values up-front so the caller gets a clear 400
        // rather than an empty result they cannot debug. Empty / null
        // input means "all statuses".
        IReadOnlyList<string>? statusFilter = null;
        if (query.Status is { Count: > 0 })
        {
            var cleaned = query.Status
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            // Canonicalise FIRST (case-insensitive), then validate.
            // WorkflowTaskStatus.IsKnown is a case-sensitive switch
            // expression, so calling it on the raw input would reject
            // valid case variants like "open" / "INPROGRESS". By mapping
            // to the on-disk casing up front we both (a) make the SQL
            // IN comparison match and (b) make IsKnown a reliable
            // backstop against truly unknown values.
            var canonicalised = cleaned
                .Select(s =>
                    s.Equals(WorkflowTaskStatus.Open,       StringComparison.OrdinalIgnoreCase) ? WorkflowTaskStatus.Open       :
                    s.Equals(WorkflowTaskStatus.InProgress, StringComparison.OrdinalIgnoreCase) ? WorkflowTaskStatus.InProgress :
                    s.Equals(WorkflowTaskStatus.Completed,  StringComparison.OrdinalIgnoreCase) ? WorkflowTaskStatus.Completed  :
                    s.Equals(WorkflowTaskStatus.Cancelled,  StringComparison.OrdinalIgnoreCase) ? WorkflowTaskStatus.Cancelled  :
                                                                                                  s) // unknown — preserved for the error message
                .ToArray();

            var unknown = canonicalised.Where(s => !WorkflowTaskStatus.IsKnown(s)).ToArray();
            if (unknown.Length > 0)
            {
                throw new ValidationException(
                    $"Unknown WorkflowTaskStatus value(s): {string.Join(", ", unknown)}. " +
                    $"Allowed: Open, InProgress, Completed, Cancelled.");
            }

            statusFilter = canonicalised;
        }

        // ------- Base predicate (tenant via query filter, user explicit) -------
        var baseQuery = _db.WorkflowTasks.AsNoTracking()
            .Where(t => t.AssignedUserId == userId);

        if (statusFilter is not null)
        {
            baseQuery = baseQuery.Where(t => statusFilter.Contains(t.Status));
        }

        // ------- Total count (same predicate, before paging) -------
        var totalCount = await baseQuery.CountAsync(ct);

        // ------- Page slice with deterministic ordering + workflow context join -------
        // Ordering rationale (see report §"Query / Filtering Notes"):
        //   1. Active first  — Open / InProgress sort before Completed /
        //      Cancelled. Encoded as `IsActive DESC` so true beats false.
        //   2. Recency       — UpdatedAt DESC, with CreatedAt fallback
        //      because UpdatedAt is null on never-modified rows.
        //   3. Stable tiebreaker — Id, so Skip/Take is reproducible
        //      across requests with identical timestamps (seed data,
        //      bulk inserts).
        var items = await baseQuery
            .OrderByDescending(t =>
                t.Status == WorkflowTaskStatus.Open ||
                t.Status == WorkflowTaskStatus.InProgress)
            .ThenByDescending(t => t.UpdatedAt ?? t.CreatedAt)
            .ThenBy(t => t.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new MyTaskDto
            {
                TaskId             = t.Id,
                Title              = t.Title,
                Description        = t.Description,
                Status             = t.Status,
                Priority           = t.Priority,
                StepKey            = t.StepKey,
                AssignedUserId     = t.AssignedUserId,
                CreatedAt          = t.CreatedAt,
                UpdatedAt          = t.UpdatedAt,
                StartedAt          = t.StartedAt,
                CompletedAt        = t.CompletedAt,
                CancelledAt        = t.CancelledAt,
                WorkflowInstanceId = t.WorkflowInstanceId,
                // LEFT-JOIN-style enrichment via navigation properties.
                // EF translates this into a single SQL with LEFT JOINs;
                // no N+1. WorkflowInstance and WorkflowDefinition are
                // both nullable so a missing definition yields nulls
                // rather than throwing.
                WorkflowName       = t.WorkflowInstance != null && t.WorkflowInstance.WorkflowDefinition != null
                                        ? t.WorkflowInstance.WorkflowDefinition.Name
                                        : null,
                ProductKey         = t.WorkflowInstance != null
                                        ? t.WorkflowInstance.ProductKey
                                        : null,
            })
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
}
