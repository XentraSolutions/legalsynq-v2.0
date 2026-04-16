using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using BuildingBlocks.Context;
using SynqComm.Application.DTOs;
using SynqComm.Application.Interfaces;
using SynqComm.Domain;

namespace SynqComm.Api.Endpoints;

public static class SenderConfigEndpoints
{
    public static void MapSenderConfigEndpoints(this WebApplication app)
    {
        app.MapPost("/api/synqcomm/email/sender-configs", CreateSenderConfig)
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(SynqCommPermissions.ProductCode)
            .RequirePermission(SynqCommPermissions.EmailConfigManage);

        app.MapGet("/api/synqcomm/email/sender-configs", ListSenderConfigs)
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(SynqCommPermissions.ProductCode)
            .RequirePermission(SynqCommPermissions.EmailConfigManage);

        app.MapGet("/api/synqcomm/email/sender-configs/{id:guid}", GetSenderConfig)
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(SynqCommPermissions.ProductCode)
            .RequirePermission(SynqCommPermissions.EmailConfigManage);

        app.MapPatch("/api/synqcomm/email/sender-configs/{id:guid}", UpdateSenderConfig)
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(SynqCommPermissions.ProductCode)
            .RequirePermission(SynqCommPermissions.EmailConfigManage);
    }

    private static async Task<IResult> CreateSenderConfig(
        CreateTenantEmailSenderConfigRequest request,
        ISenderConfigService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = ctx.TenantId ?? throw new UnauthorizedAccessException("Tenant context is required.");
        var userId = ctx.UserId ?? throw new UnauthorizedAccessException("User context is required.");

        var result = await service.CreateAsync(request, tenantId, userId, ct);
        return Results.Created($"/api/synqcomm/email/sender-configs/{result.Id}", result);
    }

    private static async Task<IResult> ListSenderConfigs(
        ISenderConfigService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = ctx.TenantId ?? throw new UnauthorizedAccessException("Tenant context is required.");

        var result = await service.ListAsync(tenantId, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetSenderConfig(
        Guid id,
        ISenderConfigService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = ctx.TenantId ?? throw new UnauthorizedAccessException("Tenant context is required.");

        var result = await service.GetByIdAsync(tenantId, id, ct);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> UpdateSenderConfig(
        Guid id,
        UpdateTenantEmailSenderConfigRequest request,
        ISenderConfigService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = ctx.TenantId ?? throw new UnauthorizedAccessException("Tenant context is required.");
        var userId = ctx.UserId ?? throw new UnauthorizedAccessException("User context is required.");

        var result = await service.UpdateAsync(id, request, tenantId, userId, ct);
        return Results.Ok(result);
    }
}
