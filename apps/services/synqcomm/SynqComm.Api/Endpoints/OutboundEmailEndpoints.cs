using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using BuildingBlocks.Context;
using SynqComm.Application.DTOs;
using SynqComm.Application.Interfaces;
using SynqComm.Domain;

namespace SynqComm.Api.Endpoints;

public static class OutboundEmailEndpoints
{
    public static void MapOutboundEmailEndpoints(this WebApplication app)
    {
        app.MapPost("/api/synqcomm/email/send", SendOutbound)
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(SynqCommPermissions.ProductCode)
            .RequirePermission(SynqCommPermissions.EmailSend);

        app.MapPost("/api/synqcomm/email/delivery-status", ProcessDeliveryStatus)
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(SynqCommPermissions.ProductCode)
            .RequirePermission(SynqCommPermissions.EmailDeliveryUpdate);

        app.MapGet("/api/synqcomm/conversations/{conversationId:guid}/email-delivery", ListDeliveryStates)
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(SynqCommPermissions.ProductCode)
            .RequirePermission(SynqCommPermissions.ConversationRead);

        app.MapGet("/api/synqcomm/conversations/{conversationId:guid}/reply-all-preview", GetReplyAllPreview)
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(SynqCommPermissions.ProductCode)
            .RequirePermission(SynqCommPermissions.ConversationRead);
    }

    private static async Task<IResult> SendOutbound(
        SendOutboundEmailRequest request,
        IOutboundEmailService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = ctx.TenantId ?? throw new UnauthorizedAccessException("Tenant context is required.");
        var orgId = ctx.OrgId ?? throw new UnauthorizedAccessException("Organization context is required.");
        var userId = ctx.UserId ?? throw new UnauthorizedAccessException("User context is required.");

        var result = await service.SendOutboundAsync(request, tenantId, orgId, userId, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> ProcessDeliveryStatus(
        DeliveryStatusUpdateRequest request,
        IOutboundEmailService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = ctx.TenantId ?? throw new UnauthorizedAccessException("Tenant context is required.");

        var matched = await service.ProcessDeliveryStatusAsync(request, tenantId, ct);
        return matched ? Results.Ok(new { status = "updated" }) : Results.NotFound(new { status = "not_matched" });
    }

    private static async Task<IResult> ListDeliveryStates(
        Guid conversationId,
        IOutboundEmailService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = ctx.TenantId ?? throw new UnauthorizedAccessException("Tenant context is required.");
        var userId = ctx.UserId ?? throw new UnauthorizedAccessException("User context is required.");

        var result = await service.ListDeliveryStatesAsync(tenantId, conversationId, userId, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetReplyAllPreview(
        Guid conversationId,
        IOutboundEmailService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = ctx.TenantId ?? throw new UnauthorizedAccessException("Tenant context is required.");
        var userId = ctx.UserId ?? throw new UnauthorizedAccessException("User context is required.");

        var result = await service.GetReplyAllPreviewAsync(tenantId, conversationId, userId, ct);
        return Results.Ok(result);
    }
}
