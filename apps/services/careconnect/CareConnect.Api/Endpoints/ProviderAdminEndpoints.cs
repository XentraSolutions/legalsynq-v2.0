// LSCC-002: Admin provider org-linkage backfill endpoint.
// LSCC-002-01: Extended with unlinked-list and bulk-link endpoints.
using BuildingBlocks.Authorization;
using BuildingBlocks.Context;
using CareConnect.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CareConnect.Api.Endpoints;

/// <summary>
/// Admin-only endpoints for CareConnect provider management.
///
/// PUT  /api/admin/providers/{id}/link-organization       — single provider org-link (LSCC-002)
/// GET  /api/admin/providers/unlinked                     — list providers with no org link (LSCC-002-01)
/// POST /api/admin/providers/bulk-link-organization       — batch org-link from explicit mapping (LSCC-002-01)
///
/// All operations are explicit, idempotent, and require PlatformOrTenantAdmin.
/// </summary>
// LSCC-002-01: Provider bulk backfill admin tooling
public static class ProviderAdminEndpoints
{
    public static IEndpointRouteBuilder MapProviderAdminEndpoints(
        this IEndpointRouteBuilder routes)
    {
        routes
            .MapPut("/api/admin/providers/{id:guid}/link-organization", LinkOrganizationAsync)
            .RequireAuthorization(Policies.PlatformOrTenantAdmin);

        // LSCC-002-01: List all active providers that have no Identity OrganizationId set.
        routes
            .MapGet("/api/admin/providers/unlinked", GetUnlinkedAsync)
            .RequireAuthorization(Policies.PlatformOrTenantAdmin);

        // LSCC-002-01: Bulk-link providers to organizations from an explicit admin-supplied mapping.
        routes
            .MapPost("/api/admin/providers/bulk-link-organization", BulkLinkOrganizationAsync)
            .RequireAuthorization(Policies.PlatformOrTenantAdmin);

        return routes;
    }

    // LSCC-002: Single provider org-link backfill — idempotent, explicit.
    private static async Task<IResult> LinkOrganizationAsync(
        Guid                 id,
        [FromBody] LinkOrganizationRequest request,
        IProviderService     service,
        ICurrentRequestContext ctx,
        CancellationToken    ct)
    {
        var tenantId = ctx.TenantId
            ?? throw new InvalidOperationException("tenant_id claim is missing.");

        var result = await service.LinkOrganizationAsync(tenantId, id, request.OrganizationId, ct);
        return Results.Ok(result);
    }

    // LSCC-002-01: Returns all active providers that have no OrganizationId.
    // Response: 200 { providers: [...], count: N }
    private static async Task<IResult> GetUnlinkedAsync(
        IProviderService       service,
        ICurrentRequestContext ctx,
        CancellationToken      ct)
    {
        var tenantId = ctx.TenantId
            ?? throw new InvalidOperationException("tenant_id claim is missing.");

        var providers = await service.GetUnlinkedProvidersAsync(tenantId, ct);
        return Results.Ok(new { providers, count = providers.Count });
    }

    // LSCC-002-01: Bulk org-link from explicit mapping.
    // Body: { items: [{ providerId, organizationId }, ...] }
    // Response: 200 { total, updated, skipped, unresolved }
    // Skipped   = provider already has an org ID (idempotent no-op per item).
    // Unresolved = provider ID not found in this tenant.
    private static async Task<IResult> BulkLinkOrganizationAsync(
        [FromBody] BulkLinkOrganizationRequest request,
        IProviderService       service,
        ICurrentRequestContext ctx,
        CancellationToken      ct)
    {
        var tenantId = ctx.TenantId
            ?? throw new InvalidOperationException("tenant_id claim is missing.");

        if (request.Items is null || request.Items.Count == 0)
            return Results.BadRequest("items must be a non-empty array.");

        var items = request.Items
            .Select(i => new ProviderOrgLinkItem(i.ProviderId, i.OrganizationId))
            .ToList();

        var report = await service.BulkLinkOrganizationAsync(tenantId, items, ct);
        return Results.Ok(report);
    }
}

/// <summary>Request body for PUT /api/admin/providers/{id}/link-organization.</summary>
public sealed record LinkOrganizationRequest(Guid OrganizationId);

/// <summary>LSCC-002-01: Request body for POST /api/admin/providers/bulk-link-organization.</summary>
public sealed record BulkLinkOrganizationRequest(List<BulkLinkItemDto> Items);

/// <summary>LSCC-002-01: Single item in a bulk-link request.</summary>
public sealed record BulkLinkItemDto(Guid ProviderId, Guid OrganizationId);
