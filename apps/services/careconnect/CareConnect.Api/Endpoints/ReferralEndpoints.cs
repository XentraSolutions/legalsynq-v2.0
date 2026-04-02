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

        // ── LSCC-005-01: Hardening endpoints (authenticated) ────────────────────

        // GET /api/referrals/{id}/notifications — email delivery history for a referral
        group.MapGet("/{id:guid}/notifications", async (
            Guid id,
            IReferralService service,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            var notifs = await service.GetNotificationsAsync(tenantId, id, ct);
            return Results.Ok(notifs);
        })
        .RequireAuthorization(Policies.AuthenticatedUser);

        // POST /api/referrals/{id}/resend-email — resend provider notification email
        // Only available while referral is in New status.
        group.MapPost("/{id:guid}/resend-email", async (
            Guid id,
            IReferralService service,
            ICurrentRequestContext ctx,
            AuthorizationService authSvc,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            await CareConnectAuthHelper.RequireAsync(ctx, authSvc, CapabilityCodes.ReferralCreate, ct);

            try
            {
                var referral = await service.ResendEmailAsync(tenantId, id, ct);
                return Results.Ok(referral);
            }
            catch (NotFoundException)
            {
                return Results.NotFound();
            }
            catch (InvalidOperationException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        })
        .RequireAuthorization(Policies.AuthenticatedUser);

        // POST /api/referrals/{id}/revoke-token — invalidate all previously issued view tokens
        group.MapPost("/{id:guid}/revoke-token", async (
            Guid id,
            IReferralService service,
            ICurrentRequestContext ctx,
            AuthorizationService authSvc,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            await CareConnectAuthHelper.RequireAsync(ctx, authSvc, CapabilityCodes.ReferralCreate, ct);

            try
            {
                var referral = await service.RevokeTokenAsync(tenantId, id, ct);
                return Results.Ok(referral);
            }
            catch (NotFoundException)
            {
                return Results.NotFound();
            }
        })
        .RequireAuthorization(Policies.AuthenticatedUser);

        // GET /api/referrals/{id}/audit — operational audit timeline (LSCC-005-02)
        // Returns status-history + notification events merged and sorted chronologically.
        group.MapGet("/{id:guid}/audit", async (
            Guid id,
            IReferralService service,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            try
            {
                var timeline = await service.GetAuditTimelineAsync(tenantId, id, ct);
                return Results.Ok(timeline);
            }
            catch (NotFoundException)
            {
                return Results.NotFound();
            }
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

        // ── LSCC-008: Provider activation funnel (public, token-gated) ──────────

        // GET /api/referrals/{id}/public-summary?token=...
        // Returns limited referral context for the activation landing page.
        // Token is HMAC-validated + version-checked before any data is returned.
        group.MapGet("/{id:guid}/public-summary", async (
            Guid id,
            [FromQuery] string token,
            IReferralService service,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(token))
                return Results.BadRequest(new { error = "token is required." });

            var summary = await service.GetPublicSummaryAsync(id, token, ct);
            if (summary is null)
                return Results.Unauthorized();

            return Results.Ok(summary);
        });
        // Note: no .RequireAuthorization — intentionally public, token-gated

        // POST /api/referrals/{id}/track-funnel
        // Body: { token, eventType }
        // Records a provider funnel event (ReferralViewed | ActivationStarted).
        group.MapPost("/{id:guid}/track-funnel", async (
            Guid id,
            [FromBody] TrackFunnelEventRequest request,
            IReferralService service,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Token))
                return Results.BadRequest(new { error = "token is required." });

            if (string.IsNullOrWhiteSpace(request.EventType))
                return Results.BadRequest(new { error = "eventType is required." });

            var ok = await service.TrackFunnelEventAsync(
                id, request.Token, request.EventType,
                request.RequesterName, request.RequesterEmail, ct);
            return ok ? Results.Ok() : Results.BadRequest(new { error = "Invalid token or unrecognised event type." });
        });
        // Note: no .RequireAuthorization — intentionally public, token-gated

        // POST /api/referrals/{id}/auto-provision
        // LSCC-010: Attempts instant provider activation.
        // On success:  provider is linked to an Identity org, returns loginUrl.
        // On fallback: upserts LSCC-009 activation request for admin review.
        // Public, token-gated (same HMAC-validated approach as track-funnel).
        group.MapPost("/{id:guid}/auto-provision", async (
            Guid id,
            [FromBody] AutoProvisionRequest request,
            IAutoProvisionService provisioner,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Token))
                return Results.BadRequest(new { error = "token is required." });

            var result = await provisioner.ProvisionAsync(
                id, request.Token, request.RequesterName, request.RequesterEmail, ct);

            return Results.Ok(result);
        });
        // Note: no .RequireAuthorization — intentionally public, token-gated

        // LSCC-01-002-01: Public token-only acceptance is permanently retired.
        // This endpoint no longer mutates referral state.
        // Providers must log in to accept referrals from the authenticated referral detail page.
        // Valid token links are still routed into the login + returnTo flow by /referrals/view.
        group.MapPost("/{id:guid}/accept-by-token", (Guid id) =>
            Results.Problem(
                detail: "Direct token-based acceptance is no longer supported. " +
                        "Please log in to the platform to view and accept this referral.",
                statusCode: StatusCodes.Status410Gone,
                title: "Acceptance path retired"));
        // Note: intentionally public — must remain accessible to serve legacy links safely
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
