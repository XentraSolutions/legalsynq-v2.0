using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using CareConnect.Application.Interfaces;

namespace CareConnect.Api.Endpoints;

public static class CategoryEndpoints
{
    public static void MapCategoryEndpoints(this WebApplication app)
    {
        app.MapGet("/api/categories", async (
            ICategoryService service,
            CancellationToken ct) =>
        {
            var categories = await service.GetAllAsync(ct);
            return Results.Ok(categories);
        })
        .RequireAuthorization(Policies.AuthenticatedUser)
        .RequireProductAccess(ProductCodes.SynqCareConnect);
    }
}
