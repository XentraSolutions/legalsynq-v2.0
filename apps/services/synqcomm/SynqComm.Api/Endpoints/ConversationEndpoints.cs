using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using BuildingBlocks.Context;
using SynqComm.Application.DTOs;
using SynqComm.Application.Interfaces;
using SynqComm.Domain;

namespace SynqComm.Api.Endpoints;

public static class ConversationEndpoints
{
    public static void MapConversationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/synqcomm/conversations")
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(SynqCommPermissions.ProductCode);

        group.MapGet("/", ListByContext)
            .RequirePermission(SynqCommPermissions.ConversationRead);

        group.MapGet("/{id:guid}", GetById)
            .RequirePermission(SynqCommPermissions.ConversationRead);

        group.MapPost("/", Create)
            .RequirePermission(SynqCommPermissions.ConversationCreate);

        group.MapPatch("/{id:guid}/status", UpdateStatus)
            .RequirePermission(SynqCommPermissions.ConversationUpdate);
    }

    private static Guid RequireTenantId(ICurrentRequestContext ctx) =>
        ctx.TenantId ?? throw new UnauthorizedAccessException("Tenant context is required.");

    private static Guid RequireUserId(ICurrentRequestContext ctx) =>
        ctx.UserId ?? throw new UnauthorizedAccessException("User context is required.");

    private static Guid RequireOrgId(ICurrentRequestContext ctx) =>
        ctx.OrgId ?? throw new UnauthorizedAccessException("Organization context is required.");

    private static async Task<IResult> ListByContext(
        IConversationService service,
        ICurrentRequestContext ctx,
        string contextType,
        string contextId,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var result = await service.ListByContextAsync(tenantId, contextType, contextId, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetById(
        Guid id,
        IConversationService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var result = await service.GetByIdAsync(tenantId, id, ct);
        return result is null
            ? Results.NotFound(new { error = new { code = "not_found", message = $"Conversation '{id}' not found." } })
            : Results.Ok(result);
    }

    private static async Task<IResult> Create(
        CreateConversationRequest request,
        IConversationService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var orgId = RequireOrgId(ctx);
        var userId = RequireUserId(ctx);
        var result = await service.CreateAsync(tenantId, orgId, userId, request, ct);
        return Results.Created($"/api/synqcomm/conversations/{result.Id}", result);
    }

    private static async Task<IResult> UpdateStatus(
        Guid id,
        UpdateConversationStatusRequest request,
        IConversationService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId = RequireUserId(ctx);
        var result = await service.UpdateStatusAsync(tenantId, id, userId, request, ct);
        return Results.Ok(result);
    }
}
