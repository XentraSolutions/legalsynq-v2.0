using BuildingBlocks.Authorization;
using Microsoft.Extensions.Options;
using Tenant.Application.Configuration;
using Tenant.Application.Metrics;

namespace Tenant.Api.Endpoints;

/// <summary>
/// TENANT-B08 — Admin diagnostics endpoint for in-process runtime metrics.
///
/// GET /api/v1/admin/runtime-metrics
///   Returns lifetime read, sync, and cache counters for the Tenant service.
///   Counters are process-memory only and reset on service restart.
///   Requires PlatformAdmin role.
/// </summary>
public static class RuntimeMetricsEndpoints
{
    public static void MapRuntimeMetricsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/admin/runtime-metrics", (
            TenantRuntimeMetrics     metrics,
            IOptions<TenantFeatures> opts) =>
        {
            var s = metrics.Snapshot();
            var f = opts.Value;

            return Results.Ok(new
            {
                startedAtUtc  = s.StartedAtUtc,
                uptimeSeconds = s.UptimeSeconds,
                branding = new
                {
                    attempted    = s.BrandingAttempted,
                    succeeded    = s.BrandingSucceeded,
                    failed       = s.BrandingFailed,
                    cacheHits    = s.BrandingCacheHits,
                    cacheMisses  = s.BrandingCacheMisses,
                    cacheHitRate = s.BrandingCacheHits + s.BrandingCacheMisses == 0
                        ? (double?)null
                        : Math.Round((double)s.BrandingCacheHits / (s.BrandingCacheHits + s.BrandingCacheMisses) * 100, 1),
                },
                resolution = new
                {
                    attempted    = s.ResolutionAttempted,
                    succeeded    = s.ResolutionSucceeded,
                    failed       = s.ResolutionFailed,
                    cacheHits    = s.ResolutionCacheHits,
                    cacheMisses  = s.ResolutionCacheMisses,
                    cacheHitRate = s.ResolutionCacheHits + s.ResolutionCacheMisses == 0
                        ? (double?)null
                        : Math.Round((double)s.ResolutionCacheHits / (s.ResolutionCacheHits + s.ResolutionCacheMisses) * 100, 1),
                },
                sync = new
                {
                    attempted = s.SyncAttemptsReceived,
                    succeeded = s.SyncSucceeded,
                    failed    = s.SyncFailed,
                    successRate = s.SyncAttemptsReceived == 0
                        ? (double?)null
                        : Math.Round((double)s.SyncSucceeded / s.SyncAttemptsReceived * 100, 1),
                },
                cacheConfig = new
                {
                    enabled    = f.TenantReadCachingEnabled,
                    ttlSeconds = f.TenantReadCacheTtlSeconds,
                },
                note = "Counters are process-memory only and reset on service restart.",
            });
        })
        .RequireAuthorization(Policies.AdminOnly);
    }
}
