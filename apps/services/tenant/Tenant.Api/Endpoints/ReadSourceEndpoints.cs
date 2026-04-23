using BuildingBlocks.Authorization;
using Microsoft.Extensions.Options;
using Tenant.Api.Configuration;

namespace Tenant.Api.Endpoints;

public static class ReadSourceEndpoints
{
    public static void MapReadSourceEndpoints(this IEndpointRouteBuilder app)
    {
        // ── GET /api/v1/admin/read-source ──────────────────────────────────────
        // Returns current read-source feature flag config for operators.
        // AdminOnly — does not expose sensitive data, but restricts to platform admins
        // to prevent leaking operational intent through public routes.
        app.MapGet("/api/v1/admin/read-source", (IOptions<TenantFeatures> opts) =>
        {
            var f = opts.Value;
            return Results.Ok(new
            {
                tenantReadSource            = f.TenantReadSource.ToString(),
                tenantBrandingReadSource    = f.TenantBrandingReadSource.ToString(),
                tenantResolutionReadSource  = f.TenantResolutionReadSource.ToString(),
                tenantDualWriteEnabled      = f.TenantDualWriteEnabled,
                effectiveBrandingSource     = f.TenantBrandingReadSource != TenantReadSource.Identity
                    ? f.TenantBrandingReadSource.ToString()
                    : f.TenantReadSource.ToString(),
                effectiveResolutionSource   = f.TenantResolutionReadSource != TenantReadSource.Identity
                    ? f.TenantResolutionReadSource.ToString()
                    : f.TenantReadSource.ToString(),
            });
        })
        .RequireAuthorization(Policies.AdminOnly);
    }
}
