using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using BuildingBlocks.Context;
using SynqComm.Application.DTOs;
using SynqComm.Application.Interfaces;
using SynqComm.Domain;

namespace SynqComm.Api.Endpoints;

public static class OperationalEndpoints
{
    public static void MapOperationalEndpoints(this WebApplication app)
    {
        var convGroup = app.MapGroup("/api/synqcomm/conversations")
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(SynqCommPermissions.ProductCode);

        convGroup.MapPost("/{id:guid}/assign", Assign)
            .RequirePermission(SynqCommPermissions.AssignmentManage);

        convGroup.MapPost("/{id:guid}/reassign", Reassign)
            .RequirePermission(SynqCommPermissions.AssignmentManage);

        convGroup.MapPost("/{id:guid}/unassign", Unassign)
            .RequirePermission(SynqCommPermissions.AssignmentManage);

        convGroup.MapPost("/{id:guid}/accept", Accept)
            .RequirePermission(SynqCommPermissions.OperationalRead);

        convGroup.MapPatch("/{id:guid}/priority", UpdatePriority)
            .RequirePermission(SynqCommPermissions.AssignmentManage);

        convGroup.MapGet("/{id:guid}/operational", GetOperationalSummary)
            .RequirePermission(SynqCommPermissions.OperationalRead);

        var opsGroup = app.MapGroup("/api/synqcomm/operations")
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(SynqCommPermissions.ProductCode);

        opsGroup.MapGet("/", ListOperational)
            .RequirePermission(SynqCommPermissions.OperationalRead);
    }

    private static Guid RequireTenantId(ICurrentRequestContext ctx) =>
        ctx.TenantId ?? throw new UnauthorizedAccessException("Tenant context is required.");

    private static Guid RequireUserId(ICurrentRequestContext ctx) =>
        ctx.UserId ?? throw new UnauthorizedAccessException("User context is required.");

    private static async Task<IResult> Assign(
        Guid id,
        AssignConversationRequest request,
        IAssignmentService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId = RequireUserId(ctx);
        var result = await service.AssignAsync(tenantId, id, userId, request, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> Reassign(
        Guid id,
        ReassignConversationRequest request,
        IAssignmentService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId = RequireUserId(ctx);
        var result = await service.ReassignAsync(tenantId, id, userId, request, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> Unassign(
        Guid id,
        IAssignmentService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId = RequireUserId(ctx);
        var result = await service.UnassignAsync(tenantId, id, userId, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> Accept(
        Guid id,
        IAssignmentService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId = RequireUserId(ctx);
        var result = await service.AcceptAsync(tenantId, id, userId, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> UpdatePriority(
        Guid id,
        UpdateConversationPriorityRequest request,
        IOperationalService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId = RequireUserId(ctx);
        var result = await service.UpdatePriorityAsync(tenantId, id, userId, request, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetOperationalSummary(
        Guid id,
        IOperationalService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var result = await service.GetOperationalSummaryAsync(tenantId, id, ct);
        return result is null
            ? Results.NotFound(new { error = new { code = "not_found", message = $"Conversation '{id}' not found." } })
            : Results.Ok(result);
    }

    private static async Task<IResult> ListOperational(
        IOperationalService service,
        ICurrentRequestContext ctx,
        Guid? queueId = null,
        Guid? assignedUserId = null,
        string? assignmentStatus = null,
        string? priority = null,
        bool? breachedFirstResponse = null,
        bool? breachedResolution = null,
        string? conversationStatus = null,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var query = new OperationalListQuery(
            queueId, assignedUserId, assignmentStatus,
            priority, breachedFirstResponse, breachedResolution,
            conversationStatus);
        var result = await service.ListOperationalAsync(tenantId, query, ct);
        return Results.Ok(result);
    }
}
