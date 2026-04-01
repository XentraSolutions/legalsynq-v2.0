using Identity.Application;
using Identity.Application.DTOs;
using Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Identity.Api.Endpoints;

public static class TenantBrandingEndpoints
{
    public static void MapTenantBrandingEndpoints(this WebApplication app)
    {
        // ── GET /api/tenants/current/branding ────────────────────────────────
        // Anonymous — must never require auth (the login page loads this before auth).
        //
        // Tenant resolution priority:
        //   1. X-Tenant-Code header  — sent by Next.js in dev (NEXT_PUBLIC_TENANT_CODE)
        //   2. Host header           — subdomain-based, production only
        //      e.g. "firm-a.legalsynq.com" → TenantDomains lookup
        //
        // Caching: safe to cache for 5–15 minutes at the CDN/gateway layer.
        // The branding data changes infrequently; stale data is a minor cosmetic issue.
        //
        // LogoDocumentId: when set, the authenticated portal proxies the logo via
        //   the Documents service (GET /documents/{id}/content).
        //   Anonymous contexts (login page) receive null — default LegalSynq branding applies.
        app.MapGet("/api/tenants/current/branding", async (
            HttpContext httpContext,
            ITenantRepository tenantRepository,
            CancellationToken ct) =>
        {
            var tenant = await ResolveTenantAsync(httpContext, tenantRepository, ct);

            if (tenant is null || !tenant.IsActive)
            {
                // Return a safe default rather than 404 — the login page must always render
                return Results.Ok(new TenantBrandingResponse(
                    TenantId:       string.Empty,
                    TenantCode:     string.Empty,
                    DisplayName:    "LegalSynq",
                    LogoUrl:        null,
                    LogoDocumentId: null,
                    PrimaryColor:   null,
                    FaviconUrl:     null));
            }

            return Results.Ok(new TenantBrandingResponse(
                TenantId:       tenant.Id.ToString(),
                TenantCode:     tenant.Code,
                DisplayName:    tenant.Name,
                LogoUrl:        null,
                LogoDocumentId: tenant.LogoDocumentId?.ToString(),
                PrimaryColor:   null,       // Phase 2: from TenantBranding table
                FaviconUrl:     null));     // Phase 2: from TenantBranding table
        })
        .AllowAnonymous();
    }

    private static async Task<Identity.Domain.Tenant?> ResolveTenantAsync(
        HttpContext httpContext,
        ITenantRepository tenantRepository,
        CancellationToken ct)
    {
        // Priority 1: explicit header (dev override or Next.js BFF-set header)
        if (httpContext.Request.Headers.TryGetValue("X-Tenant-Code", out var tenantCodeHeader))
        {
            var code = tenantCodeHeader.ToString().Trim().ToUpperInvariant();
            if (!string.IsNullOrEmpty(code))
                return await tenantRepository.GetByCodeAsync(code, ct);
        }

        // Priority 2: resolve from Host header via TenantDomains table (production)
        var host = httpContext.Request.Headers["X-Forwarded-Host"].FirstOrDefault()
            ?? httpContext.Request.Host.Host;

        if (!string.IsNullOrEmpty(host))
            return await tenantRepository.GetByHostAsync(host, ct);

        return null;
    }
}
