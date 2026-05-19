using Monitoring.Api.Authentication;
using Monitoring.Api.Contracts;
using Monitoring.Application.Queries;
using Monitoring.Domain.Monitoring;

namespace Monitoring.Api.Endpoints;

/// <summary>
/// Read endpoints for monitoring status, alerts, and summary.
///
/// All endpoints require authentication via the MonitoringRead policy, which
/// accepts either a valid user JWT (Bearer scheme) or a valid service token
/// (ServiceToken scheme). This prevents unauthenticated external access to
/// internal operational telemetry while still allowing the Control Center BFF
/// and other platform services to call these endpoints from the server side.
///
/// Auth note: MON-INT-01-003 resolved the RS256 vs HS256 mismatch; both
/// user JWT and service-token schemes are now wired in AddMonitoringAuthentication().
/// </summary>
public static class MonitoringReadEndpoints
{
    public static IEndpointRouteBuilder MapMonitoringReadEndpoints(this IEndpointRouteBuilder app)
    {
        var read = app.MapGroup("/monitoring").RequireAuthorization(MonitoringPolicies.Read);

        read.MapGet("/status",  GetStatusAsync);
        read.MapGet("/alerts",  GetAlertsAsync);
        read.MapGet("/summary", GetSummaryAsync);

        return app;
    }

    // ── Status ────────────────────────────────────────────────────────────────

    private static async Task<IResult> GetStatusAsync(
        IMonitoringReadService svc,
        CancellationToken ct)
    {
        var results = await svc.GetStatusAsync(ct);
        return Results.Ok(results.Select(MapStatus).ToArray());
    }

    // ── Alerts ────────────────────────────────────────────────────────────────

    private static async Task<IResult> GetAlertsAsync(
        IMonitoringReadService svc,
        CancellationToken ct)
    {
        var results = await svc.GetActiveAlertsAsync(ct);
        return Results.Ok(results.Select(MapAlert).ToArray());
    }

    // ── Summary ───────────────────────────────────────────────────────────────

    private static async Task<IResult> GetSummaryAsync(
        IMonitoringReadService svc,
        CancellationToken ct)
    {
        var summary = await svc.GetSummaryAsync(ct);

        var integrations = summary.Statuses.Select(MapStatus).ToArray();
        var alerts       = summary.ActiveAlerts.Select(MapAlert).ToArray();

        // System status = worst status across all entities.
        // Priority: Down > Degraded > Healthy; Unknown treated as Degraded
        // (entity registered but not yet checked — conservative stance).
        var system = DeriveSystemStatus(integrations);

        return Results.Ok(new MonitoringSummaryResponse(system, integrations, alerts));
    }

    // ── Mapping helpers ───────────────────────────────────────────────────────

    private static MonitoringStatusResponse MapStatus(MonitoringStatusResult r) =>
        new(
            EntityId:         r.EntityId,
            Name:             r.Name,
            Scope:            r.Scope,
            Status:           MapEntityStatus(r.Status),
            LastCheckedAtUtc: r.LastCheckedAtUtc,
            LatencyMs:        r.LastElapsedMs);

    private static MonitoringAlertResponse MapAlert(MonitoringAlertResult r) =>
        new(
            AlertId:      r.AlertId,
            EntityId:     r.EntityId,
            Name:         r.Name,
            Severity:     MapAlertSeverity(r.AlertType),
            Message:      r.Message,
            CreatedAtUtc: r.TriggeredAtUtc,
            ResolvedAtUtc: r.ResolvedAtUtc);

    /// <summary>
    /// Maps domain <see cref="EntityStatus"/> to Control Center vocabulary.
    /// </summary>
    private static string MapEntityStatus(EntityStatus status) => status switch
    {
        EntityStatus.Up       => "Healthy",
        EntityStatus.Degraded => "Degraded",
        EntityStatus.Down     => "Down",
        EntityStatus.Unknown  => "Degraded",  // not yet checked — conservative
        _                     => "Degraded",
    };

    /// <summary>
    /// Maps domain <see cref="AlertType"/> to Control Center severity vocabulary.
    /// </summary>
    private static string MapAlertSeverity(AlertType type) => type switch
    {
        AlertType.StatusDown => "Critical",
        _                    => "Warning",
    };

    /// <summary>
    /// Derives the aggregate system status from the set of entity statuses.
    /// Down beats Degraded beats Healthy. An empty set is reported as Healthy.
    /// Entities with scope "test" are excluded — they exist only for internal
    /// verification and must not influence the production health signal.
    /// </summary>
    private static MonitoringSystemStatusResponse DeriveSystemStatus(
        IReadOnlyList<MonitoringStatusResponse> integrations)
    {
        var now = DateTime.UtcNow;

        // Exclude test-scope entities from system-level aggregation so that
        // internal probe entities (e.g. ServiceToken-Test-Entity) never drive
        // the overall status to "Down" or "Degraded".
        var productive = integrations
            .Where(i => !string.Equals(i.Scope, "test", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (productive.Count == 0)
        {
            return new MonitoringSystemStatusResponse("Healthy", now);
        }

        string status;

        if (productive.Any(i => i.Status == "Down"))
        {
            status = "Down";
        }
        else if (productive.Any(i => i.Status == "Degraded"))
        {
            status = "Degraded";
        }
        else
        {
            status = "Healthy";
        }

        // Use the most recent check timestamp as the system-level timestamp.
        // Based on productive entities only, consistent with the status above.
        var lastChecked = productive
            .Where(i => i.LastCheckedAtUtc.HasValue)
            .Select(i => i.LastCheckedAtUtc!.Value)
            .DefaultIfEmpty(now)
            .Max();

        return new MonitoringSystemStatusResponse(status, lastChecked);
    }
}
