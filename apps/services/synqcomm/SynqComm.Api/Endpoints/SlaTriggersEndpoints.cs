using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using BuildingBlocks.Context;
using SynqComm.Application.DTOs;
using SynqComm.Application.Interfaces;
using SynqComm.Domain;

namespace SynqComm.Api.Endpoints;

public static class SlaTriggersEndpoints
{
    private static readonly Guid SystemUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public static void MapSlaTriggersEndpoints(this WebApplication app)
    {
        var internalGroup = app.MapGroup("/api/synqcomm/internal/sla");

        internalGroup.MapPost("/evaluate", EvaluateSlaTriggers);

        var triggerGroup = app.MapGroup("/api/synqcomm/operational/conversations")
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(SynqCommPermissions.ProductCode);

        triggerGroup.MapGet("/{id:guid}/sla-triggers", GetTriggerState)
            .RequirePermission(SynqCommPermissions.OperationalRead);

        var escalationGroup = app.MapGroup("/api/synqcomm/queues")
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(SynqCommPermissions.ProductCode);

        escalationGroup.MapPost("/{id:guid}/escalation-config", CreateEscalationConfig)
            .RequirePermission(SynqCommPermissions.EscalationConfigManage);

        escalationGroup.MapPatch("/{id:guid}/escalation-config", UpdateEscalationConfig)
            .RequirePermission(SynqCommPermissions.EscalationConfigManage);

        escalationGroup.MapGet("/{id:guid}/escalation-config", GetEscalationConfig)
            .RequirePermission(SynqCommPermissions.QueueRead);
    }

    private static Guid RequireTenantId(ICurrentRequestContext ctx) =>
        ctx.TenantId ?? throw new UnauthorizedAccessException("Tenant context is required.");

    private static Guid RequireUserId(ICurrentRequestContext ctx) =>
        ctx.UserId ?? throw new UnauthorizedAccessException("User context is required.");

    private static async Task<IResult> EvaluateSlaTriggers(
        ISlaNotificationService service,
        HttpContext httpContext,
        CancellationToken ct = default)
    {
        if (httpContext.Items["InternalServiceAuth"] is not true)
            return Results.Json(
                new { error = new { code = "unauthorized", message = "Internal service authentication required." } },
                statusCode: StatusCodes.Status401Unauthorized);

        var tenantIdHeader = httpContext.Request.Headers["X-Tenant-Id"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(tenantIdHeader) || !Guid.TryParse(tenantIdHeader, out var tid))
            return Results.BadRequest(new { error = new { code = "bad_request", message = "X-Tenant-Id header required." } });

        var evalResult = await service.EvaluateAllAsync(tid, SystemUserId, ct);
        return Results.Ok(evalResult);
    }

    private static async Task<IResult> GetTriggerState(
        Guid id,
        ISlaNotificationService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var result = await service.GetTriggerStateAsync(tenantId, id, ct);
        return result is null
            ? Results.NotFound(new { error = new { code = "not_found", message = $"No trigger state for conversation '{id}'." } })
            : Results.Ok(result);
    }

    private static async Task<IResult> CreateEscalationConfig(
        Guid id,
        CreateQueueEscalationConfigRequest request,
        IQueueEscalationConfigService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId = RequireUserId(ctx);
        var result = await service.CreateOrUpdateAsync(tenantId, id, request, userId, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> UpdateEscalationConfig(
        Guid id,
        UpdateQueueEscalationConfigRequest request,
        IQueueEscalationConfigService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId = RequireUserId(ctx);
        var result = await service.UpdateAsync(tenantId, id, request, userId, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetEscalationConfig(
        Guid id,
        IQueueEscalationConfigService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var result = await service.GetByQueueAsync(tenantId, id, ct);
        return result is null
            ? Results.NotFound(new { error = new { code = "not_found", message = $"No escalation config for queue '{id}'." } })
            : Results.Ok(result);
    }
}
