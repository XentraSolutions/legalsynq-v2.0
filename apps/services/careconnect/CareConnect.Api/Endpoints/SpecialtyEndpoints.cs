using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using CareConnect.Application.Cache;
using CareConnect.Application.DTOs;
using CareConnect.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace CareConnect.Api.Endpoints;

public static class SpecialtyEndpoints
{
    public static void MapSpecialtyEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/specialties")
            .RequireProductAccess(ProductCodes.SynqCareConnect);

        group.MapGet("/", async (
            [FromQuery] bool? includeInactive,
            ISpecialtyService service,
            IMemoryCache cache,
            CancellationToken ct) =>
        {
            if (includeInactive == true)
            {
                var all = await service.GetAllAsync(includeInactive: true, ct);
                return Results.Ok(all);
            }

            var specialties = await cache.GetOrCreateAsync(
                CareConnectCacheKeys.Specialties,
                entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = CareConnectCacheTtl.Specialties;
                    entry.Size = 1;
                    return service.GetAllAsync(includeInactive: false, ct);
                });

            return Results.Ok(specialties);
        })
        .RequireAuthorization(Policies.AuthenticatedUser);

        group.MapPost("/", async (
            [FromBody] CreateSpecialtyRequest request,
            ISpecialtyService service,
            IMemoryCache cache,
            CancellationToken ct) =>
        {
            var specialty = await service.CreateAsync(request, ct);
            cache.Remove(CareConnectCacheKeys.Specialties);
            return Results.Created($"/api/specialties/{specialty.Id}", specialty);
        })
        .RequireAuthorization(policy => policy.RequireRole(Roles.PlatformAdmin));

        group.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] UpdateSpecialtyRequest request,
            ISpecialtyService service,
            IMemoryCache cache,
            CancellationToken ct) =>
        {
            var specialty = await service.UpdateAsync(id, request, ct);
            cache.Remove(CareConnectCacheKeys.Specialties);
            return Results.Ok(specialty);
        })
        .RequireAuthorization(policy => policy.RequireRole(Roles.PlatformAdmin));

        group.MapDelete("/{id:guid}", async (
            Guid id,
            ISpecialtyService service,
            IMemoryCache cache,
            CancellationToken ct) =>
        {
            await service.DeactivateAsync(id, ct);
            cache.Remove(CareConnectCacheKeys.Specialties);
            return Results.NoContent();
        })
        .RequireAuthorization(policy => policy.RequireRole(Roles.PlatformAdmin));
    }
}
