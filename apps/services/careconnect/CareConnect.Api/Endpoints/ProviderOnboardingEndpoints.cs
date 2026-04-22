// CC2-INT-B09: Provider tenant self-onboarding endpoints.
// Authenticated — requires a valid JWT (COMMON_PORTAL provider).
// Gateway route: /careconnect/api/provider/onboarding/* → RequireAuthorization (protected)
using BuildingBlocks.Context;
using CareConnect.Application.DTOs;
using CareConnect.Application.Interfaces;
using CareConnect.Application.Repositories;
using CareConnect.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Builder;

namespace CareConnect.Api.Endpoints;

public static class ProviderOnboardingEndpoints
{
    public static IEndpointRouteBuilder MapProviderOnboardingEndpoints(
        this IEndpointRouteBuilder app)
    {
        // ── GET /api/provider/onboarding/status ──────────────────────────────
        // Returns the provider's current onboarding stage so the frontend can
        // decide whether to show the "Set up your workspace" CTA.
        app.MapGet("/api/provider/onboarding/status", async (
            ICurrentRequestContext     ctx,
            IProviderRepository        providerRepo,
            CancellationToken          ct) =>
        {
            var identityUserId = ctx.UserId;
            if (identityUserId is null)
                return Results.Unauthorized();

            var provider = await providerRepo.GetByIdentityUserIdAsync(identityUserId.Value, ct);
            if (provider is null)
                return Results.NotFound(new { message = "No provider record linked to this account." });

            return Results.Ok(new
            {
                providerId   = provider.Id,
                accessStage  = provider.AccessStage,
                canOnboard   = provider.AccessStage == ProviderAccessStage.CommonPortal,
            });
        }).RequireAuthorization();

        // ── GET /api/provider/onboarding/check-code ───────────────────────────
        // Checks whether a tenant code is available for self-provisioning.
        // Called live from the onboarding form as the user types.
        app.MapGet("/api/provider/onboarding/check-code", async (
            string                      code,
            IProviderOnboardingService  onboardingSvc,
            CancellationToken           ct) =>
        {
            if (string.IsNullOrWhiteSpace(code))
                return Results.BadRequest(new { message = "code query parameter is required." });

            var result = await onboardingSvc.CheckCodeAvailableAsync(code.Trim(), ct);

            if (result is null)
            {
                // Identity service unreachable — return an optimistic response so the user can proceed.
                // The provision step enforces uniqueness and will still fail if the code is taken.
                return Results.Ok(new TenantCodeAvailabilityResponse
                {
                    Available      = true,
                    NormalizedCode = code.Trim().ToLowerInvariant(),
                    Message        = "Availability could not be confirmed — the code will be validated on submission.",
                });
            }

            return Results.Ok(new TenantCodeAvailabilityResponse
            {
                Available      = result.Available,
                NormalizedCode = result.NormalizedCode,
                Message        = result.Message,
            });
        });

        // ── POST /api/provider/onboarding/provision-tenant ────────────────────
        // Creates a new tenant workspace for the authenticated COMMON_PORTAL provider.
        // Transitions provider AccessStage: COMMON_PORTAL → TENANT.
        app.MapPost("/api/provider/onboarding/provision-tenant", async (
            ProviderOnboardingRequest  req,
            ICurrentRequestContext     ctx,
            IProviderOnboardingService onboardingSvc,
            CancellationToken          ct) =>
        {
            // Guard: authenticated user is required.
            var identityUserId = ctx.UserId;
            if (identityUserId is null)
                return Results.Unauthorized();

            // Input validation.
            if (string.IsNullOrWhiteSpace(req.TenantName) || req.TenantName.Trim().Length < 2)
                return Results.UnprocessableEntity(new
                {
                    message = "Validation failed.",
                    errors  = new Dictionary<string, string>
                    {
                        ["tenantName"] = "Organization name must be at least 2 characters.",
                    },
                });

            if (string.IsNullOrWhiteSpace(req.TenantCode) || req.TenantCode.Trim().Length < 2)
                return Results.UnprocessableEntity(new
                {
                    message = "Validation failed.",
                    errors  = new Dictionary<string, string>
                    {
                        ["tenantCode"] = "Subdomain code must be at least 2 characters.",
                    },
                });

            try
            {
                var result = await onboardingSvc.ProvisionToTenantAsync(
                    identityUserId.Value,
                    req.TenantName.Trim(),
                    req.TenantCode.Trim(),
                    ct);

                return Results.Created(
                    $"/api/provider/onboarding/provision-tenant",
                    new ProviderOnboardingResponse
                    {
                        ProviderId         = result.ProviderId,
                        TenantId           = result.TenantId,
                        TenantCode         = result.TenantCode,
                        Subdomain          = result.Subdomain,
                        ProvisioningStatus = result.ProvisioningStatus,
                        PortalUrl          = result.PortalUrl,
                        Message            = "Your workspace is being set up. DNS provisioning may take a few minutes.",
                    });
            }
            catch (ProviderOnboardingException ex)
            {
                return ex.Code switch
                {
                    ProviderOnboardingErrorCode.ProviderNotFound     => Results.NotFound(new { message = ex.Message }),
                    ProviderOnboardingErrorCode.WrongAccessStage     => Results.UnprocessableEntity(new { message = ex.Message }),
                    ProviderOnboardingErrorCode.TenantCodeUnavailable => Results.Conflict(new { message = ex.Message }),
                    ProviderOnboardingErrorCode.IdentityServiceFailed => Results.Problem(ex.Message, statusCode: 503),
                    _                                                 => Results.Problem(ex.Message),
                };
            }
        }).RequireAuthorization(); // JWT required — enforced by gateway + ASP.NET Core auth pipeline

        return app;
    }
}
