using BuildingBlocks.Authorization;
using Tenant.Application.DTOs;
using Tenant.Application.Interfaces;

namespace Tenant.Api.Endpoints;

/// <summary>
/// BLK-TS-01 — Tenant Core Foundation endpoints.
///
/// GET  /api/v1/tenants/check-code  — validate and check uniqueness of a tenant code (anonymous)
/// POST /api/v1/tenants/provision   — minimal tenant creation from name + code (admin only)
/// </summary>
public static class ProvisionEndpoints
{
    public static void MapProvisionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/tenants");

        // ── GET /api/v1/tenants/check-code?code=acme ─────────────────────────
        //
        // Public: no auth required — reveals only availability, no sensitive data.
        // Normalizes input, validates format, checks uniqueness.
        //
        // 200 { available: true,  normalizedCode: "acme" }
        // 200 { available: false, normalizedCode: "acme", error: "..." }
        group.MapGet("/check-code", async (
            string?          code,
            ITenantService   svc,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(code))
                return Results.BadRequest(new
                {
                    error = new { code = "invalid_input", message = "The 'code' query parameter is required." }
                });

            var result = await svc.CheckCodeAsync(code, ct);
            return Results.Ok(result);
        })
        .AllowAnonymous();

        // ── POST /api/v1/tenants/provision ────────────────────────────────────
        //
        // Admin-only. Minimal provision: tenantName + tenantCode → canonical tenant record.
        // Subdomain defaults to the normalized code.
        // Does NOT create users, Identity memberships, DNS, or product entitlements.
        //
        // 201 { tenantId, tenantCode, subdomain }
        // 409 duplicate code / subdomain
        // 422 invalid code format
        group.MapPost("/provision", async (
            ProvisionRequest  request,
            ITenantService    svc,
            CancellationToken ct) =>
        {
            var result = await svc.ProvisionAsync(request, ct);
            return Results.Created($"/api/v1/tenants/{result.TenantId}", result);
        })
        .RequireAuthorization(Policies.AdminOnly);
    }
}
