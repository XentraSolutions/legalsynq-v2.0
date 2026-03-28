using BuildingBlocks.Authorization;
using BuildingBlocks.Context;
using CareConnect.Application.DTOs;
using CareConnect.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CareConnect.Api.Endpoints;

public static class SlotEndpoints
{
    public static void MapSlotEndpoints(this WebApplication app)
    {
        app.MapPost("/api/providers/{providerId:guid}/slots/generate", async (
            Guid providerId,
            [FromBody] GenerateSlotsRequest request,
            ISlotGenerationService service,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            var result = await service.GenerateSlotsAsync(tenantId, providerId, ctx.UserId, request, ct);
            return Results.Ok(result);
        })
        .RequireAuthorization(Policies.PlatformOrTenantAdmin);

        app.MapGet("/api/slots", async (
            [AsParameters] SlotSearchParams query,
            IAppointmentService service,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            var result = await service.SearchSlotsAsync(tenantId, query, ct);
            return Results.Ok(result);
        })
        .RequireAuthorization(Policies.AuthenticatedUser);
    }
}
