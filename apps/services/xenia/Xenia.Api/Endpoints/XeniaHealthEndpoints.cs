using Xenia.Application.Adapters;
using Xenia.Application.Modules;
using Xenia.Domain.Adapters;

namespace Xenia.Api.Endpoints;

public static class XeniaHealthEndpoints
{
    public static IEndpointRouteBuilder MapXeniaHealthEndpoints(this IEndpointRouteBuilder app)
    {
        // GET /health — anonymous liveness probe
        // Returns 200 while the process is alive regardless of adapter state.
        app.MapGet("/health", () => Results.Ok(new
        {
            status = "ok",
            service = "xenia",
            timestamp = DateTime.UtcNow,
        })).AllowAnonymous();

        // GET /ready — anonymous readiness probe
        //
        // Readiness is determined by adapter criticality:
        //   Mandatory  — unavailability → 503 not_ready
        //   Optional   — unavailability → 200 degraded (noted in response)
        //   Disabled   — excluded from computation entirely
        //
        // DB connectivity is always mandatory — if the module registry query
        // fails the service is not ready regardless of adapter state.
        app.MapGet("/ready", async (
            IModuleRegistry modules,
            IAdapterRegistry adapters,
            CancellationToken ct) =>
        {
            var checks = new Dictionary<string, object>();
            var ready = true;
            var degraded = false;

            // ── Mandatory check: database connectivity ────────────────────────
            try
            {
                var mods = await modules.GetModulesAsync(ct);
                checks["database"] = new { status = "ok", criticality = "Mandatory", module_count = mods.Count };
            }
            catch (Exception ex)
            {
                checks["database"] = new
                {
                    status = "unavailable",
                    criticality = "Mandatory",
                    message = "Database is not reachable."
                };
                ready = false;
                _ = ex;
            }

            // ── Adapter checks categorised by criticality ─────────────────────
            var adapterChecks = new List<object>();
            try
            {
                var allAdapters = await adapters.GetAllAsync(ct);
                foreach (var a in allAdapters)
                {
                    var adapterReady = a.HealthStatus is "Healthy" or "Unknown" or "Unconfigured";

                    // Parse criticality enum from the DTO string
                    var criticality = Enum.TryParse<AdapterCriticality>(a.Criticality, out var c)
                        ? c : AdapterCriticality.Optional;

                    adapterChecks.Add(new
                    {
                        a.AdapterKey,
                        a.Criticality,
                        a.ConfigurationStatus,
                        a.HealthStatus,
                    });

                    switch (criticality)
                    {
                        case AdapterCriticality.Mandatory when !adapterReady && a.HealthStatus == "Unavailable":
                            // Truly unavailable mandatory adapter makes service not ready
                            ready = false;
                            break;

                        case AdapterCriticality.Optional when a.HealthStatus == "Unavailable":
                            degraded = true;
                            break;

                        case AdapterCriticality.Disabled:
                            // Ignored entirely
                            break;
                    }
                }
            }
            catch
            {
                adapterChecks.Add(new { note = "adapter registry unavailable" });
            }

            checks["adapters"] = adapterChecks;

            var status = !ready ? "not_ready" : degraded ? "degraded" : "ready";
            var statusCode = ready ? 200 : 503;

            return Results.Json(new { status, checks }, statusCode: statusCode);
        }).AllowAnonymous();

        return app;
    }
}
