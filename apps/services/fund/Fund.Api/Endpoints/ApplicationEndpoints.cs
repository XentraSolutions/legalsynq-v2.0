using BuildingBlocks.Authorization;
using BuildingBlocks.Context;
using BuildingBlocks.Exceptions;
using Fund.Application.DTOs;
using Fund.Application.Interfaces;

namespace Fund.Api.Endpoints;

public static class ApplicationEndpoints
{
    public static void MapApplicationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/applications");

        group.MapGet("/", async (ICurrentRequestContext ctx, IApplicationService svc, CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            var results = await svc.GetAllAsync(tenantId, ct);
            return Results.Ok(results);
        }).RequireAuthorization(Policies.AuthenticatedUser);

        group.MapGet("/{id:guid}", async (Guid id, ICurrentRequestContext ctx, IApplicationService svc, CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            var result = await svc.GetByIdAsync(tenantId, id, ct);
            if (result is null) throw new NotFoundException($"Application '{id}' was not found.");
            return Results.Ok(result);
        }).RequireAuthorization(Policies.AuthenticatedUser);

        group.MapPost("/", async (CreateApplicationRequest request, ICurrentRequestContext ctx, IApplicationService svc, CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            var userId = ctx.UserId ?? throw new InvalidOperationException("sub claim is missing.");
            var result = await svc.CreateAsync(tenantId, userId, request, ct);
            return Results.Created($"/api/applications/{result.Id}", result);
        }).RequireAuthorization(Policies.PlatformOrTenantAdmin);

        group.MapPut("/{id:guid}", async (Guid id, UpdateApplicationRequest request, ICurrentRequestContext ctx, IApplicationService svc, CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            var userId = ctx.UserId ?? throw new InvalidOperationException("sub claim is missing.");
            var result = await svc.UpdateAsync(tenantId, id, userId, request, ct);
            return Results.Ok(result);
        }).RequireAuthorization(Policies.PlatformOrTenantAdmin);
    }
}
