using BuildingBlocks.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Tenant.Api.Configuration;
using Tenant.Infrastructure.Data;

namespace Tenant.Api.Endpoints;

public static class ReadSourceEndpoints
{
    public static void MapReadSourceEndpoints(this IEndpointRouteBuilder app)
    {
        // ── GET /api/v1/admin/read-source ──────────────────────────────────────
        // Returns current read-source feature flag config for operators.
        app.MapGet("/api/v1/admin/read-source", (IOptions<TenantFeatures> opts) =>
        {
            var f = opts.Value;
            return Results.Ok(new
            {
                tenantReadSource            = f.TenantReadSource.ToString(),
                tenantBrandingReadSource    = f.TenantBrandingReadSource.ToString(),
                tenantResolutionReadSource  = f.TenantResolutionReadSource.ToString(),
                tenantDualWriteEnabled      = f.TenantDualWriteEnabled,
                tenantDualWriteStrictMode   = f.TenantDualWriteStrictMode,
                effectiveBrandingSource     = f.TenantBrandingReadSource != TenantReadSource.Identity
                    ? f.TenantBrandingReadSource.ToString()
                    : f.TenantReadSource.ToString(),
                effectiveResolutionSource   = f.TenantResolutionReadSource != TenantReadSource.Identity
                    ? f.TenantResolutionReadSource.ToString()
                    : f.TenantReadSource.ToString(),
            });
        })
        .RequireAuthorization(Policies.AdminOnly);

        // ── GET /api/v1/admin/cutover-check ────────────────────────────────────
        // TENANT-B07 — Operator-facing cutover validation endpoint.
        // Returns current read-source config, latest migration run summary, and a
        // readiness assessment so operators can decide when to switch to Tenant mode.
        app.MapGet("/api/v1/admin/cutover-check", async (
            IOptions<TenantFeatures> opts,
            TenantDbContext          db,
            CancellationToken        ct) =>
        {
            var f = opts.Value;

            var effectiveBranding   = f.TenantBrandingReadSource    != TenantReadSource.Identity
                ? f.TenantBrandingReadSource.ToString()
                : f.TenantReadSource.ToString();

            var effectiveResolution = f.TenantResolutionReadSource  != TenantReadSource.Identity
                ? f.TenantResolutionReadSource.ToString()
                : f.TenantReadSource.ToString();

            // ── Latest migration run ───────────────────────────────────────────
            var latestRun = await db.MigrationRuns
                .OrderByDescending(r => r.StartedAtUtc)
                .FirstOrDefaultAsync(ct);

            object? migrationSummary = null;
            if (latestRun is not null)
            {
                migrationSummary = new
                {
                    runId            = latestRun.Id,
                    mode             = latestRun.Mode,
                    startedAtUtc     = latestRun.StartedAtUtc,
                    completedAtUtc   = latestRun.CompletedAtUtc,
                    totalScanned     = latestRun.TotalScanned,
                    tenantsCreated   = latestRun.TenantsCreated,
                    tenantsUpdated   = latestRun.TenantsUpdated,
                    tenantsSkipped   = latestRun.TenantsSkipped,
                    errors           = latestRun.Errors,
                    durationMs       = latestRun.DurationMs,
                    errorMessage     = latestRun.ErrorMessage,
                };
            }

            // ── Readiness assessment ───────────────────────────────────────────
            var migrationOk   = latestRun is not null
                                && latestRun.Mode == "Execute"
                                && latestRun.Errors == 0;
            var brandingReady = effectiveBranding   == "Tenant";
            var resolveReady  = effectiveResolution  == "Tenant";

            var readiness = (brandingReady && resolveReady && migrationOk) ? "ready"
                          : (migrationOk || brandingReady || resolveReady)  ? "partial"
                          : "not_ready";

            return Results.Ok(new
            {
                readiness,
                config = new
                {
                    tenantReadSource           = f.TenantReadSource.ToString(),
                    effectiveBrandingSource    = effectiveBranding,
                    effectiveResolutionSource  = effectiveResolution,
                    tenantDualWriteEnabled     = f.TenantDualWriteEnabled,
                    tenantDualWriteStrictMode  = f.TenantDualWriteStrictMode,
                },
                migrationSummary,
                notes = new[]
                {
                    brandingReady  ? null : "Set TenantBrandingReadSource=Tenant (or use HybridFallback first) to enable Tenant-first branding.",
                    resolveReady   ? null : "Set TenantResolutionReadSource=Tenant (or use HybridFallback first) to enable Tenant-first resolution.",
                    migrationOk    ? null : "Run POST /api/admin/migration/execute to migrate all tenant records into the Tenant service.",
                }.Where(n => n is not null).ToArray(),
            });
        })
        .RequireAuthorization(Policies.AdminOnly);
    }
}
