using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using BuildingBlocks.Context;
using SynqComm.Application.DTOs;
using SynqComm.Application.Interfaces;
using SynqComm.Domain;

namespace SynqComm.Api.Endpoints;

public static class MessageEndpoints
{
    public static void MapMessageEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/synqcomm/conversations/{conversationId:guid}/messages")
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(SynqCommPermissions.ProductCode);

        group.MapGet("/", ListByConversation)
            .RequirePermission(SynqCommPermissions.MessageRead);

        group.MapPost("/", AddMessage)
            .RequirePermission(SynqCommPermissions.MessageCreate);
    }

    private static Guid RequireTenantId(ICurrentRequestContext ctx) =>
        ctx.TenantId ?? throw new UnauthorizedAccessException("Tenant context is required.");

    private static Guid RequireUserId(ICurrentRequestContext ctx) =>
        ctx.UserId ?? throw new UnauthorizedAccessException("User context is required.");

    private static Guid RequireOrgId(ICurrentRequestContext ctx) =>
        ctx.OrgId ?? throw new UnauthorizedAccessException("Organization context is required.");

    private static async Task<IResult> ListByConversation(
        Guid conversationId,
        IMessageService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId = RequireUserId(ctx);
        var result = await service.ListByConversationAsync(tenantId, conversationId, userId, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> AddMessage(
        Guid conversationId,
        AddMessageRequest request,
        IMessageService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var orgId = RequireOrgId(ctx);
        var userId = RequireUserId(ctx);
        var result = await service.AddAsync(tenantId, orgId, userId, conversationId, request, ct);
        return Results.Created($"/api/synqcomm/conversations/{conversationId}/messages/{result.Id}", result);
    }
}
