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
        // COMPATIBILITY-ONLY [TENANT-B08]: This endpoint is the Identity-sourced branding
        // bootstrap path. It is the active read source when TENANT_BRANDING_READ_SOURCE=Identity
        // (the safe default). When the platform migrates to Tenant-primary mode
        // (TENANT_BRANDING_READ_SOURCE=Tenant or HybridFallback), this endpoint becomes the
        // fallback path only. After sustained Tenant-primary production validation (≥30 days),
        // this endpoint can be retired and replaced by the Tenant service's
        // GET /tenant/api/v1/public/branding/by-code/{code}.
        //
        // WRITE-THROUGH [TENANT-B08]: LogoDocumentId and LogoWhiteDocumentId are still
        // read from the Identity Tenant entity. They are also written to the Tenant service
        // via dual-write from AdminEndpoints.SetTenantLogo/SetTenantLogoWhite. When branding
        // read source switches to Tenant, these fields will be read from TenantBranding instead.
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
                    LogoWhiteDocumentId: null,
                    PrimaryColor:   null,
                    FaviconUrl:     null));
            }

            return Results.Ok(new TenantBrandingResponse(
                TenantId:       tenant.Id.ToString(),
                TenantCode:     tenant.Code,
                DisplayName:    tenant.Name,
                LogoUrl:        null,
                LogoDocumentId: tenant.LogoDocumentId?.ToString(),
                LogoWhiteDocumentId: tenant.LogoWhiteDocumentId?.ToString(),
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
        if (httpContext.Request.Headers.TryGetValue("X-Tenant-Code", out var tenantCodeHeader))
        {
            var code = tenantCodeHeader.ToString().Trim();
            if (!string.IsNullOrEmpty(code))
            {
                var tenant = await tenantRepository.GetByCodeAsync(code, ct);
                if (tenant is not null) return tenant;

                var upper = code.ToUpperInvariant();
                if (upper != code)
                {
                    tenant = await tenantRepository.GetByCodeAsync(upper, ct);
                    if (tenant is not null) return tenant;
                }

                tenant = await tenantRepository.GetBySubdomainAsync(code, ct);
                if (tenant is not null) return tenant;
            }
        }

        // Priority 2: resolve from Host header via TenantDomains table (production)
        var host = httpContext.Request.Headers["X-Forwarded-Host"].FirstOrDefault()
            ?? httpContext.Request.Host.Host;

        if (!string.IsNullOrEmpty(host))
            return await tenantRepository.GetByHostAsync(host, ct);

        return null;
    }
}
