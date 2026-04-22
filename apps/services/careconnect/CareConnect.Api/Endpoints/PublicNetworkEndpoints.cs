using CareConnect.Application.DTOs;
using CareConnect.Application.Repositories;
using CareConnect.Domain;

namespace CareConnect.Api.Endpoints;

// CC2-INT-B07 — Public Network Surface.
// These endpoints are intentionally anonymous — no JWT or platform_session required.
// Tenant isolation is enforced via the X-Tenant-Id header, which is resolved
// server-side by the Next.js BFF from the request subdomain → Identity lookup.
// The caller (Next.js Server Component) NEVER reads this header from user input;
// it resolves the tenant from the subdomain and forwards only the GUID.
public static class PublicNetworkEndpoints
{
    public static void MapPublicNetworkEndpoints(this WebApplication app)
    {
        // All public routes share the /api/public/network prefix.
        // The Gateway is configured to route /careconnect/api/public/** anonymously.
        var group = app.MapGroup("/api/public/network");

        // ── GET /api/public/network ─────────────────────────────────────────
        // Lists all networks for the resolved tenant.
        // Header: X-Tenant-Id (GUID, resolved from subdomain by Next.js BFF)
        group.MapGet("/", async (
            HttpContext http,
            INetworkRepository repo,
            CancellationToken  ct) =>
        {
            var tenantId = ResolveTenantId(http);
            if (tenantId == null)
                return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

            var networks = await repo.GetAllByTenantAsync(tenantId.Value, ct);

            // For each network, get the provider count
            var summaries = new List<PublicNetworkSummary>(networks.Count);
            foreach (var n in networks)
            {
                var detail = await repo.GetWithProvidersAsync(tenantId.Value, n.Id, ct);
                summaries.Add(new PublicNetworkSummary(
                    n.Id,
                    n.Name,
                    n.Description,
                    detail?.NetworkProviders.Count ?? 0));
            }

            return Results.Ok(summaries);
        }).AllowAnonymous();

        // ── GET /api/public/network/{id}/providers ──────────────────────────
        // Lists public provider information for a specific network.
        // Applies stage-aware data: all providers returned regardless of stage,
        // but the stage field allows the frontend to decorate or redirect.
        group.MapGet("/{id:guid}/providers", async (
            Guid       id,
            HttpContext http,
            INetworkRepository repo,
            CancellationToken  ct) =>
        {
            var tenantId = ResolveTenantId(http);
            if (tenantId == null)
                return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

            // Verify the network belongs to this tenant
            var network = await repo.GetByIdAsync(tenantId.Value, id, ct);
            if (network == null)
                return Results.NotFound();

            var providers = await repo.GetNetworkProvidersAsync(tenantId.Value, id, ct);

            var items = providers
                .Select(p => new PublicProviderItem(
                    p.Id,
                    p.Name,
                    p.OrganizationName,
                    p.Phone,
                    p.City,
                    p.State,
                    p.PostalCode,
                    p.IsActive,
                    p.AcceptingReferrals,
                    p.AccessStage,
                    null))  // PrimaryCategory: placeholder for future specialty tag
                .ToList();

            return Results.Ok(items);
        }).AllowAnonymous();

        // ── GET /api/public/network/{id}/providers/markers ──────────────────
        // Returns geo-coded map markers for providers in a network.
        // Only providers with Latitude/Longitude are included.
        group.MapGet("/{id:guid}/providers/markers", async (
            Guid       id,
            HttpContext http,
            INetworkRepository repo,
            CancellationToken  ct) =>
        {
            var tenantId = ResolveTenantId(http);
            if (tenantId == null)
                return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

            var network = await repo.GetByIdAsync(tenantId.Value, id, ct);
            if (network == null)
                return Results.NotFound();

            var providers = await repo.GetNetworkProvidersAsync(tenantId.Value, id, ct);

            var markers = providers
                .Where(p => p.Latitude.HasValue && p.Longitude.HasValue)
                .Select(p => new PublicProviderMarker(
                    p.Id,
                    p.Name,
                    p.OrganizationName,
                    p.City,
                    p.State,
                    p.AcceptingReferrals,
                    p.Latitude!.Value,
                    p.Longitude!.Value))
                .ToList();

            return Results.Ok(markers);
        }).AllowAnonymous();

        // ── GET /api/public/network/{id}/detail ────────────────────────────
        // Combined endpoint: returns network info + providers + markers in a
        // single round-trip — optimal for the public landing page SSR.
        group.MapGet("/{id:guid}/detail", async (
            Guid       id,
            HttpContext http,
            INetworkRepository repo,
            CancellationToken  ct) =>
        {
            var tenantId = ResolveTenantId(http);
            if (tenantId == null)
                return Results.BadRequest(new { message = "X-Tenant-Id header is required." });

            var network = await repo.GetWithProvidersAsync(tenantId.Value, id, ct);
            if (network == null)
                return Results.NotFound();

            var providers = await repo.GetNetworkProvidersAsync(tenantId.Value, id, ct);

            var items = providers
                .Select(p => new PublicProviderItem(
                    p.Id,
                    p.Name,
                    p.OrganizationName,
                    p.Phone,
                    p.City,
                    p.State,
                    p.PostalCode,
                    p.IsActive,
                    p.AcceptingReferrals,
                    p.AccessStage,
                    null))
                .ToList();

            var markers = providers
                .Where(p => p.Latitude.HasValue && p.Longitude.HasValue)
                .Select(p => new PublicProviderMarker(
                    p.Id,
                    p.Name,
                    p.OrganizationName,
                    p.City,
                    p.State,
                    p.AcceptingReferrals,
                    p.Latitude!.Value,
                    p.Longitude!.Value))
                .ToList();

            var detail = new PublicNetworkDetail(
                network.Id,
                network.Name,
                network.Description,
                items,
                markers);

            return Results.Ok(detail);
        }).AllowAnonymous();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Reads X-Tenant-Id from the request headers.
    /// This is set server-side by the Next.js BFF after resolving the
    /// subdomain → tenant via the Identity service (anonymous branding endpoint).
    /// Returns null if the header is missing or malformed.
    /// </summary>
    private static Guid? ResolveTenantId(HttpContext http)
    {
        var raw = http.Request.Headers["X-Tenant-Id"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return Guid.TryParse(raw, out var id) ? id : null;
    }
}
