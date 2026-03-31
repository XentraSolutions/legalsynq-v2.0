using BuildingBlocks.Authorization;
using BuildingBlocks.Context;
using BuildingBlocks.Exceptions;
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

            // LSCC-001: Org-participant scoping — referrers see outbound, receivers see addressed.
            // Admins and PlatformAdmin bypass capability checks and see all referrals.
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

        // LSCC-002: Row-level access control — caller must be an admin or a participant
        // (ReferringOrganizationId or ReceivingOrganizationId matches their org).
        // Returns 404 (not 403) for non-participants to avoid confirming record existence.
        group.MapGet("/{id:guid}", async (
            Guid id,
            IReferralService service,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            var referral = await service.GetByIdAsync(tenantId, id, ct);

            if (!CareConnectParticipantHelper.IsAdmin(ctx))
            {
                // Re-construct the domain participant view from the response DTO org IDs.
                var isParticipant =
                    (ctx.OrgId.HasValue && referral.ReferringOrganizationId == ctx.OrgId) ||
                    (ctx.OrgId.HasValue && referral.ReceivingOrganizationId  == ctx.OrgId);

                if (!isParticipant)
                    return Results.NotFound();
            }

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

        // ── LSCC-005: Public token-based endpoints (no auth required) ──────────

        // Resolves a view token to determine how to route the provider:
        //   "pending"  → route to public accept page (/referrals/accept/{id}?token=...)
        //   "active"   → route through platform login then to /careconnect/referrals/{id}
        //   "invalid"  → token is bad or expired
        //   "notfound" → referral was deleted
        group.MapGet("/resolve-view-token", async (
            [FromQuery] string token,
            IReferralService service,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(token))
                return Results.BadRequest(new { error = "token is required." });

            var result = await service.ResolveViewTokenAsync(token, ct);
            return Results.Ok(result);
        });
        // Note: no .RequireAuthorization — intentionally public

        // Accepts a referral on behalf of a pending (unlinked) provider.
        // The token proves the provider received the notification email.
        group.MapPost("/{id:guid}/accept-by-token", async (
            Guid id,
            [FromBody] AcceptByTokenRequest request,
            IReferralService service,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Token))
                return Results.BadRequest(new { error = "token is required." });

            try
            {
                var referral = await service.AcceptByTokenAsync(id, request.Token, ct);
                return Results.Ok(referral);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Problem(
                    detail: ex.Message,
                    statusCode: StatusCodes.Status401Unauthorized,
                    title: "Invalid or expired token");
            }
            catch (NotFoundException)
            {
                return Results.NotFound();
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        });
        // Note: no .RequireAuthorization — intentionally public
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
