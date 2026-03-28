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
            [AsParameters] ReferralSearchParams p,
            IReferralService service,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");

            var query = new GetReferralsQuery
            {
                Status      = p.Status,
                ProviderId  = p.ProviderId,
                ClientName  = p.ClientName,
                CaseNumber  = p.CaseNumber,
                Urgency     = p.Urgency,
                CreatedFrom = p.CreatedFrom,
                CreatedTo   = p.CreatedTo,
                Page        = p.Page ?? 1,
                PageSize    = p.PageSize ?? 20
            };

            var result = await service.SearchAsync(tenantId, query, ct);
            return Results.Ok(result);
        })
        .RequireAuthorization(Policies.AuthenticatedUser);

        group.MapGet("/{id:guid}", async (
            Guid id,
            IReferralService service,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            var referral = await service.GetByIdAsync(tenantId, id, ct);
            return Results.Ok(referral);
        })
        .RequireAuthorization(Policies.AuthenticatedUser);

        group.MapGet("/{id:guid}/history", async (
            Guid id,
            IReferralService service,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            var history = await service.GetHistoryAsync(tenantId, id, ct);
            return Results.Ok(history);
        })
        .RequireAuthorization(Policies.AuthenticatedUser);

        group.MapPost("/", async (
            [FromBody] CreateReferralRequest request,
            IReferralService service,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            var referral = await service.CreateAsync(tenantId, ctx.UserId, request, ct);
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
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            var referral = await service.UpdateAsync(tenantId, id, ctx.UserId, request, ct);
            return Results.Ok(referral);
        })
        .RequireAuthorization(Policies.PlatformOrTenantAdmin);
    }
}

internal sealed class ReferralSearchParams
{
    public string?   Status      { get; init; }
    public Guid?     ProviderId  { get; init; }
    public string?   ClientName  { get; init; }
    public string?   CaseNumber  { get; init; }
    public string?   Urgency     { get; init; }
    public DateTime? CreatedFrom { get; init; }
    public DateTime? CreatedTo   { get; init; }
    public int?      Page        { get; init; }
    public int?      PageSize    { get; init; }
}
