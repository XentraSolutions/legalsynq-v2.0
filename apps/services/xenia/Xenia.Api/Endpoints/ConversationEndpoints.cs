using Xenia.Api.Authentication;
using Xenia.Application;

namespace Xenia.Api.Endpoints;

public static class ConversationEndpoints
{
    public static IEndpointRouteBuilder MapConversationEndpoints(this IEndpointRouteBuilder routes)
    {
        var userRoutes = routes.MapGroup("/xenia")
            .RequireAuthorization(XeniaPolicies.AuthenticatedUser);

        userRoutes.MapPost("/conversations", (XeniaCreateConversationRequest request, HttpContext context, IXeniaService service) =>
        {
            XeniaEndpointContext.RequireXeniaAccess(context);
            var tenantId = XeniaEndpointContext.ResolveTenantId(context);
            var conversation = service.CreateConversation(tenantId, XeniaEndpointContext.ResolveActorUserId(context), request);
            return Results.Created($"/xenia/conversations/{conversation.ConversationId}", conversation);
        });
        userRoutes.MapGet("/conversations", (HttpContext context, IXeniaService service) =>
        {
            XeniaEndpointContext.RequireXeniaAccess(context);
            return Results.Ok(service.ListConversations(XeniaEndpointContext.ResolveTenantId(context)));
        });
        userRoutes.MapGet("/conversations/{conversationId:guid}", (Guid conversationId, HttpContext context, IXeniaService service) =>
        {
            XeniaEndpointContext.RequireXeniaAccess(context);
            return Results.Ok(service.GetConversation(XeniaEndpointContext.ResolveTenantId(context), conversationId));
        });
        userRoutes.MapPost("/conversations/{conversationId:guid}/messages", (Guid conversationId, XeniaConversationMessageRequest request, HttpContext context, IXeniaService service) =>
        {
            XeniaEndpointContext.RequireXeniaAccess(context);
            return Results.Ok(service.AddConversationMessage(
                XeniaEndpointContext.ResolveTenantId(context),
                conversationId,
                XeniaEndpointContext.ResolveActorUserId(context),
                request));
        });
        userRoutes.MapPost("/conversations/{conversationId:guid}/messages/stream", (Guid conversationId, XeniaConversationMessageRequest request, HttpContext context, IXeniaService service) =>
        {
            XeniaEndpointContext.RequireXeniaAccess(context);
            var response = service.AddConversationMessage(
                XeniaEndpointContext.ResolveTenantId(context),
                conversationId,
                XeniaEndpointContext.ResolveActorUserId(context),
                request);

            return Results.Text(
                XeniaEndpointContext.ToServerSentEvents(response.OutputChunks, response.AssistantMessage.MessageId),
                "text/event-stream");
        });

        return routes;
    }
}
