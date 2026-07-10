using Xenia.Application.Modules;
using Xenia.Application.TenantContext;

namespace Xenia.Api.Endpoints;

public static class XeniaModuleEndpoints
{
    public static IEndpointRouteBuilder MapXeniaModuleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/modules").RequireAuthorization(XeniaPolicies.ModulesRead);

        // GET /modules — list all registered modules
        group.MapGet("/", async (IModuleRegistry registry, CancellationToken ct) =>
        {
            var modules = await registry.GetModulesAsync(ct);
            return Results.Ok(new { modules, total = modules.Count });
        });

        // GET /modules/{key} — single module detail
        group.MapGet("/{key}", async (string key, IModuleRegistry registry, CancellationToken ct) =>
        {
            var module = await registry.GetModuleAsync(key, ct);
            return module is null
                ? Results.NotFound(new { error = $"Module '{key}' is not registered." })
                : Results.Ok(module);
        });

        // PUT /modules/{key}/enable — enable module globally (requires modules.manage)
        group.MapPut("/{key}/enable", async (
            string key,
            IModuleRegistry registry,
            CancellationToken ct) =>
        {
            await registry.EnableModuleAsync(key, ct);
            return Results.Ok(new { module_key = key, global_enabled = true });
        }).RequireAuthorization(XeniaPolicies.ModulesManage);

        // PUT /modules/{key}/disable — disable module globally (requires modules.manage)
        group.MapPut("/{key}/disable", async (
            string key,
            IModuleRegistry registry,
            CancellationToken ct) =>
        {
            await registry.DisableModuleAsync(key, ct);
            return Results.Ok(new { module_key = key, global_enabled = false });
        }).RequireAuthorization(XeniaPolicies.ModulesManage);

        // GET /modules/tenant — tenant-scoped module list
        // Requires tenant context in the JWT.
        group.MapGet("/tenant", async (
            XeniaTenantContextAccessor tenantAccessor,
            ITenantModuleRegistry tenantRegistry,
            CancellationToken ct) =>
        {
            var ctx = tenantAccessor.Current;
            if (ctx is null || !ctx.IsResolved)
                return Results.BadRequest(new { error = "Tenant context is required for this endpoint." });

            var tenantModules = await tenantRegistry.GetTenantModulesAsync(ctx.TenantId, ct);
            return Results.Ok(new { tenant_id = ctx.TenantId, modules = tenantModules });
        });

        return app;
    }
}
