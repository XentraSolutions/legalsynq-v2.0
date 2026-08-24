using Xenia.Application.Email;
using Xenia.Application.Modules;
using Xenia.Application.TenantContext;

namespace Xenia.Api.Endpoints;

/// <summary>
/// Endpoints for tenant-scoped Email module management.
/// Tenant admins can view and toggle the Email module for their tenant.
/// Platform admins can toggle it globally.
/// </summary>
public static class XeniaEmailModuleEndpoints
{
    public static IEndpointRouteBuilder MapXeniaEmailModuleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/email/module");

        // GET /email/module — effective email module state for the current tenant
        group.MapGet("/", async (
            XeniaTenantContextAccessor tenantAccessor,
            IModuleRegistry moduleRegistry,
            ITenantModuleRegistry tenantRegistry,
            CancellationToken ct) =>
        {
            var ctx = tenantAccessor.Current;
            if (ctx is null || !ctx.IsResolved)
                return Results.BadRequest(new { error = "Tenant context is required." });

            var global = await moduleRegistry.GetModuleAsync(EmailModuleKeys.ModuleKey, ct);
            if (global is null)
                return Results.NotFound(new { error = "Email module is not registered." });

            var tenantModules = await tenantRegistry.GetTenantModulesAsync(ctx.TenantId, ct);
            var tenant = tenantModules.FirstOrDefault(m => m.ModuleKey == EmailModuleKeys.ModuleKey);

            var effective = EffectiveModuleDto.From(global, tenant);
            return Results.Ok(effective);
        }).RequireAuthorization(XeniaPolicies.EmailRead);

        // PUT /email/module/enable — tenant admin enables Email for their tenant
        group.MapPut("/enable", async (
            XeniaTenantContextAccessor tenantAccessor,
            ITenantModuleRegistry tenantRegistry,
            IModuleRegistry moduleRegistry,
            CancellationToken ct) =>
        {
            var ctx = tenantAccessor.Current;
            if (ctx is null || !ctx.IsResolved)
                return Results.BadRequest(new { error = "Tenant context is required." });

            var global = await moduleRegistry.GetModuleAsync(EmailModuleKeys.ModuleKey, ct);
            if (global is null)
                return Results.NotFound(new { error = "Email module is not registered." });

            if (!global.GlobalEnabled)
                return Results.Conflict(new
                {
                    error = "Email module is globally disabled and cannot be enabled by tenants. Contact platform administration.",
                    module_key = EmailModuleKeys.ModuleKey,
                });

            await tenantRegistry.EnableModuleForTenantAsync(ctx.TenantId, EmailModuleKeys.ModuleKey, ct);
            return Results.Ok(new { module_key = EmailModuleKeys.ModuleKey, tenant_enabled = true });
        }).RequireAuthorization(XeniaPolicies.EmailManage);

        // PUT /email/module/disable — tenant admin disables Email for their tenant
        group.MapPut("/disable", async (
            XeniaTenantContextAccessor tenantAccessor,
            ITenantModuleRegistry tenantRegistry,
            CancellationToken ct) =>
        {
            var ctx = tenantAccessor.Current;
            if (ctx is null || !ctx.IsResolved)
                return Results.BadRequest(new { error = "Tenant context is required." });

            await tenantRegistry.DisableModuleForTenantAsync(ctx.TenantId, EmailModuleKeys.ModuleKey, ct);
            return Results.Ok(new { module_key = EmailModuleKeys.ModuleKey, tenant_enabled = false });
        }).RequireAuthorization(XeniaPolicies.EmailManage);

        // PUT /email/module/global/enable — platform admin enables Email globally
        group.MapPut("/global/enable", async (
            IModuleRegistry registry,
            CancellationToken ct) =>
        {
            await registry.EnableModuleAsync(EmailModuleKeys.ModuleKey, ct);
            return Results.Ok(new { module_key = EmailModuleKeys.ModuleKey, global_enabled = true });
        }).RequireAuthorization(XeniaPolicies.ModulesManage);

        // PUT /email/module/global/disable — platform admin disables Email globally
        group.MapPut("/global/disable", async (
            IModuleRegistry registry,
            CancellationToken ct) =>
        {
            await registry.DisableModuleAsync(EmailModuleKeys.ModuleKey, ct);
            return Results.Ok(new { module_key = EmailModuleKeys.ModuleKey, global_enabled = false });
        }).RequireAuthorization(XeniaPolicies.ModulesManage);

        return app;
    }
}
