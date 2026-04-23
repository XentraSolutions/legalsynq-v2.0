using Contracts;
using Tenant.Application.Interfaces;

namespace Tenant.Api.Endpoints;

public static class MigrationEndpoints
{
    public static WebApplication MapMigrationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin/migration")
            .RequireAuthorization(Policies.AdminOnly);

        // ── Dry-run reconciliation ─────────────────────────────────────────────

        group.MapGet("/dry-run", async (
            IMigrationUtilityService service,
            CancellationToken ct) =>
        {
            var report = await service.RunDryRunAsync(ct);
            return Results.Ok(report);
        });

        return app;
    }
}
