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

    private static async Task<IResult> GetPendingAsync(
        IActivationRequestService service,
        CancellationToken         ct)
    {
        var items = await service.GetPendingAsync(ct);
        return Results.Ok(new { items, count = items.Count });
    }

    private static async Task<IResult> GetByIdAsync(
        Guid                      id,
        IActivationRequestService service,
        CancellationToken         ct)
    {
        var detail = await service.GetByIdAsync(id, ct);
        if (detail is null)
            return Results.NotFound(new { error = $"ActivationRequest '{id}' was not found." });

        return Results.Ok(detail);
    }

    private static async Task<IResult> ApproveAsync(
        Guid                      id,
        [FromBody] ApproveActivationRequest request,
        IActivationRequestService service,
        ICurrentRequestContext    ctx,
        CancellationToken         ct)
    {
        if (request.OrganizationId == Guid.Empty)
            return Results.BadRequest(new { error = "organizationId is required and must be a valid GUID." });

        var approvedByUserId = ctx.UserId;

        var result = await service.ApproveAsync(id, request.OrganizationId, approvedByUserId, ct);
        return Results.Ok(result);
    }
}
