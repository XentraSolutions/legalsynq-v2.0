using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using BuildingBlocks.Context;
using SynqComm.Application.DTOs;
using SynqComm.Application.Interfaces;
using SynqComm.Domain;

namespace SynqComm.Api.Endpoints;

public static class EmailIntakeEndpoints
{
    public static void MapEmailIntakeEndpoints(this WebApplication app)
    {
        app.MapPost("/api/synqcomm/email/intake", ProcessInbound)
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(SynqCommPermissions.ProductCode)
            .RequirePermission(SynqCommPermissions.EmailIntake);

        app.MapGet("/api/synqcomm/conversations/{conversationId:guid}/email-references", ListEmailReferences)
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(SynqCommPermissions.ProductCode)
            .RequirePermission(SynqCommPermissions.ConversationRead);
    }

    private static async Task<IResult> ProcessInbound(
        InboundEmailIntakeRequest request,
        IEmailIntakeService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = ctx.TenantId ?? throw new UnauthorizedAccessException("Tenant context is required.");
        var orgId = ctx.OrgId ?? throw new UnauthorizedAccessException("Organization context is required.");

        if (request.TenantId != tenantId)
            throw new UnauthorizedAccessException("TenantId in request body does not match authenticated context.");
        if (request.OrgId != orgId)
            throw new UnauthorizedAccessException("OrgId in request body does not match authenticated context.");

        var result = await service.ProcessInboundAsync(request, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> ListEmailReferences(
        Guid conversationId,
        IEmailIntakeService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = ctx.TenantId ?? throw new UnauthorizedAccessException("Tenant context is required.");
        var userId = ctx.UserId ?? throw new UnauthorizedAccessException("User context is required.");
        var result = await service.ListEmailReferencesAsync(tenantId, conversationId, userId, ct);
        return Results.Ok(result);
    }
}
