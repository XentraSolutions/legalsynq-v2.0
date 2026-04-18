using Flow.Application.DTOs;
using Flow.Application.Interfaces;
using Flow.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Flow.Application.Services;

/// <summary>
/// E19 — default implementation of <see cref="IFlowAnalyticsService"/>.
///
/// <para>
/// All queries are <c>AsNoTracking</c> read-only. The EF global tenant query
/// filter applies unless explicitly bypassed (platform summary only).
/// Each method issues the minimum number of SQL queries — typically one
/// per domain via GROUP BY or a single WHERE scan over the tenant slice.
/// </para>
///
/// <para>
/// Metric definitions and source-of-truth are documented on each DTO;
/// this service enforces the same semantics in its WHERE clauses.
/// </para>
/// </summary>
public sealed class FlowAnalyticsService : IFlowAnalyticsService
{
    private const int OverloadThreshold     = 10;
    private const int MaxQueueBreakdown     = 20;
    private const int MaxTopOverdueQueues   = 10;
    private const int MaxTopAssignees       = 20;
    private const int MaxPlatformTenants    = 20;
    private const int MaxProductBreakdown   = 10;

    // Statuses that represent live (non-terminal) tasks.
    private static readonly string[] ActiveStatuses =
    [
        WorkflowTaskStatus.Open,
        WorkflowTaskStatus.InProgress,
    ];

    private readonly IFlowDbContext _db;
    private readonly ILogger<FlowAnalyticsService> _log;

    public FlowAnalyticsService(IFlowDbContext db, ILogger<FlowAnalyticsService> log)
    {
        _db  = db;
        _log = log;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static (DateTime start, DateTime end, string label) WindowBounds(AnalyticsWindow window)
    {
        var now = DateTime.UtcNow;
        var start = window switch
        {
            AnalyticsWindow.Today      => now.Date,
            AnalyticsWindow.Last7Days  => now.AddDays(-7),
            AnalyticsWindow.Last30Days => now.AddDays(-30),
            _                          => now.AddDays(-7),
        };
        var label = window switch
        {
            AnalyticsWindow.Today      => "Today",
            AnalyticsWindow.Last7Days  => "Last 7 Days",
            AnalyticsWindow.Last30Days => "Last 30 Days",
            _                          => "Last 7 Days",
        };
        return (start, now, label);
    }

    // ── Dashboard Summary ─────────────────────────────────────────────────────

    public async Task<AnalyticsDashboardSummaryDto> GetDashboardSummaryAsync(
        AnalyticsWindow window,
        CancellationToken ct = default)
    {
        var (start, end, label) = WindowBounds(window);

        var (sla, queue, throughput, assignment, outbox) = (
            await GetSlaSummaryAsync(window, ct),
            await GetQueueSummaryAsync(ct),
            await GetWorkflowThroughputAsync(window, ct),
            await GetAssignmentSummaryAsync(window, ct),
            await GetOutboxAnalyticsAsync(window, ct)
        );

        return new AnalyticsDashboardSummaryDto
        {
            Sla         = sla,
            Queue       = queue,
            Workflows   = throughput,
            Assignment  = assignment,
            Outbox      = outbox,
            GeneratedAt = end,
            WindowLabel = label,
        };
    }

    // ── SLA Analytics ─────────────────────────────────────────────────────────

    public async Task<SlaSummaryDto> GetSlaSummaryAsync(
        AnalyticsWindow window,
        CancellationToken ct = default)
    {
        var (start, end, label) = WindowBounds(window);
        var now = end;

        // Active task SLA breakdown — single GROUP BY query
        var slaGroups = await _db.WorkflowTasks
            .AsNoTracking()
            .Where(t => ActiveStatuses.Contains(t.Status))
            .GroupBy(t => t.SlaStatus)
            .Select(g => new { SlaStatus = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var onTrack  = slaGroups.FirstOrDefault(g => g.SlaStatus == WorkflowSlaStatus.OnTrack)?.Count  ?? 0;
        var atRisk   = slaGroups.FirstOrDefault(g => g.SlaStatus == WorkflowSlaStatus.DueSoon)?.Count  ?? 0;
        var overdue  = slaGroups.FirstOrDefault(g => g.SlaStatus == WorkflowSlaStatus.Overdue)?.Count  ?? 0;
        var total    = onTrack + atRisk + overdue;

        // Avg overdue age (SlaBreachedAt → now)
        double? avgOverdueAgeDays = null;
        if (overdue > 0)
        {
            // Pull SlaBreachedAt for overdue tasks to compute average age in-process
            // (EF cannot compute TimeSpan arithmetic directly in all providers)
            var breachDates = await _db.WorkflowTasks
                .AsNoTracking()
                .Where(t => ActiveStatuses.Contains(t.Status)
                         && t.SlaStatus == WorkflowSlaStatus.Overdue
                         && t.SlaBreachedAt != null)
                .Select(t => t.SlaBreachedAt!.Value)
                .ToListAsync(ct);

            if (breachDates.Count > 0)
                avgOverdueAgeDays = breachDates.Average(d => (now - d).TotalDays);
        }

        // Window: tasks where SlaBreachedAt is within window
        var breachedInWindow = await _db.WorkflowTasks
            .AsNoTracking()
            .CountAsync(t => t.SlaBreachedAt >= start && t.SlaBreachedAt <= now, ct);

        // Window: tasks completed on-time in window
        var completedInWindow = await _db.WorkflowTasks
            .AsNoTracking()
            .Where(t => t.Status == WorkflowTaskStatus.Completed
                     && t.CompletedAt >= start && t.CompletedAt <= now)
            .CountAsync(ct);

        var completedOnTime = await _db.WorkflowTasks
            .AsNoTracking()
            .Where(t => t.Status == WorkflowTaskStatus.Completed
                     && t.CompletedAt >= start && t.CompletedAt <= now
                     && t.SlaStatus == WorkflowSlaStatus.OnTrack)
            .CountAsync(ct);

        // Top overdue queues
        var roleOverdue = await _db.WorkflowTasks
            .AsNoTracking()
            .Where(t => ActiveStatuses.Contains(t.Status)
                     && t.SlaStatus == WorkflowSlaStatus.Overdue
                     && t.AssignmentMode == WorkflowTaskAssignmentMode.RoleQueue
                     && t.AssignedRole != null)
            .GroupBy(t => t.AssignedRole!)
            .Select(g => new QueueOverdueBreakdownDto
            {
                QueueKey   = g.Key,
                QueueType  = "Role",
                OverdueCount = g.Count(),
            })
            .OrderByDescending(x => x.OverdueCount)
            .Take(MaxTopOverdueQueues / 2)
            .ToListAsync(ct);

        var orgOverdue = await _db.WorkflowTasks
            .AsNoTracking()
            .Where(t => ActiveStatuses.Contains(t.Status)
                     && t.SlaStatus == WorkflowSlaStatus.Overdue
                     && t.AssignmentMode == WorkflowTaskAssignmentMode.OrgQueue
                     && t.AssignedOrgId != null)
            .GroupBy(t => t.AssignedOrgId!)
            .Select(g => new QueueOverdueBreakdownDto
            {
                QueueKey   = g.Key,
                QueueType  = "Org",
                OverdueCount = g.Count(),
            })
            .OrderByDescending(x => x.OverdueCount)
            .Take(MaxTopOverdueQueues / 2)
            .ToListAsync(ct);

        var topOverdueQueues = roleOverdue
            .Concat(orgOverdue)
            .OrderByDescending(x => x.OverdueCount)
            .Take(MaxTopOverdueQueues)
            .ToList();

        return new SlaSummaryDto
        {
            ActiveOnTrackCount     = onTrack,
            ActiveAtRiskCount      = atRisk,
            ActiveOverdueCount     = overdue,
            TotalActiveCount       = total,
            OverduePercentage      = total > 0 ? Math.Round((double)overdue / total * 100, 1) : 0,
            BreachedInWindow       = breachedInWindow,
            CompletedOnTimeInWindow = completedOnTime,
            CompletedInWindow      = completedInWindow,
            AvgOverdueAgeDays      = avgOverdueAgeDays.HasValue
                                        ? Math.Round(avgOverdueAgeDays.Value, 2)
                                        : null,
            WindowStart            = start,
            WindowEnd              = now,
            WindowLabel            = label,
            TopOverdueQueues       = topOverdueQueues,
        };
    }

    // ── Queue / Workload Analytics ────────────────────────────────────────────

    public async Task<QueueSummaryDto> GetQueueSummaryAsync(
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        // Role queue backlog breakdown
        var roleGroups = await _db.WorkflowTasks
            .AsNoTracking()
            .Where(t => ActiveStatuses.Contains(t.Status)
                     && t.AssignmentMode == WorkflowTaskAssignmentMode.RoleQueue
                     && t.AssignedRole != null)
            .GroupBy(t => new { t.AssignedRole, t.Status, t.SlaStatus })
            .Select(g => new { g.Key.AssignedRole, g.Key.Status, g.Key.SlaStatus, Count = g.Count() })
            .ToListAsync(ct);

        var roleQueueBreakdown = roleGroups
            .GroupBy(x => x.AssignedRole!)
            .Select(grp => new RoleQueueBacklogDto
            {
                Role           = grp.Key,
                OpenCount      = grp.Where(x => x.Status == WorkflowTaskStatus.Open).Sum(x => x.Count),
                InProgressCount = grp.Where(x => x.Status == WorkflowTaskStatus.InProgress).Sum(x => x.Count),
                TotalCount     = grp.Sum(x => x.Count),
                OverdueCount   = grp.Where(x => x.SlaStatus == WorkflowSlaStatus.Overdue).Sum(x => x.Count),
            })
            .OrderByDescending(x => x.TotalCount)
            .Take(MaxQueueBreakdown)
            .ToList();

        // Org queue backlog breakdown
        var orgGroups = await _db.WorkflowTasks
            .AsNoTracking()
            .Where(t => ActiveStatuses.Contains(t.Status)
                     && t.AssignmentMode == WorkflowTaskAssignmentMode.OrgQueue
                     && t.AssignedOrgId != null)
            .GroupBy(t => new { t.AssignedOrgId, t.Status, t.SlaStatus })
            .Select(g => new { g.Key.AssignedOrgId, g.Key.Status, g.Key.SlaStatus, Count = g.Count() })
            .ToListAsync(ct);

        var orgQueueBreakdown = orgGroups
            .GroupBy(x => x.AssignedOrgId!)
            .Select(grp => new OrgQueueBacklogDto
            {
                OrgId          = grp.Key,
                OpenCount      = grp.Where(x => x.Status == WorkflowTaskStatus.Open).Sum(x => x.Count),
                InProgressCount = grp.Where(x => x.Status == WorkflowTaskStatus.InProgress).Sum(x => x.Count),
                TotalCount     = grp.Sum(x => x.Count),
                OverdueCount   = grp.Where(x => x.SlaStatus == WorkflowSlaStatus.Overdue).Sum(x => x.Count),
            })
            .OrderByDescending(x => x.TotalCount)
            .Take(MaxQueueBreakdown)
            .ToList();

        var roleBacklog      = roleQueueBreakdown.Sum(x => x.TotalCount);
        var orgBacklog       = orgQueueBreakdown.Sum(x => x.TotalCount);
        var unassignedBacklog = await _db.WorkflowTasks
            .AsNoTracking()
            .CountAsync(t => ActiveStatuses.Contains(t.Status)
                          && t.AssignmentMode == WorkflowTaskAssignmentMode.Unassigned, ct);

        // Queue age for non-DirectUser tasks — pull CreatedAt for bounded set
        var queuedCreatedAts = await _db.WorkflowTasks
            .AsNoTracking()
            .Where(t => ActiveStatuses.Contains(t.Status)
                     && t.AssignmentMode != WorkflowTaskAssignmentMode.DirectUser)
            .Select(t => t.CreatedAt)
            .ToListAsync(ct);

        double? oldestAgeHours = null;
        double? medianAgeHours = null;
        if (queuedCreatedAts.Count > 0)
        {
            var ages = queuedCreatedAts.Select(c => (now - c).TotalHours).OrderBy(h => h).ToList();
            oldestAgeHours = Math.Round(ages.Last(), 2);
            medianAgeHours = Math.Round(ages[ages.Count / 2], 2);
        }

        // Active tasks per user
        var userWorkloads = await _db.WorkflowTasks
            .AsNoTracking()
            .Where(t => ActiveStatuses.Contains(t.Status) && t.AssignedUserId != null)
            .GroupBy(t => t.AssignedUserId!)
            .Select(g => g.Count())
            .ToListAsync(ct);

        var activeUserCount    = userWorkloads.Count;
        var overloadedCount    = userWorkloads.Count(c => c >= OverloadThreshold);

        return new QueueSummaryDto
        {
            RoleQueueBacklog        = roleBacklog,
            OrgQueueBacklog         = orgBacklog,
            UnassignedBacklog       = unassignedBacklog,
            OldestQueuedTaskAgeHours = oldestAgeHours,
            MedianQueueAgeHours     = medianAgeHours,
            ActiveUserCount         = activeUserCount,
            OverloadedUserCount     = overloadedCount,
            OverloadThreshold       = OverloadThreshold,
            RoleQueueBreakdown      = roleQueueBreakdown,
            OrgQueueBreakdown       = orgQueueBreakdown,
            AsOf                    = now,
        };
    }

    // ── Workflow Throughput ───────────────────────────────────────────────────

    public async Task<WorkflowThroughputDto> GetWorkflowThroughputAsync(
        AnalyticsWindow window,
        CancellationToken ct = default)
    {
        var (start, end, label) = WindowBounds(window);

        var started   = await _db.WorkflowInstances.AsNoTracking()
            .CountAsync(i => i.CreatedAt >= start && i.CreatedAt <= end, ct);

        var completed = await _db.WorkflowInstances.AsNoTracking()
            .CountAsync(i => i.Status == "Completed"
                          && i.CompletedAt >= start && i.CompletedAt <= end, ct);

        var cancelled = await _db.WorkflowInstances.AsNoTracking()
            .CountAsync(i => i.Status == "Cancelled"
                          && i.UpdatedAt >= start && i.UpdatedAt <= end, ct);

        var failed = await _db.WorkflowInstances.AsNoTracking()
            .CountAsync(i => i.Status == "Failed"
                          && i.UpdatedAt >= start && i.UpdatedAt <= end, ct);

        var activeCount = await _db.WorkflowInstances.AsNoTracking()
            .CountAsync(i => i.Status == "Active", ct);

        // Cycle time — only for instances completed in window with valid timestamps
        var cycleDates = await _db.WorkflowInstances
            .AsNoTracking()
            .Where(i => i.Status == "Completed"
                     && i.CompletedAt >= start && i.CompletedAt <= end
                     && i.CreatedAt != default)
            .Select(i => new { i.CreatedAt, CompletedAt = i.CompletedAt!.Value })
            .ToListAsync(ct);

        double? avgCycleHours    = null;
        double? medianCycleHours = null;
        if (cycleDates.Count > 0)
        {
            var hours = cycleDates
                .Select(x => (x.CompletedAt - x.CreatedAt).TotalHours)
                .Where(h => h >= 0)
                .OrderBy(h => h)
                .ToList();
            if (hours.Count > 0)
            {
                avgCycleHours    = Math.Round(hours.Average(), 2);
                medianCycleHours = Math.Round(hours[hours.Count / 2], 2);
            }
        }

        // By-product breakdown
        var productGroups = await _db.WorkflowInstances
            .AsNoTracking()
            .Where(i => i.CreatedAt >= start && i.CreatedAt <= end)
            .GroupBy(i => i.ProductKey)
            .Select(g => new WorkflowProductBreakdownDto
            {
                ProductKey     = g.Key,
                StartedCount   = g.Count(),
                CompletedCount = g.Count(x => x.Status == "Completed"),
                ActiveCount    = g.Count(x => x.Status == "Active"),
            })
            .OrderByDescending(x => x.StartedCount)
            .Take(MaxProductBreakdown)
            .ToListAsync(ct);

        return new WorkflowThroughputDto
        {
            StartedInWindow      = started,
            CompletedInWindow    = completed,
            CancelledInWindow    = cancelled,
            FailedInWindow       = failed,
            CurrentlyActiveCount = activeCount,
            AvgCycleTimeHours    = avgCycleHours,
            MedianCycleTimeHours = medianCycleHours,
            ByProduct            = productGroups,
            WindowStart          = start,
            WindowEnd            = end,
            WindowLabel          = label,
        };
    }

    // ── Assignment Analytics ──────────────────────────────────────────────────

    public async Task<AssignmentSummaryDto> GetAssignmentSummaryAsync(
        AnalyticsWindow window,
        CancellationToken ct = default)
    {
        var (start, end, label) = WindowBounds(window);

        // Current mode distribution (all statuses)
        var modeGroups = await _db.WorkflowTasks
            .AsNoTracking()
            .GroupBy(t => t.AssignmentMode)
            .Select(g => new { Mode = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var directUser  = modeGroups.FirstOrDefault(g => g.Mode == WorkflowTaskAssignmentMode.DirectUser)?.Count  ?? 0;
        var roleQueue   = modeGroups.FirstOrDefault(g => g.Mode == WorkflowTaskAssignmentMode.RoleQueue)?.Count   ?? 0;
        var orgQueue    = modeGroups.FirstOrDefault(g => g.Mode == WorkflowTaskAssignmentMode.OrgQueue)?.Count    ?? 0;
        var unassigned  = modeGroups.FirstOrDefault(g => g.Mode == WorkflowTaskAssignmentMode.Unassigned)?.Count  ?? 0;

        // Assigned in window (AssignedAt within window)
        var assignedInWindow = await _db.WorkflowTasks
            .AsNoTracking()
            .CountAsync(t => t.AssignedAt >= start && t.AssignedAt <= end, ct);

        // Top assignees by active load
        var topAssignees = await _db.WorkflowTasks
            .AsNoTracking()
            .Where(t => ActiveStatuses.Contains(t.Status) && t.AssignedUserId != null)
            .GroupBy(t => new { t.AssignedUserId, t.Status })
            .Select(g => new { g.Key.AssignedUserId, g.Key.Status, Count = g.Count() })
            .ToListAsync(ct);

        var userWorkload = topAssignees
            .GroupBy(x => x.AssignedUserId!)
            .Select(grp => new UserWorkloadDto
            {
                UserId          = grp.Key,
                ActiveTaskCount = grp.Sum(x => x.Count),
                OpenCount       = grp.Where(x => x.Status == WorkflowTaskStatus.Open).Sum(x => x.Count),
                InProgressCount = grp.Where(x => x.Status == WorkflowTaskStatus.InProgress).Sum(x => x.Count),
            })
            .OrderByDescending(x => x.ActiveTaskCount)
            .Take(MaxTopAssignees)
            .ToList();

        return new AssignmentSummaryDto
        {
            DirectUserCount             = directUser,
            RoleQueueCount              = roleQueue,
            OrgQueueCount               = orgQueue,
            UnassignedCount             = unassigned,
            AssignedInWindow            = assignedInWindow,
            TopAssigneesByActiveLoad    = userWorkload,
            WindowStart                 = start,
            WindowEnd                   = end,
            WindowLabel                 = label,
        };
    }

    // ── Outbox Analytics ──────────────────────────────────────────────────────

    public async Task<OutboxAnalyticsSummaryDto> GetOutboxAnalyticsAsync(
        AnalyticsWindow window,
        CancellationToken ct = default)
    {
        var (start, end, label) = WindowBounds(window);
        var now = end;

        // Current-state counts (bypass global query filter — OutboxProcessor runs
        // in null-tenant scope; consistent with AdminOutboxController pattern)
        var statusGroups = await _db.OutboxMessages
            .IgnoreQueryFilters()
            .AsNoTracking()
            .GroupBy(m => m.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        int Cnt(string s) => statusGroups.FirstOrDefault(g => g.Status == s)?.Count ?? 0;
        var pending     = Cnt(OutboxStatus.Pending);
        var processing  = Cnt(OutboxStatus.Processing);
        var failed      = Cnt(OutboxStatus.Failed);
        var deadLetter  = Cnt(OutboxStatus.DeadLettered);
        var succeeded   = Cnt(OutboxStatus.Succeeded);

        // Window-scoped counts
        var createdInWindow = await _db.OutboxMessages.IgnoreQueryFilters().AsNoTracking()
            .CountAsync(m => m.CreatedAt >= start && m.CreatedAt <= now, ct);

        var succeededInWindow = await _db.OutboxMessages.IgnoreQueryFilters().AsNoTracking()
            .CountAsync(m => m.Status == OutboxStatus.Succeeded
                          && m.ProcessedAt >= start && m.ProcessedAt <= now, ct);

        var failedInWindow = await _db.OutboxMessages.IgnoreQueryFilters().AsNoTracking()
            .CountAsync(m => m.Status == OutboxStatus.Failed
                          && m.ProcessedAt >= start && m.ProcessedAt <= now, ct);

        var deadLetteredInWindow = await _db.OutboxMessages.IgnoreQueryFilters().AsNoTracking()
            .CountAsync(m => m.Status == OutboxStatus.DeadLettered
                          && m.ProcessedAt >= start && m.ProcessedAt <= now, ct);

        // Failed + dead-lettered by event type
        var failedByType = await _db.OutboxMessages
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(m => m.Status == OutboxStatus.Failed || m.Status == OutboxStatus.DeadLettered)
            .GroupBy(m => new { m.EventType, m.Status })
            .Select(g => new { g.Key.EventType, g.Key.Status, Count = g.Count() })
            .ToListAsync(ct);

        var byEventType = failedByType
            .GroupBy(x => x.EventType)
            .Select(grp => new OutboxEventTypeBreakdownDto
            {
                EventType      = grp.Key,
                FailedCount    = grp.Where(x => x.Status == OutboxStatus.Failed).Sum(x => x.Count),
                DeadLettered   = grp.Where(x => x.Status == OutboxStatus.DeadLettered).Sum(x => x.Count),
                TotalUnhealthy = grp.Sum(x => x.Count),
            })
            .OrderByDescending(x => x.TotalUnhealthy)
            .Take(20)
            .ToList();

        return new OutboxAnalyticsSummaryDto
        {
            PendingCount         = pending,
            ProcessingCount      = processing,
            FailedCount          = failed,
            DeadLetteredCount    = deadLetter,
            SucceededCount       = succeeded,
            UnhealthyCount       = pending + processing + failed + deadLetter,
            CreatedInWindow      = createdInWindow,
            SucceededInWindow    = succeededInWindow,
            FailedInWindow       = failedInWindow,
            DeadLetteredInWindow = deadLetteredInWindow,
            FailedByEventType    = byEventType,
            WindowStart          = start,
            WindowEnd            = now,
            WindowLabel          = label,
            AsOf                 = now,
        };
    }

    // ── Platform Summary (cross-tenant) ──────────────────────────────────────

    public async Task<PlatformAnalyticsSummaryDto> GetPlatformSummaryAsync(
        AnalyticsWindow window,
        CancellationToken ct = default)
    {
        var (start, end, label) = WindowBounds(window);
        var now = end;

        // Cross-tenant active workflows
        var totalActiveWorkflows = await _db.WorkflowInstances
            .IgnoreQueryFilters()
            .AsNoTracking()
            .CountAsync(i => i.Status == "Active", ct);

        // Cross-tenant active + overdue tasks
        var taskStatusSla = await _db.WorkflowTasks
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(t => ActiveStatuses.Contains(t.Status))
            .GroupBy(t => t.SlaStatus)
            .Select(g => new { SlaStatus = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var totalActiveTasks = taskStatusSla.Sum(g => g.Count);
        var totalOverdue     = taskStatusSla.FirstOrDefault(g => g.SlaStatus == WorkflowSlaStatus.Overdue)?.Count ?? 0;

        // Cross-tenant outbox
        var outboxGroups = await _db.OutboxMessages
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(m => m.Status == OutboxStatus.Failed || m.Status == OutboxStatus.DeadLettered)
            .GroupBy(m => m.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var totalFailed      = outboxGroups.FirstOrDefault(g => g.Status == OutboxStatus.Failed)?.Count ?? 0;
        var totalDeadLettered = outboxGroups.FirstOrDefault(g => g.Status == OutboxStatus.DeadLettered)?.Count ?? 0;

        // Top tenants by overdue rate
        var tenantOverdue = await _db.WorkflowTasks
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(t => ActiveStatuses.Contains(t.Status))
            .GroupBy(t => new { t.TenantId, t.SlaStatus })
            .Select(g => new { g.Key.TenantId, g.Key.SlaStatus, Count = g.Count() })
            .ToListAsync(ct);

        var topByOverdue = tenantOverdue
            .GroupBy(x => x.TenantId)
            .Select(grp =>
            {
                var totalActive = grp.Sum(x => x.Count);
                var overdueCount = grp.Where(x => x.SlaStatus == WorkflowSlaStatus.Overdue).Sum(x => x.Count);
                return new TenantOverdueRankDto
                {
                    TenantId     = grp.Key,
                    OverdueCount = overdueCount,
                    OverdueRate  = totalActive > 0 ? Math.Round((double)overdueCount / totalActive * 100, 1) : 0,
                };
            })
            .OrderByDescending(x => x.OverdueCount)
            .Take(10)
            .ToList();

        // Top tenants by active workflow count
        var tenantWorkflows = await _db.WorkflowInstances
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(i => i.Status == "Active")
            .GroupBy(i => i.TenantId)
            .Select(g => new TenantWorkflowRankDto { TenantId = g.Key, ActiveCount = g.Count() })
            .OrderByDescending(x => x.ActiveCount)
            .Take(10)
            .ToListAsync(ct);

        // Outbox health by tenant
        var tenantOutbox = await _db.OutboxMessages
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(m => m.Status == OutboxStatus.Failed || m.Status == OutboxStatus.DeadLettered)
            .GroupBy(m => new { m.TenantId, m.Status })
            .Select(g => new { g.Key.TenantId, g.Key.Status, Count = g.Count() })
            .ToListAsync(ct);

        var outboxByTenant = tenantOutbox
            .GroupBy(x => x.TenantId)
            .Select(grp => new TenantOutboxHealthDto
            {
                TenantId     = grp.Key,
                FailedCount  = grp.Where(x => x.Status == OutboxStatus.Failed).Sum(x => x.Count),
                DeadLettered = grp.Where(x => x.Status == OutboxStatus.DeadLettered).Sum(x => x.Count),
            })
            .OrderByDescending(x => x.FailedCount + x.DeadLettered)
            .Take(MaxPlatformTenants)
            .ToList();

        return new PlatformAnalyticsSummaryDto
        {
            TotalActiveWorkflows      = totalActiveWorkflows,
            TotalActiveTasks          = totalActiveTasks,
            TotalOverdueTasks         = totalOverdue,
            TotalDeadLettered         = totalDeadLettered,
            TotalFailedOutbox         = totalFailed,
            TopTenantsByOverdue       = topByOverdue,
            TopTenantsByActiveWorkflows = tenantWorkflows,
            OutboxHealthByTenant      = outboxByTenant,
            AsOf                      = now,
            WindowLabel               = label,
        };
    }
}
