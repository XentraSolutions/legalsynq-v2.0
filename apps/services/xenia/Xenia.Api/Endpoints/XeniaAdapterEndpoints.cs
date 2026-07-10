using Xenia.Application.Adapters;

namespace Xenia.Api.Endpoints;

public static class XeniaAdapterEndpoints
{
    public static IEndpointRouteBuilder MapXeniaAdapterEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/adapters").RequireAuthorization(XeniaPolicies.Read);

        // GET /adapters — list all registered adapters with safe status information
        // Credentials are never returned.
        group.MapGet("/", async (IAdapterRegistry registry, CancellationToken ct) =>
        {
            var adapters = await registry.GetAllAsync(ct);
            return Results.Ok(new { adapters, total = adapters.Count });
        });

        // GET /adapters/{key} — single adapter detail
        group.MapGet("/{key}", async (string key, IAdapterRegistry registry, CancellationToken ct) =>
        {
            var adapter = await registry.GetAsync(key, ct);
            return adapter is null
                ? Results.NotFound(new { error = $"Adapter '{key}' is not registered." })
                : Results.Ok(adapter);
        });

        return app;
    }
}
