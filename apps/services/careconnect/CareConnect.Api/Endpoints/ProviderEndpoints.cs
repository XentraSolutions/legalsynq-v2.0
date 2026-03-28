using BuildingBlocks.Authorization;
using BuildingBlocks.Context;
using CareConnect.Application.DTOs;
using CareConnect.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CareConnect.Api.Endpoints;

public static class ProviderEndpoints
{
    public static void MapProviderEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/providers");

        group.MapGet("/", async (
            IProviderService service,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var providers = await service.GetAllAsync(ctx.TenantId, ct);
            return Results.Ok(providers);
        })
        .RequireAuthorization(Policies.AuthenticatedUser);

        group.MapGet("/{id:guid}", async (
            Guid id,
            IProviderService service,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var provider = await service.GetByIdAsync(ctx.TenantId, id, ct);
            return Results.Ok(provider);
        })
        .RequireAuthorization(Policies.AuthenticatedUser);

        group.MapPost("/", async (
            [FromBody] CreateProviderRequest request,
            IProviderService service,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var provider = await service.CreateAsync(ctx.TenantId, ctx.UserId, request, ct);
            return Results.Created($"/api/providers/{provider.Id}", provider);
        })
        .RequireAuthorization(Policies.PlatformOrTenantAdmin);

        group.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] UpdateProviderRequest request,
            IProviderService service,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var provider = await service.UpdateAsync(ctx.TenantId, id, ctx.UserId, request, ct);
            return Results.Ok(provider);
        })
        .RequireAuthorization(Policies.PlatformOrTenantAdmin);
    }
}
