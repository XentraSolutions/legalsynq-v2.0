// LSCC-011: Activation funnel analytics endpoint.
// Route: GET /api/admin/analytics/funnel
// Auth:  PlatformOrTenantAdmin
// Query: ?days=7|30|90  OR  ?startDate=yyyy-MM-dd&endDate=yyyy-MM-dd
using BuildingBlocks.Authorization;
using CareConnect.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CareConnect.Api.Endpoints;

public static class AnalyticsEndpoints
{
    public static void MapAnalyticsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin/analytics")
            .RequireAuthorization(Policies.PlatformOrTenantAdmin);

        // GET /api/admin/analytics/funnel?days=30
        // GET /api/admin/analytics/funnel?startDate=2026-01-01&endDate=2026-03-31
        group.MapGet("/funnel", async (
            [FromQuery] int?    days,
            [FromQuery] string? startDate,
            [FromQuery] string? endDate,
            IActivationFunnelAnalyticsService analytics,
            CancellationToken ct) =>
        {
            var (from, to) = ResolveRange(days, startDate, endDate);
            var metrics = await analytics.GetMetricsAsync(from, to, ct);
            return Results.Ok(metrics);
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static (DateTime from, DateTime to) ResolveRange(
        int?    days,
        string? startDate,
        string? endDate)
    {
        var today = DateTime.UtcNow.Date;

        // Custom range takes priority when both dates are supplied
        if (!string.IsNullOrWhiteSpace(startDate) && !string.IsNullOrWhiteSpace(endDate) &&
            DateTime.TryParse(startDate, out var from) &&
            DateTime.TryParse(endDate,   out var to))
        {
            return (from.Date, to.Date);
        }

        // Preset: last N days (default 30)
        var n = days is > 0 and <= 365 ? days.Value : 30;
        return (today.AddDays(-(n - 1)), today);
    }
}
