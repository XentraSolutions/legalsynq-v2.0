namespace Xenia.Api.Endpoints;

public static class XeniaInfoEndpoints
{
    public static IEndpointRouteBuilder MapXeniaInfoEndpoints(this IEndpointRouteBuilder app)
    {
        // GET /info — anonymous, safe service metadata
        // Does not expose secrets, connection strings, or signing keys.
        app.MapGet("/info", (IWebHostEnvironment env) => Results.Ok(new
        {
            service = "xenia",
            description = "Xenia Automation Platform — Core Service",
            version = XeniaBuildInfo.ServiceVersion,
            environment = env.EnvironmentName,
            started_at = XeniaBuildInfo.StartedAt,
            uptime_seconds = (DateTime.UtcNow - XeniaBuildInfo.StartedAt).TotalSeconds,
            is_standalone = true,
            note = "Xenia is a standalone, tenant-aware automation platform. " +
                   "Platform services (Tenant, Identity, Documents, etc.) are accessed through adapter interfaces.",
        })).AllowAnonymous();

        return app;
    }
}
