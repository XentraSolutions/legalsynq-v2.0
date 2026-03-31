using BuildingBlocks.Authorization;
using BuildingBlocks.Context;
using CareConnect.Application.Authorization;
using CareConnect.Application.DTOs;
using CareConnect.Application.Interfaces;
using CareConnect.Domain;
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
            AuthorizationService authSvc,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");

            // Referrers see their org's outbound referrals; receivers see addressed referrals.
            // Admins and PlatformAdmin see all (no org filter applied by helper since they bypass).
            var isReceiver = await authSvc.IsAuthorizedAsync(ctx, CapabilityCodes.ReferralReadAddressed, ct);

            var query = new GetReferralsQuery
            {
                Status             = p.Status,
                ProviderId         = p.ProviderId,
                ClientName         = p.ClientName,
                CaseNumber         = p.CaseNumber,
                Urgency            = p.Urgency,
                CreatedFrom        = p.CreatedFrom,
                CreatedTo          = p.CreatedTo,
                Page               = p.Page ?? 1,
                PageSize           = p.PageSize ?? 20,
                ReferringOrgId     = (!isReceiver && !ctx.IsPlatformAdmin
                                       && !ctx.Roles.Contains(Roles.TenantAdmin))
                                     ? ctx.OrgId : null,
                ReceivingOrgId     = (isReceiver && !ctx.IsPlatformAdmin
                                       && !ctx.Roles.Contains(Roles.TenantAdmin))
                                     ? ctx.OrgId : null,
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
            AuthorizationService authSvc,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            await CareConnectAuthHelper.RequireAsync(ctx, authSvc, CapabilityCodes.ReferralCreate, ct);
            var referral = await service.CreateAsync(tenantId, ctx.UserId, request, ct);
            return Results.Created($"/api/referrals/{referral.Id}", referral);
        })
        .RequireAuthorization(Policies.AuthenticatedUser);

        group.MapPut("/{id:guid}", async (
            Guid id,
            [FromBody] UpdateReferralRequest request,
            IReferralService service,
            ICurrentRequestContext ctx,
            AuthorizationService authSvc,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");

            // Determine the required capability based on the target status.
            var requiredCapability = ReferralWorkflowRules.RequiredCapabilityFor(request.Status);
            await CareConnectAuthHelper.RequireAsync(ctx, authSvc, requiredCapability, ct);

            var referral = await service.UpdateAsync(tenantId, id, ctx.UserId, request, ct);
            return Results.Ok(referral);
        })
        .RequireAuthorization(Policies.AuthenticatedUser);
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
