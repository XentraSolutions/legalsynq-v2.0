using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using BuildingBlocks.Context;
using SynqComm.Application.DTOs;
using SynqComm.Application.Interfaces;
using SynqComm.Domain;

namespace SynqComm.Api.Endpoints;

public static class AttachmentEndpoints
{
    public static void MapAttachmentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/synqcomm/conversations/{conversationId:guid}/messages/{messageId:guid}/attachments")
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(SynqCommPermissions.ProductCode);

        group.MapGet("/", ListByMessage)
            .RequirePermission(SynqCommPermissions.MessageRead);

        group.MapPost("/", LinkAttachment)
            .RequirePermission(SynqCommPermissions.AttachmentManage);

        group.MapDelete("/{attachmentId:guid}", RemoveAttachment)
            .RequirePermission(SynqCommPermissions.AttachmentManage);
    }

    private static Guid RequireTenantId(ICurrentRequestContext ctx) =>
        ctx.TenantId ?? throw new UnauthorizedAccessException("Tenant context is required.");

    private static Guid RequireUserId(ICurrentRequestContext ctx) =>
        ctx.UserId ?? throw new UnauthorizedAccessException("User context is required.");

    private static async Task<IResult> ListByMessage(
        Guid conversationId,
        Guid messageId,
        IMessageAttachmentService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId = RequireUserId(ctx);
        var result = await service.ListByMessageAsync(tenantId, userId, conversationId, messageId, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> LinkAttachment(
        Guid conversationId,
        Guid messageId,
        AddMessageAttachmentRequest request,
        IMessageAttachmentService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId = RequireUserId(ctx);
        var result = await service.LinkAttachmentAsync(tenantId, userId, conversationId, messageId, request, ct);
        return Results.Created(
            $"/api/synqcomm/conversations/{conversationId}/messages/{messageId}/attachments/{result.Id}",
            result);
    }

    private static async Task<IResult> RemoveAttachment(
        Guid conversationId,
        Guid messageId,
        Guid attachmentId,
        IMessageAttachmentService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId = RequireUserId(ctx);
        await service.RemoveAttachmentAsync(tenantId, userId, conversationId, messageId, attachmentId, ct);
        return Results.NoContent();
    }
}
