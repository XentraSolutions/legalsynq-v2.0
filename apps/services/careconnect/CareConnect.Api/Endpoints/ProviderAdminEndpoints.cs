// LSCC-002: Admin provider org-linkage backfill endpoint.
using BuildingBlocks.Authorization;
using BuildingBlocks.Context;
using CareConnect.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CareConnect.Api.Endpoints;

/// <summary>
/// Admin-only endpoints for CareConnect provider management.
///
/// PUT /api/admin/providers/{id}/link-organization
///
/// Explicitly links a CareConnect provider record to an Identity Organization.
/// This is the safe backfill path for providers seeded or created before
/// org-linkage enforcement was active (i.e. those with OrganizationId = null).
///
/// The operation is idempotent: calling it again with the same organizationId
/// is a no-op from a domain perspective.
///
/// Authorization: PlatformOrTenantAdmin (no product-role capability required).
/// </summary>
// LSCC-002: Admin provider org-link backfill — PUT /api/admin/providers/{id}/link-organization
public static class ProviderAdminEndpoints
{
    public static IEndpointRouteBuilder MapProviderAdminEndpoints(
        this IEndpointRouteBuilder routes)
    {
        routes
            .MapPut("/api/admin/providers/{id:guid}/link-organization", LinkOrganizationAsync)
            .RequireAuthorization(Policies.PlatformOrTenantAdmin);

        return routes;
    }

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
}

/// <summary>Request body for PUT /api/admin/providers/{id}/link-organization.</summary>
public sealed record LinkOrganizationRequest(Guid OrganizationId);
