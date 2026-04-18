using Flow.Application.DTOs;
using Flow.Application.Exceptions;
using Flow.Application.Interfaces;
using Flow.Application.Options;
using Flow.Domain.Common;
using Flow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Flow.Application.Services;

/// <summary>
/// LS-FLOW-E18 — deterministic, explainable assignee recommendation
/// engine.
///
/// <para>
/// <b>Algorithm summary (documented rule hierarchy):</b>
/// <list type="number">
///   <item>
///     Resolve candidate user ids — either from the explicit caller-
///     supplied list or from workload-history derivation
///     (role-queue: users with active tasks for the same role;
///      org-queue: users with active tasks for the same org).
///   </item>
///   <item>
///     Fetch active task counts for all candidates via
///     <see cref="IWorkloadService.GetActiveTaskCountsAsync"/>.
///   </item>
///   <item>
///     Sort candidates into three capacity buckets (in preference order):
///     <list type="bullet">
///       <item>Below soft threshold (count &lt; <c>SoftCapacityThreshold</c>)</item>
///       <item>Between soft and hard cap (count &lt; <c>MaxActiveTasksPerUser</c>)</item>
///       <item>Overloaded (count ≥ <c>MaxActiveTasksPerUser</c>)</item>
///     </list>
///     Within each bucket, sort by active count ascending, then UserId
///     lexicographic ascending as a stable tiebreaker.
///   </item>
///   <item>
///     <c>RecommendedUserId</c> = first in sorted list; null if no candidates.
///   </item>
///   <item>
///     Generate a human-readable <c>ExplanationSummary</c> and per-candidate
///     <c>ExplanationNote</c> for every candidate.
///   </item>
/// </list>
/// No randomness. No ML. No opaque scoring. Every recommendation is
/// reproducible for the same input state.
/// </para>
/// </summary>
public sealed class TaskRecommendationService : ITaskRecommendationService
{
    private readonly IFlowDbContext _db;
    private readonly IWorkloadService _workload;
    private readonly WorkDistributionOptions _opts;
    private readonly ILogger<TaskRecommendationService> _log;

    public TaskRecommendationService(
        IFlowDbContext db,
        IWorkloadService workload,
        IOptions<WorkDistributionOptions> opts,
        ILogger<TaskRecommendationService> log)
    {
        _db      = db;
        _workload = workload;
        _opts    = opts.Value;
        _log     = log;
    }

    /// <inheritdoc />
    public async Task<RecommendAssigneeResult> RecommendAsync(
        Guid taskId,
        IReadOnlyList<string>? candidateUserIds,
        CancellationToken ct = default)
    {
        if (!_opts.EnableRecommendation)
        {
            return new RecommendAssigneeResult
            {
                TaskId            = taskId,
                RecommendedUserId = null,
                ExplanationSummary = "Recommendation feature is currently disabled (WorkDistribution:EnableRecommendation = false).",
                CandidateSource   = "disabled",
                TaskSlaStatus     = WorkflowSlaStatus.OnTrack,
                TaskPriority      = WorkflowTaskPriority.Normal,
                Candidates        = Array.Empty<AssigneeCandidateInfo>(),
            };
        }

        // 1. Load the task context (tenant filter applied globally).
        var ctx = await _db.WorkflowTasks
            .AsNoTracking()
            .Where(t => t.Id == taskId)
            .Select(t => new TaskRecommendationContext(
                t.Id,
                t.AssignmentMode,
                t.AssignedRole,
                t.AssignedOrgId,
                t.SlaStatus,
                t.Priority))
            .FirstOrDefaultAsync(ct);

        if (ctx is null)
            throw new NotFoundException(nameof(WorkflowTask), taskId);

        // 2. Resolve candidates + source label.
        var (candidates, source) = await ResolveCandidatesAsync(ctx, candidateUserIds, ct);

        if (candidates.Count == 0)
        {
            var noMsg = BuildNoRecommendationMessage(ctx, source);
            _log.LogInformation(
                "E18 Recommendation: TaskId={TaskId} — no candidates (source={Source})",
                taskId, source);

            return new RecommendAssigneeResult
            {
                TaskId             = taskId,
                RecommendedUserId  = null,
                ExplanationSummary = noMsg,
                CandidateSource    = source,
                TaskSlaStatus      = ctx.SlaStatus,
                TaskPriority       = ctx.Priority,
                Candidates         = Array.Empty<AssigneeCandidateInfo>(),
            };
        }

        // 3. Fetch workload counts for all candidates (single query).
        var counts = await _workload.GetActiveTaskCountsAsync(candidates, ct);

        // 4. Build + sort the ranked list.
        var ranked = RankCandidates(candidates, counts);

        var recommended = ranked[0].UserId;
        var summary = BuildExplanationSummary(recommended, ranked[0], ctx, source);

        _log.LogInformation(
            "E18 Recommendation: TaskId={TaskId} SlaStatus={Sla} Priority={Priority} " +
            "→ Recommended={User} (count={Count} bucket={Bucket}) source={Source} candidates={Total}",
            taskId, ctx.SlaStatus, ctx.Priority,
            recommended, ranked[0].ActiveTaskCount,
            GetBucket(ranked[0].ActiveTaskCount),
            source, ranked.Count);

        return new RecommendAssigneeResult
        {
            TaskId             = taskId,
            RecommendedUserId  = recommended,
            ExplanationSummary = summary,
            CandidateSource    = source,
            TaskSlaStatus      = ctx.SlaStatus,
            TaskPriority       = ctx.Priority,
            Candidates         = ranked,
        };
    }

    // ── Candidate resolution ──────────────────────────────────────────────

    private async Task<(IReadOnlyList<string> Candidates, string Source)> ResolveCandidatesAsync(
        TaskRecommendationContext ctx,
        IReadOnlyList<string>? explicit_,
        CancellationToken ct)
    {
        // Caller-supplied list takes priority — no workload-history derivation.
        if (explicit_ is { Count: > 0 })
        {
            var deduped = explicit_
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            return (deduped, "explicit");
        }

        // Derive from workload history based on assignment mode.
        switch (ctx.AssignmentMode)
        {
            case WorkflowTaskAssignmentMode.RoleQueue:
                if (string.IsNullOrWhiteSpace(ctx.AssignedRole))
                    return (Array.Empty<string>(), "workload-history-role");

                var roleIds = await _workload.GetUserIdsForRoleAsync(
                    ctx.AssignedRole, _opts.MaxDerivedCandidates, ct);
                return (roleIds, "workload-history-role");

            case WorkflowTaskAssignmentMode.OrgQueue:
                if (string.IsNullOrWhiteSpace(ctx.AssignedOrgId))
                    return (Array.Empty<string>(), "workload-history-org");

                var orgIds = await _workload.GetUserIdsForOrgAsync(
                    ctx.AssignedOrgId, _opts.MaxDerivedCandidates, ct);
                return (orgIds, "workload-history-org");

            default:
                // DirectUser is already assigned; Unassigned has no eligible set.
                return (Array.Empty<string>(), "none");
        }
    }

    // ── Ranking ───────────────────────────────────────────────────────────

    private IReadOnlyList<AssigneeCandidateInfo> RankCandidates(
        IReadOnlyList<string> candidates,
        IReadOnlyDictionary<string, int> counts)
    {
        // Enrich — users not in the counts dictionary have 0 active tasks.
        var enriched = candidates
            .Select(uid =>
            {
                var count = counts.TryGetValue(uid, out var c) ? c : 0;
                return (UserId: uid, Count: count, Bucket: GetBucket(count));
            })
            // Sort: bucket asc (0 = best), then count asc (less work first),
            // then UserId asc (stable lexicographic tiebreaker).
            .OrderBy(x => x.Bucket)
            .ThenBy(x => x.Count)
            .ThenBy(x => x.UserId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return enriched
            .Select((x, idx) => new AssigneeCandidateInfo
            {
                UserId               = x.UserId,
                ActiveTaskCount      = x.Count,
                IsWithinSoftThreshold = x.Count < _opts.SoftCapacityThreshold,
                IsWithinHardCap      = x.Count < _opts.MaxActiveTasksPerUser,
                Rank                 = idx + 1,
                ExplanationNote      = BuildCandidateNote(x.UserId, x.Count, x.Bucket, idx + 1, enriched.Count),
            })
            .ToList();
    }

    /// <summary>
    /// Returns the capacity bucket index (0 = below soft, 1 = below hard, 2 = overloaded).
    /// Lower is better.
    /// </summary>
    private int GetBucket(int count) =>
        count < _opts.SoftCapacityThreshold ? 0 :
        count < _opts.MaxActiveTasksPerUser  ? 1 : 2;

    // ── Explanation text helpers ──────────────────────────────────────────

    private string BuildExplanationSummary(
        string recommendedUserId,
        AssigneeCandidateInfo top,
        TaskRecommendationContext ctx,
        string source)
    {
        var slaNote = ctx.SlaStatus is WorkflowSlaStatus.Escalated or WorkflowSlaStatus.Overdue
            ? $" Task is {ctx.SlaStatus} — high urgency factored into prioritization."
            : ctx.SlaStatus == WorkflowSlaStatus.DueSoon
              ? $" Task is {ctx.SlaStatus} — moderate urgency noted."
              : string.Empty;

        var capacityNote = top.IsWithinSoftThreshold
            ? $"below soft capacity ({top.ActiveTaskCount}/{_opts.SoftCapacityThreshold} active tasks)"
            : top.IsWithinHardCap
              ? $"between soft and hard cap ({top.ActiveTaskCount} active tasks; soft={_opts.SoftCapacityThreshold}, max={_opts.MaxActiveTasksPerUser})"
              : $"overloaded ({top.ActiveTaskCount}/{_opts.MaxActiveTasksPerUser} active tasks — no other candidates available)";

        var tieNote = top.Rank == 1 && top.ActiveTaskCount == 0
            ? " Lowest possible load (0 active tasks)."
            : string.Empty;

        var sourceNote = source == "explicit"
            ? "from caller-supplied candidate list"
            : source == "workload-history-role"
              ? $"from workload history (users with active tasks for role '{ctx.AssignedRole}')"
              : source == "workload-history-org"
                ? $"from workload history (users with active tasks for org '{ctx.AssignedOrgId}')"
                : string.Empty;

        return $"Recommended user '{recommendedUserId}': eligible {sourceNote}, " +
               $"{capacityNote}, and has the lowest load among ranked candidates.{tieNote}{slaNote}";
    }

    private static string BuildNoRecommendationMessage(
        TaskRecommendationContext ctx,
        string source)
    {
        if (source == "none")
        {
            return ctx.AssignmentMode == WorkflowTaskAssignmentMode.DirectUser
                ? "No recommendation: task is already directly assigned (AssignmentMode = DirectUser). Use the standard reassign endpoint."
                : "No recommendation: task is Unassigned. An administrator should assign a queue mode first.";
        }

        if (source == "workload-history-role")
            return $"No recommendation: no workload-history candidates found for role '{ctx.AssignedRole}'. " +
                   "No users in this tenant currently have active tasks with this role. " +
                   "Supply candidateUserIds explicitly to receive a recommendation.";

        if (source == "workload-history-org")
            return $"No recommendation: no workload-history candidates found for org '{ctx.AssignedOrgId}'. " +
                   "Supply candidateUserIds explicitly to receive a recommendation.";

        return "No recommendation: candidate list is empty after deduplication.";
    }

    private string BuildCandidateNote(
        string userId,
        int count,
        int bucket,
        int rank,
        int total)
    {
        var capacity = bucket switch
        {
            0 => $"Below soft threshold ({count}/{_opts.SoftCapacityThreshold} tasks).",
            1 => $"Between soft and hard cap ({count}/{_opts.MaxActiveTasksPerUser} tasks).",
            _ => $"Overloaded ({count}/{_opts.MaxActiveTasksPerUser} tasks).",
        };

        var rankNote = rank == 1
            ? " Ranked #1 — lowest eligible load."
            : rank == total
              ? $" Ranked last ({rank}/{total})."
              : $" Ranked #{rank} of {total}.";

        return $"{capacity}{rankNote}";
    }
}
