using Flow.Domain.Common;
using Flow.Domain.Entities;
using Flow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Flow.Infrastructure.Outbox;

/// <summary>
/// LS-FLOW-E10.3 (task slice) — background worker that evaluates
/// <see cref="WorkflowTask"/> SLA / timer state. Sibling to
/// <see cref="WorkflowSlaEvaluator"/>; shares the same lifecycle shape
/// and idempotency posture but operates on the work-item grain.
///
/// <para>
/// Lifecycle (per tick):
///   1. Open a fresh DI scope so <see cref="FlowDbContext"/> is fresh
///      and the request-scoped tenant provider resolves null. We
///      <c>IgnoreQueryFilters()</c> because a background worker has no
///      tenant context.
///   2. Pull a bounded batch of active tasks (Open / InProgress) with
///      <c>DueAt</c> set, ordered by oldest <c>LastSlaEvaluatedAt</c>
///      first then by <c>DueAt</c> so the workload is fair under
///      continuous churn.
///   3. For each row, compute the new <see cref="WorkflowSlaStatus"/>
///      from <c>(now, DueAt, dueSoonThresholdMinutes)</c> via the pure
///      <see cref="WorkflowTaskSlaPolicy"/>.
///   4. If the computed status differs from the persisted value, mutate
///      the row. If the new status is Overdue and we have not stamped
///      a breach moment yet, stamp <c>SlaBreachedAt = now</c>.
///   5. Always stamp <c>LastSlaEvaluatedAt = now</c> on every visited
///      row so the ordering window naturally rotates.
/// </para>
///
/// <para>
/// Idempotency: re-evaluating an unchanged row is a no-op for
/// <c>SlaStatus</c> / <c>SlaBreachedAt</c>; only <c>LastSlaEvaluatedAt</c>
/// rotates. This phase emits no outbox events for task SLA transitions
/// (deferred to the escalation / notifications phase) so the worker is
/// genuinely write-light.
/// </para>
///
/// <para>
/// Single-replica by design (same posture as <see cref="WorkflowSlaEvaluator"/>);
/// multi-replica would require a SKIP LOCKED claim phase.
/// </para>
/// </summary>
public sealed class WorkflowTaskSlaEvaluator : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IOptionsMonitor<WorkflowTaskSlaOptions> _options;
    private readonly ILogger<WorkflowTaskSlaEvaluator> _log;

    public WorkflowTaskSlaEvaluator(
        IServiceScopeFactory scopes,
        IOptionsMonitor<WorkflowTaskSlaOptions> options,
        ILogger<WorkflowTaskSlaEvaluator> log)
    {
        _scopes = scopes;
        _options = options;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = _options.CurrentValue;
        _log.LogInformation(
            "WorkflowTaskSlaEvaluator starting. Enabled={Enabled} pollSeconds={Poll} batchSize={Batch} dueSoonMinutes={DueSoon} durations(U/H/N/L)={Urgent}/{High}/{Normal}/{Low}",
            opts.Enabled, opts.PollingIntervalSeconds, opts.BatchSize, opts.DueSoonThresholdMinutes,
            opts.Durations.UrgentMinutes, opts.Durations.HighMinutes,
            opts.Durations.NormalMinutes, opts.Durations.LowMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "WorkflowTaskSlaEvaluator tick threw — sleeping then retrying.");
            }

            try
            {
                var delay = TimeSpan.FromSeconds(Math.Max(1, _options.CurrentValue.PollingIntervalSeconds));
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _log.LogInformation("WorkflowTaskSlaEvaluator stopped.");
    }

    private async Task TickAsync(CancellationToken ct)
    {
        var opts = _options.CurrentValue;
        if (!opts.Enabled) return;

        await using var scope = _scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FlowDbContext>();

        var now = DateTime.UtcNow;

        // Pull active tasks with a DueAt that might transition. The
        // predicate matches any of:
        //   • DueAt is in the past (Overdue territory; row may need to
        //     flip from OnTrack/DueSoon → Overdue, or to get its
        //     SlaBreachedAt stamped on first observation)
        //   • DueAt is in the dueSoon horizon (OnTrack → DueSoon)
        //   • SlaStatus has already been promoted off OnTrack so we keep
        //     re-visiting until completion (and so a manually-edited
        //     DueAt that pushes the deadline back can demote correctly)
        var dueSoonHorizon = now.AddMinutes(Math.Max(0, opts.DueSoonThresholdMinutes));

        var batch = await db.WorkflowTasks
            .IgnoreQueryFilters()
            .Where(t => t.DueAt != null
                        && (t.Status == WorkflowTaskStatus.Open || t.Status == WorkflowTaskStatus.InProgress)
                        && (t.DueAt <= dueSoonHorizon || t.SlaStatus != WorkflowSlaStatus.OnTrack))
            .OrderBy(t => t.LastSlaEvaluatedAt ?? DateTime.MinValue)
            .ThenBy(t => t.DueAt)
            .Take(Math.Max(1, opts.BatchSize))
            .ToListAsync(ct);

        if (batch.Count == 0) return;

        var transitions = 0;
        foreach (var task in batch)
        {
            if (ct.IsCancellationRequested) break;
            if (TryEvaluate(task, now, opts.DueSoonThresholdMinutes))
            {
                transitions++;
            }
            task.LastSlaEvaluatedAt = now;
        }

        await db.SaveChangesAsync(ct);

        if (transitions > 0)
        {
            _log.LogInformation(
                "WorkflowTaskSlaEvaluator tick: visited={Visited} transitioned={Transitioned}",
                batch.Count, transitions);
        }
    }

    /// <summary>
    /// Apply the pure-policy decision to a single task. Mutates
    /// <paramref name="task"/> only when the computed status differs
    /// from persisted (or when a first-observation breach moment needs
    /// to be stamped). Returns true when a status mutation occurred.
    /// </summary>
    internal static bool TryEvaluate(WorkflowTask task, DateTime now, int dueSoonThresholdMinutes)
    {
        if (task.DueAt is not DateTime dueAt) return false;

        var newStatus = WorkflowTaskSlaPolicy.ComputeStatus(now, dueAt, dueSoonThresholdMinutes);
        var newBreached = WorkflowTaskSlaPolicy.ComputeBreachedAt(newStatus, task.SlaBreachedAt, now);

        var statusChanged = !string.Equals(newStatus, task.SlaStatus, StringComparison.Ordinal);
        var breachedChanged = newBreached != task.SlaBreachedAt;

        if (!statusChanged && !breachedChanged) return false;

        task.SlaStatus = newStatus;
        task.SlaBreachedAt = newBreached;
        return statusChanged;
    }
}
