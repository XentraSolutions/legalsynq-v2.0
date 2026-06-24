using Flow.Application.Interfaces;
using Flow.Domain.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Sockets;

namespace Flow.Infrastructure.Outbox;

/// <summary>
/// LS-FLOW-E10.3 (task slice) — background worker that evaluates
/// <see cref="Flow.Domain.Entities.WorkflowTask"/> SLA / timer state.
///
/// <para>
/// <b>TASK-FLOW-03 (post-migration):</b> the shadow table
/// (<c>flow_workflow_tasks</c>) has been dropped. The evaluator now
/// sources its batch from the Task service via
/// <see cref="IFlowTaskServiceClient.GetTasksForSlaEvaluationAsync"/>
/// and pushes transitions back via
/// <see cref="IFlowTaskServiceClient.UpdateSlaStateAsync"/>.
/// No Flow DB writes occur during a tick.
/// </para>
///
/// <para>
/// Lifecycle (per tick):
///   1. Open a fresh DI scope.
///   2. Call the Task service for a bounded batch of active tasks (Open /
///      InProgress) with <c>DueAt</c> set that may need SLA re-evaluation.
///   3. For each task, compute the new <see cref="WorkflowSlaStatus"/>
///      from <c>(now, DueAt, dueSoonThresholdMinutes)</c> via the pure
///      <see cref="WorkflowTaskSlaPolicy"/>.
///   4. Collect tasks where status or <c>SlaBreachedAt</c> changed.
///   5. Push the changes back to the Task service grouped by TenantId.
/// </para>
///
/// <para>
/// Idempotency: re-evaluating an unchanged task is a no-op — the push
/// payload is empty and no Task service write occurs.
/// </para>
///
/// <para>
/// Single-replica by design (same posture as <see cref="WorkflowSlaEvaluator"/>).
/// </para>
/// </summary>
public sealed class WorkflowTaskSlaEvaluator : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IOptionsMonitor<WorkflowTaskSlaOptions> _options;
    private readonly ILogger<WorkflowTaskSlaEvaluator> _log;
    private readonly Uri? _taskServiceBaseUri;
    private bool _taskServiceUnavailable;

    public WorkflowTaskSlaEvaluator(
        IServiceScopeFactory scopes,
        IConfiguration configuration,
        IOptionsMonitor<WorkflowTaskSlaOptions> options,
        ILogger<WorkflowTaskSlaEvaluator> log)
    {
        _scopes = scopes;
        _taskServiceBaseUri = BuildTaskServiceBaseUri(configuration["ExternalServices:Task:BaseUrl"]);
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

        if (!await IsTaskServiceReachableAsync(ct))
        {
            if (!_taskServiceUnavailable)
            {
                _taskServiceUnavailable = true;
                _log.LogWarning(
                    "WorkflowTaskSlaEvaluator: Task service at {BaseUrl} is unreachable — skipping tick until the dependency is reachable.",
                    _taskServiceBaseUri?.ToString() ?? "(unconfigured)");
            }

            return;
        }

        await using var scope = _scopes.CreateAsyncScope();
        var taskClient = scope.ServiceProvider.GetRequiredService<IFlowTaskServiceClient>();

        var now = DateTime.UtcNow;

        // Pull active tasks from the Task service (write authority, post-TASK-FLOW-03).
        // The endpoint returns tasks with DueAt set where any of:
        //   • DueAt <= dueSoonHorizon (approaching or past due)
        //   • SlaStatus != OnTrack (already promoted; keep re-visiting until terminal)
        // Ordered by DueAt ascending inside the Task service.
        IReadOnlyList<FlowSlaBatchItem> batch;
        try
        {
            batch = await taskClient.GetTasksForSlaEvaluationAsync(
                Math.Max(1, opts.BatchSize),
                opts.DueSoonThresholdMinutes,
                ct);
        }
        catch (Exception ex)
        {
            if (!_taskServiceUnavailable)
            {
                _taskServiceUnavailable = true;
                _log.LogWarning(ex,
                    "WorkflowTaskSlaEvaluator: Task service SLA batch read failed — skipping tick until the dependency is reachable.");
            }
            else
            {
                _log.LogDebug(ex,
                    "WorkflowTaskSlaEvaluator: Task service SLA batch read still failing.");
            }
            return;
        }

        if (_taskServiceUnavailable)
        {
            _taskServiceUnavailable = false;
            _log.LogInformation("WorkflowTaskSlaEvaluator: Task service connectivity restored.");
        }

        if (batch.Count == 0) return;

        // Evaluate each item using the pure SLA policy.
        // Collect changes grouped by TenantId for the push call.
        var transitions = 0;
        var slaUpdates  = new List<(Guid TenantId, Guid TaskId, string SlaStatus, DateTime? SlaBreachedAt, DateTime EvaluatedAt)>();

        foreach (var item in batch)
        {
            if (ct.IsCancellationRequested) break;
            if (item.DueAt is not DateTime dueAt) continue;

            var newStatus   = WorkflowTaskSlaPolicy.ComputeStatus(now, dueAt, opts.DueSoonThresholdMinutes);
            var newBreached = WorkflowTaskSlaPolicy.ComputeBreachedAt(newStatus, item.SlaBreachedAt, now);

            var statusChanged  = !string.Equals(newStatus, item.SlaStatus, StringComparison.Ordinal);
            var breachedChanged = newBreached != item.SlaBreachedAt;

            if (!statusChanged && !breachedChanged) continue;

            if (statusChanged) transitions++;
            slaUpdates.Add((item.TenantId, item.TaskId, newStatus, newBreached, now));
        }

        if (transitions > 0)
        {
            _log.LogInformation(
                "WorkflowTaskSlaEvaluator tick: visited={Visited} transitioned={Transitioned}",
                batch.Count, transitions);
        }

        // Push SLA changes to Task service grouped by TenantId.
        if (slaUpdates.Count == 0) return;

        foreach (var group in slaUpdates.GroupBy(u => u.TenantId))
        {
            var tenantGuid = group.Key;
            var perTenantUpdates = group
                .Select(u => (u.TaskId, u.SlaStatus, u.SlaBreachedAt, u.EvaluatedAt))
                .ToList();

            try
            {
                await taskClient.UpdateSlaStateAsync(tenantGuid, perTenantUpdates, ct);
            }
            catch (Exception ex)
            {
                _log.LogError(ex,
                    "WorkflowTaskSlaEvaluator: Task service SLA push FAILED for tenant {TenantId} ({Count} update(s)).",
                    tenantGuid, perTenantUpdates.Count);
            }
        }
    }

    private static Uri? BuildTaskServiceBaseUri(string? configuredBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(configuredBaseUrl))
            return null;

        return Uri.TryCreate(configuredBaseUrl, UriKind.Absolute, out var uri) ? uri : null;
    }

    private async Task<bool> IsTaskServiceReachableAsync(CancellationToken ct)
    {
        if (_taskServiceBaseUri is null)
            return false;

        var port = _taskServiceBaseUri.IsDefaultPort
            ? (_taskServiceBaseUri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) ? 443 : 80)
            : _taskServiceBaseUri.Port;

        using var tcp = new TcpClient();
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(500));

        try
        {
            await tcp.ConnectAsync(_taskServiceBaseUri.Host, port, timeoutCts.Token);
            return true;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return false;
        }
        catch (SocketException)
        {
            return false;
        }
    }
}
