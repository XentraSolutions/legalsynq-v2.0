using Tenant.Application.Interfaces;

namespace Tenant.Api.Endpoints;

public static class EligibleTenantEndpoints
{
    public static void MapEligibleTenantEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/internal/tenants/eligible/synqliens", async (
            IEligibleTenantService service,
            CancellationToken ct) =>
        {
            var tenantIds = await service.ListActiveSynqLiensTenantIdsAsync(ct);
            return Results.Ok(new
            {
                productCode = "SYNQ_LIENS",
                totalCount = tenantIds.Count,
                tenantIds,
            });
        })
        .RequireAuthorization("InternalService");
    }
}
