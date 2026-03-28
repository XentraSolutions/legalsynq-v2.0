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
            [AsParameters] ProviderSearchParams p,
            IProviderService service,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");

            var query = new GetProvidersQuery
            {
                Name               = p.Name,
                CategoryCode       = p.CategoryCode,
                City               = p.City,
                State              = p.State,
                AcceptingReferrals = p.AcceptingReferrals,
                IsActive           = p.IsActive,
                Page               = p.Page     ?? 1,
                PageSize           = p.PageSize ?? 20,
                Latitude           = p.Latitude,
                Longitude          = p.Longitude,
                RadiusMiles        = p.RadiusMiles,
                NorthLat           = p.NorthLat,
                SouthLat           = p.SouthLat,
                EastLng            = p.EastLng,
                WestLng            = p.WestLng
            };

            var result = await service.SearchAsync(tenantId, query, ct);
            return Results.Ok(result);
        })
        .RequireAuthorization(Policies.AuthenticatedUser);

        group.MapGet("/map", async (
            [AsParameters] ProviderSearchParams p,
            IProviderService service,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");

            var query = new GetProvidersQuery
            {
                Name               = p.Name,
                CategoryCode       = p.CategoryCode,
                City               = p.City,
                State              = p.State,
                AcceptingReferrals = p.AcceptingReferrals,
                IsActive           = p.IsActive,
                Page               = 1,
                PageSize           = 500,
                Latitude           = p.Latitude,
                Longitude          = p.Longitude,
                RadiusMiles        = p.RadiusMiles,
                NorthLat           = p.NorthLat,
                SouthLat           = p.SouthLat,
                EastLng            = p.EastLng,
                WestLng            = p.WestLng
            };

            var markers = await service.GetMarkersAsync(tenantId, query, ct);
            return Results.Ok(markers);
        })
        .RequireAuthorization(Policies.AuthenticatedUser);

        group.MapGet("/{id:guid}", async (
            Guid id,
            IProviderService service,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            var provider = await service.GetByIdAsync(tenantId, id, ct);
            return Results.Ok(provider);
        })
        .RequireAuthorization(Policies.AuthenticatedUser);

        group.MapPost("/", async (
            [FromBody] CreateProviderRequest request,
            IProviderService service,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            var provider = await service.CreateAsync(tenantId, ctx.UserId, request, ct);
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
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            var provider = await service.UpdateAsync(tenantId, id, ctx.UserId, request, ct);
            return Results.Ok(provider);
        })
        .RequireAuthorization(Policies.PlatformOrTenantAdmin);
    }
}

internal sealed class ProviderSearchParams
{
    public string? Name               { get; init; }
    public string? CategoryCode       { get; init; }
    public string? City               { get; init; }
    public string? State              { get; init; }
    public bool?   AcceptingReferrals { get; init; }
    public bool?   IsActive           { get; init; }
    public int?    Page               { get; init; }
    public int?    PageSize           { get; init; }
    public double? Latitude           { get; init; }
    public double? Longitude          { get; init; }
    public double? RadiusMiles        { get; init; }
    public double? NorthLat           { get; init; }
    public double? SouthLat           { get; init; }
    public double? EastLng            { get; init; }
    public double? WestLng            { get; init; }
}
