using Xenia.Api.Authentication;
using Xenia.Application;

namespace Xenia.Api.Endpoints;

public static class TenantEndpoints
{
    public static IEndpointRouteBuilder MapTenantEndpoints(this IEndpointRouteBuilder routes)
    {
        var tenantAdmin = routes.MapGroup("/xenia/tenant")
            .RequireAuthorization(XeniaPolicies.TenantAdminOrAbove);

        tenantAdmin.MapGet("/configuration", (HttpContext context, IXeniaService service) =>
        {
            XeniaEndpointContext.RequireXeniaAccess(context);
            return Results.Ok(service.GetTenantConfiguration(XeniaEndpointContext.ResolveTenantId(context)));
        });
        tenantAdmin.MapGet("/providers", (HttpContext context, IXeniaService service) =>
        {
            XeniaEndpointContext.RequireXeniaAccess(context);
            return Results.Ok(service.ListProviders(XeniaEndpointContext.ResolveTenantId(context)));
        });
        tenantAdmin.MapPut("/byoai/configuration", (XeniaProviderConfigurationRequest request, HttpContext context, IXeniaService service) =>
        {
            XeniaEndpointContext.RequireXeniaAccess(context);
            return Results.Ok(service.SaveTenantByoAiConfiguration(
                XeniaEndpointContext.ResolveTenantId(context),
                request,
                XeniaEndpointContext.ResolveActorUserId(context)));
        });
        tenantAdmin.MapPost("/byoai/providers/test", (XeniaProviderConfigurationRequest request, HttpContext context, IXeniaService service) =>
        {
            XeniaEndpointContext.RequireXeniaAccess(context);
            return Results.Ok(service.TestProvider(
                XeniaEndpointContext.ResolveTenantId(context),
                null,
                request,
                XeniaEndpointContext.ResolveActorUserId(context)));
        });

        return routes;
    }
}
