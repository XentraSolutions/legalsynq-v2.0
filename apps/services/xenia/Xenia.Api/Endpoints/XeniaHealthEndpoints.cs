using Xenia.Application.Adapters;
using Xenia.Application.Modules;

namespace Xenia.Api.Endpoints;

public static class XeniaHealthEndpoints
{
    public static IEndpointRouteBuilder MapXeniaHealthEndpoints(this IEndpointRouteBuilder app)
    {
        // GET /health — anonymous liveness probe
        app.MapGet("/health", () => Results.Ok(new
        {
            status = "ok",
            service = "xenia",
            timestamp = DateTime.UtcNow,
        })).AllowAnonymous();

        // GET /ready — anonymous readiness probe
        // Reports whether mandatory dependencies (DB) are ready.
        // Optional adapters being unavailable does NOT affect readiness.
        app.MapGet("/ready", async (
            IModuleRegistry modules,
            IAdapterRegistry adapters,
            CancellationToken ct) =>
        {
            var checks = new Dictionary<string, object>();
            var ready = true;

            try
            {
                // Module registry access proves DB connectivity
                var mods = await modules.GetModulesAsync(ct);
                checks["database"] = new { status = "ok", module_count = mods.Count };
            }
            catch (Exception ex)
            {
                checks["database"] = new { status = "unavailable", message = "Database is not reachable." };
                ready = false;
                _ = ex;
            }

            var adapterList = new List<object>();
            try
            {
                var allAdapters = await adapters.GetAllAsync(ct);
                foreach (var a in allAdapters)
                    adapterList.Add(new { a.AdapterKey, a.ConfigurationStatus, a.HealthStatus });
            }
            catch
            {
                adapterList.Add(new { note = "adapter registry unavailable" });
            }

            checks["adapters"] = adapterList;

            return ready
                ? Results.Ok(new { status = "ready", checks })
                : Results.Json(new { status = "not_ready", checks }, statusCode: 503);
        }).AllowAnonymous();

        return app;
    }
}
