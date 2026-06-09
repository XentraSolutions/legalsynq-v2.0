using System.Security.Claims;
using BuildingBlocks.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tenant.Application.DTOs;
using Tenant.Application.Interfaces;

namespace Tenant.Api.Endpoints;

public static class CareConnectAccessCodeEndpoints
{
    public static void MapCareConnectAccessCodeEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/tenants/{tenantId:guid}/careconnect/public-network/access-code")
            .RequireAuthorization(Policies.PlatformOrTenantAdmin);

        admin.MapGet("/", async (
            Guid tenantId,
            ClaimsPrincipal user,
            ICareConnectAccessCodeService service,
            CancellationToken ct) =>
        {
            var scopeError = EnforceTenantScope(user, tenantId);
            if (scopeError is not null) return scopeError;

            var result = await service.GetMetadataAsync(tenantId, ct);
            return Results.Ok(result);
        });

        admin.MapPut("/", async (
            Guid tenantId,
            ClaimsPrincipal user,
            [FromBody] SetCareConnectAccessCodeRequest request,
            ICareConnectAccessCodeService service,
            CancellationToken ct) =>
        {
            var scopeError = EnforceTenantScope(user, tenantId);
            if (scopeError is not null) return scopeError;

            var result = await service.SetAsync(tenantId, request.Code, ct);
            return Results.Ok(result);
        });

        admin.MapDelete("/", async (
            Guid tenantId,
            ClaimsPrincipal user,
            ICareConnectAccessCodeService service,
            CancellationToken ct) =>
        {
            var scopeError = EnforceTenantScope(user, tenantId);
            if (scopeError is not null) return scopeError;

            var result = await service.ClearAsync(tenantId, ct);
            return Results.Ok(result);
        });

        var pub = app.MapGroup("/api/v1/public/tenants/{tenantId:guid}/careconnect/public-network/access-code");

        pub.MapGet("/status", async (
            Guid tenantId,
            ICareConnectAccessCodeService service,
            CancellationToken ct) =>
        {
            var result = await service.GetStatusAsync(tenantId, ct);
            return Results.Ok(result);
        }).AllowAnonymous();

        pub.MapPost("/verify", async (
            Guid tenantId,
            [FromBody] VerifyCareConnectAccessCodeRequest request,
            ICareConnectAccessCodeService service,
            CancellationToken ct) =>
        {
            var result = await service.VerifyAsync(tenantId, request.Code, ct);
            return Results.Ok(result);
        }).AllowAnonymous();
    }

    private static IResult? EnforceTenantScope(ClaimsPrincipal user, Guid tenantId)
    {
        if (user.IsInRole(Roles.PlatformAdmin))
        {
            return null;
        }

        if (!user.IsInRole(Roles.TenantAdmin))
        {
            return Results.Forbid();
        }

        var tenantClaim = user.FindFirstValue("tenant_id") ?? user.FindFirstValue("tid");
        if (!Guid.TryParse(tenantClaim, out var callerTenantId))
        {
            throw new InvalidOperationException("TenantAdmin caller is missing the tenant_id claim.");
        }

        return callerTenantId == tenantId ? null : Results.Forbid();
    }
}
