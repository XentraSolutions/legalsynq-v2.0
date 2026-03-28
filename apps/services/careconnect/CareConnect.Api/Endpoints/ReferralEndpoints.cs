using BuildingBlocks.Authorization;
using BuildingBlocks.Context;
using CareConnect.Application.DTOs;
using CareConnect.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CareConnect.Api.Endpoints;

public static class ReferralEndpoints
{
    public static void MapReferralEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/referrals");

        group.MapGet("/", async (
            IReferralService service,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var referrals = await service.GetAllAsync(ctx.TenantId, ct);
            return Results.Ok(referrals);
        })
        .RequireAuthorization(Policies.AuthenticatedUser);

        group.MapGet("/{id:guid}", async (
            Guid id,
            IReferralService service,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var referral = await service.GetByIdAsync(ctx.TenantId, id, ct);
            return Results.Ok(referral);
        })
        .RequireAuthorization(Policies.AuthenticatedUser);

        group.MapPost("/", async (
            [FromBody] CreateReferralRequest request,
            IReferralService service,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var referral = await service.CreateAsync(ctx.TenantId, ctx.UserId, request, ct);
            return Results.Created($"/api/referrals/{referral.Id}", referral);
        })
        .RequireAuthorization(Policies.PlatformOrTenantAdmin);

        group.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] UpdateReferralRequest request,
            IReferralService service,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var referral = await service.UpdateAsync(ctx.TenantId, id, ctx.UserId, request, ct);
            return Results.Ok(referral);
        })
        .RequireAuthorization(Policies.PlatformOrTenantAdmin);
    }
}
