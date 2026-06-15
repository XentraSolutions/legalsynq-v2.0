using System.Security.Claims;
using BuildingBlocks.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tenant.Application.DTOs;
using Tenant.Application.Interfaces;

namespace Tenant.Api.Endpoints;

public static class SettingEndpoints
{
    private const string MapProviderKey     = "careconnect.map-provider";
    private const string MapProviderProduct = "careconnect";

    public static WebApplication MapSettingEndpoints(this WebApplication app)
    {
        // ── Platform-admin CRUD (unchanged) ──────────────────────────────────

        var group = app.MapGroup("/api/tenants/{tenantId:guid}/settings")
            .RequireAuthorization(Policies.AdminOnly);

        group.MapGet("/", async (
            Guid tenantId,
            ISettingService service,
            CancellationToken ct) =>
        {
            var result = await service.ListByTenantAsync(tenantId, ct);
            return Results.Ok(result);
        });

        group.MapGet("/{id:guid}", async (
            Guid tenantId,
            Guid id,
            ISettingService service,
            CancellationToken ct) =>
        {
            var result = await service.GetByIdAsync(tenantId, id, ct);
            return Results.Ok(result);
        });

        group.MapPut("/", async (
            Guid tenantId,
            [FromBody] UpsertSettingRequest request,
            ISettingService service,
            CancellationToken ct) =>
        {
            var result = await service.UpsertAsync(tenantId, request, ct);
            return Results.Ok(result);
        });

        group.MapDelete("/{id:guid}", async (
            Guid tenantId,
            Guid id,
            ISettingService service,
            CancellationToken ct) =>
        {
            await service.DeleteAsync(tenantId, id, ct);
            return Results.NoContent();
        });

        // ── TenantAdmin: map-provider setting ─────────────────────────────────
        // GET  /api/tenants/{tenantId}/settings/map-provider  — read current value
        // PUT  /api/tenants/{tenantId}/settings/map-provider  — write value ("google"|"osm")

        var mapGroup = app.MapGroup("/api/tenants/{tenantId:guid}/settings/map-provider")
            .RequireAuthorization(Policies.PlatformOrTenantAdmin);

        mapGroup.MapGet("/", async (
            Guid tenantId,
            ClaimsPrincipal user,
            ISettingService service,
            CancellationToken ct) =>
        {
            var scopeError = EnforceTenantScope(user, tenantId);
            if (scopeError is not null) return scopeError;

            var setting = await service.GetByKeyAsync(tenantId, MapProviderKey, MapProviderProduct, ct);
            return Results.Ok(new MapProviderResponse(setting?.SettingValue ?? "google"));
        });

        mapGroup.MapPut("/", async (
            Guid tenantId,
            ClaimsPrincipal user,
            [FromBody] SetMapProviderRequest request,
            ISettingService service,
            CancellationToken ct) =>
        {
            var scopeError = EnforceTenantScope(user, tenantId);
            if (scopeError is not null) return scopeError;

            if (request.Value != "google" && request.Value != "osm")
                return Results.BadRequest(new { error = "Value must be 'google' or 'osm'." });

            var result = await service.UpsertAsync(tenantId, new UpsertSettingRequest(
                MapProviderKey,
                request.Value,
                "String",
                MapProviderProduct), ct);

            return Results.Ok(new MapProviderResponse(result.SettingValue));
        });

        return app;
    }

    private static IResult? EnforceTenantScope(ClaimsPrincipal user, Guid tenantId)
    {
        if (user.IsInRole(Roles.PlatformAdmin)) return null;
        if (!user.IsInRole(Roles.TenantAdmin))  return Results.Forbid();

        var tenantClaim = user.FindFirstValue("tenant_id") ?? user.FindFirstValue("tid");
        if (!Guid.TryParse(tenantClaim, out var callerTenantId))
            throw new InvalidOperationException("TenantAdmin caller is missing the tenant_id claim.");

        return callerTenantId == tenantId ? null : Results.Forbid();
    }
}

public record MapProviderResponse(string Value);
public record SetMapProviderRequest(string Value);
