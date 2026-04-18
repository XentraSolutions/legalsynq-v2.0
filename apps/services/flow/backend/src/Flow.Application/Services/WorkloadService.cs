using Flow.Application.Interfaces;
using Flow.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Flow.Application.Services;

/// <summary>
/// LS-FLOW-E18 — default implementation of <see cref="IWorkloadService"/>.
///
/// <para>
/// All queries are <c>AsNoTracking</c> and use the global EF tenant
/// query filter on <c>WorkflowTask</c>, ensuring cross-tenant counts
/// are impossible by construction.
/// </para>
///
/// <para>
/// <b>Performance:</b> <see cref="GetActiveTaskCountsAsync"/> issues a
/// single SQL <c>GROUP BY AssignedUserId</c> with an IN-clause on the
/// supplied user ids — no N+1, no full-table scan beyond the tenant
/// slice. Candidate derivation methods also emit single SQL queries
/// capped by <paramref name="maxResults"/> via <c>Take</c>.
/// </para>
/// </summary>
public sealed class WorkloadService : IWorkloadService
{
    private readonly IFlowDbContext _db;
    private readonly ILogger<WorkloadService> _log;

    // Statuses that count toward a user's active workload.
    private static readonly string[] ActiveStatuses =
    [
        WorkflowTaskStatus.Open,
        WorkflowTaskStatus.InProgress,
    ];

    public WorkloadService(IFlowDbContext db, ILogger<WorkloadService> log)
    {
        _db  = db;
        _log = log;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, int>> GetActiveTaskCountsAsync(
        IEnumerable<string> userIds,
        CancellationToken ct = default)
    {
        var ids = userIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (ids.Count == 0)
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        // Single SQL GROUP BY — EF translates IEnumerable.Contains to
        // an IN clause with parameterised values.
        var counts = await _db.WorkflowTasks
            .AsNoTracking()
            .Where(t =>
                t.AssignedUserId != null
                && ids.Contains(t.AssignedUserId)
                && ActiveStatuses.Contains(t.Status))
            .GroupBy(t => t.AssignedUserId!)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        _log.LogDebug(
            "WorkloadService.GetActiveTaskCountsAsync: queried {Requested} users, " +
            "found workload for {WithLoad} users",
            ids.Count, counts.Count);

        return counts.ToDictionary(
            x => x.UserId,
            x => x.Count,
            StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetUserIdsForRoleAsync(
        string assignedRole,
        int maxResults,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(assignedRole))
            return Array.Empty<string>();

        // Derive candidates: users who have at least one active task
        // associated with this role.  "Active" is defined as Status ∈
        // {Open, InProgress} AND AssignedUserId IS NOT NULL.
        // This is not a complete user-directory lookup — see E18 report
        // §Assumptions for the documented limitation.
        var ids = await _db.WorkflowTasks
            .AsNoTracking()
            .Where(t =>
                t.AssignedRole == assignedRole
                && t.AssignedUserId != null
                && ActiveStatuses.Contains(t.Status))
            .Select(t => t.AssignedUserId!)
            .Distinct()
            .Take(maxResults)
            .ToListAsync(ct);

        _log.LogDebug(
            "WorkloadService.GetUserIdsForRoleAsync: role={Role} → {Count} candidate(s)",
            assignedRole, ids.Count);

        return ids;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetUserIdsForOrgAsync(
        string assignedOrgId,
        int maxResults,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(assignedOrgId))
            return Array.Empty<string>();

        var ids = await _db.WorkflowTasks
            .AsNoTracking()
            .Where(t =>
                t.AssignedOrgId == assignedOrgId
                && t.AssignedUserId != null
                && ActiveStatuses.Contains(t.Status))
            .Select(t => t.AssignedUserId!)
            .Distinct()
            .Take(maxResults)
            .ToListAsync(ct);

        _log.LogDebug(
            "WorkloadService.GetUserIdsForOrgAsync: orgId={OrgId} → {Count} candidate(s)",
            assignedOrgId, ids.Count);

        return ids;
    }
}
