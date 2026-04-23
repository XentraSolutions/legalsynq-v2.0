// LSCC-009: Admin activation queue endpoints.
// All endpoints require PlatformOrTenantAdmin authorization.
//
// GET  /api/admin/activations          — list pending activation requests
// GET  /api/admin/activations/{id}     — detail for one activation request
// POST /api/admin/activations/{id}/approve — approve + link provider to org
using BuildingBlocks.Authorization;
using BuildingBlocks.Context;
using CareConnect.Application.DTOs;
using CareConnect.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CareConnect.Api.Endpoints;

public static class ActivationAdminEndpoints
{
    public static IEndpointRouteBuilder MapActivationAdminEndpoints(
        this IEndpointRouteBuilder routes)
    {
        // GET /api/admin/activations
        routes
            .MapGet("/api/admin/activations", GetPendingAsync)
            .RequireAuthorization(Policies.PlatformOrTenantAdmin);

        // GET /api/admin/activations/{id}
        routes
            .MapGet("/api/admin/activations/{id:guid}", GetByIdAsync)
            .RequireAuthorization(Policies.PlatformOrTenantAdmin);

        // POST /api/admin/activations/{id}/approve
        routes
            .MapPost("/api/admin/activations/{id:guid}/approve", ApproveAsync)
            .RequireAuthorization(Policies.PlatformOrTenantAdmin);

        return routes;
    }

    // ── Handlers ──────────────────────────────────────────────────────────────

    // BLK-SEC-02: TenantAdmin callers are scoped to their own tenant.
    // PlatformAdmin sees the full platform-wide queue.
    private static async Task<IResult> GetPendingAsync(
        IActivationRequestService service,
        ICurrentRequestContext    ctx,
        CancellationToken         ct)
    {
        var items = await service.GetPendingAsync(ct);

        // Non-PlatformAdmin: filter to caller's tenant only.
        if (!ctx.IsPlatformAdmin)
        {
            var callerTenantId = ctx.TenantId
                ?? throw new InvalidOperationException("tenant_id claim is missing.");
            items = items.Where(i => i.TenantId == callerTenantId).ToList();
        }

        return Results.Ok(new { items, count = items.Count });
    }

    // BLK-SEC-02: TenantAdmin may only retrieve activation requests for their own tenant.
    private static async Task<IResult> GetByIdAsync(
        Guid                      id,
        IActivationRequestService service,
        ICurrentRequestContext    ctx,
        CancellationToken         ct)
    {
        var detail = await service.GetByIdAsync(id, ct);
        if (detail is null)
            return Results.NotFound(new { error = $"ActivationRequest '{id}' was not found." });

        // Non-PlatformAdmin: reject cross-tenant access.
        if (!ctx.IsPlatformAdmin)
        {
            var callerTenantId = ctx.TenantId
                ?? throw new InvalidOperationException("tenant_id claim is missing.");
            if (detail.TenantId != callerTenantId)
                return Results.Forbid();
        }

        return Results.Ok(detail);
    }

    // BLK-SEC-02: TenantAdmin may only approve activation requests for their own tenant.
    private static async Task<IResult> ApproveAsync(
        Guid                      id,
        [FromBody] ApproveActivationRequest request,
        IActivationRequestService service,
        ICurrentRequestContext    ctx,
        CancellationToken         ct)
    {
        if (request.OrganizationId == Guid.Empty)
            return Results.BadRequest(new { error = "organizationId is required and must be a valid GUID." });

        // Non-PlatformAdmin: verify the activation request belongs to the caller's tenant
        // before executing the approval.
        if (!ctx.IsPlatformAdmin)
        {
            var callerTenantId = ctx.TenantId
                ?? throw new InvalidOperationException("tenant_id claim is missing.");
            var detail = await service.GetByIdAsync(id, ct);
            if (detail is null)
                return Results.NotFound(new { error = $"ActivationRequest '{id}' was not found." });
            if (detail.TenantId != callerTenantId)
                return Results.Forbid();
        }

        var result = await service.ApproveAsync(id, request.OrganizationId, ctx.UserId, ct);
        return Results.Ok(result);
    }
}
