using Xenia.Api.Authentication;
using Xenia.Application;

namespace Xenia.Api.Endpoints;

public static class InternalExecutionEndpoints
{
    public static IEndpointRouteBuilder MapInternalExecutionEndpoints(this IEndpointRouteBuilder routes)
    {
        var internalRoutes = routes.MapGroup("/xenia/internal")
            .RequireAuthorization(XeniaPolicies.InternalService);

        internalRoutes.MapPost("/complete", (XeniaExecutionRequest request, HttpContext context, IXeniaService service) =>
        {
            var normalizedRequest = request with { ProductCode = XeniaEndpointContext.ResolveCallingProductCode(context, request.ProductCode) };
            return Results.Ok(service.ExecuteInternal(
                XeniaEndpointContext.ResolveTenantId(context, normalizedRequest.TenantId),
                XeniaEndpointContext.ResolveActorUserId(context),
                normalizedRequest,
                "Complete"));
        });

        internalRoutes.MapPost("/stream", (XeniaExecutionRequest request, HttpContext context, IXeniaService service) =>
        {
            var normalizedRequest = request with { ProductCode = XeniaEndpointContext.ResolveCallingProductCode(context, request.ProductCode) };
            var response = service.ExecuteInternal(
                XeniaEndpointContext.ResolveTenantId(context, normalizedRequest.TenantId),
                XeniaEndpointContext.ResolveActorUserId(context),
                normalizedRequest,
                "Stream");

            return Results.Text(
                XeniaEndpointContext.ToServerSentEvents(response.OutputChunks, Guid.CreateVersion7()),
                "text/event-stream");
        });

        internalRoutes.MapPost("/skills/{skillCode}/execute", (string skillCode, XeniaExecutionRequest request, HttpContext context, IXeniaService service) =>
            Results.Ok(service.ExecuteInternal(
                XeniaEndpointContext.ResolveTenantId(context, request.TenantId),
                XeniaEndpointContext.ResolveActorUserId(context),
                request with { ProductCode = XeniaEndpointContext.ResolveCallingProductCode(context, request.ProductCode) },
                "Skill",
                skillCode: skillCode)));

        internalRoutes.MapPost("/agents/{agentCode}/execute", (string agentCode, XeniaExecutionRequest request, HttpContext context, IXeniaService service) =>
            Results.Ok(service.ExecuteInternal(
                XeniaEndpointContext.ResolveTenantId(context, request.TenantId),
                XeniaEndpointContext.ResolveActorUserId(context),
                request with { ProductCode = XeniaEndpointContext.ResolveCallingProductCode(context, request.ProductCode) },
                "Agent",
                agentCode: agentCode)));

        internalRoutes.MapPost("/tools/{toolCode}/execute", (string toolCode, XeniaExecutionRequest request, HttpContext context, IXeniaService service) =>
            Results.Ok(service.ExecuteInternal(
                XeniaEndpointContext.ResolveTenantId(context, request.TenantId),
                XeniaEndpointContext.ResolveActorUserId(context),
                request with { ProductCode = XeniaEndpointContext.ResolveCallingProductCode(context, request.ProductCode) },
                "Tool",
                toolCode: toolCode)));

        return routes;
    }
}
