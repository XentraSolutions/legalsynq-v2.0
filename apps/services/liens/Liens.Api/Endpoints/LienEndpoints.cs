using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using BuildingBlocks.Context;
using Liens.Domain;

namespace Liens.Api.Endpoints;

public static class LienEndpoints
{
    public static void MapLienEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/liens")
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(LiensPermissions.ProductCode);

        group.MapGet("/own", (ICurrentRequestContext ctx) =>
            Results.Ok(new { message = "Own liens listing (stub)", orgId = ctx.OrgId }))
            .RequirePermission(LiensPermissions.LienReadOwn);

        group.MapGet("/held", (ICurrentRequestContext ctx) =>
            Results.Ok(new { message = "Held liens listing (stub)", orgId = ctx.OrgId }))
            .RequirePermission(LiensPermissions.LienReadHeld);

        group.MapPost("/", (ICurrentRequestContext ctx) =>
            Results.Ok(new { message = "Lien created (stub)", orgId = ctx.OrgId }))
            .RequirePermission(LiensPermissions.LienCreate);

        group.MapPost("/{lienId:guid}/offers", (Guid lienId, ICurrentRequestContext ctx) =>
            Results.Ok(new { message = "Lien offered (stub)", lienId, orgId = ctx.OrgId }))
            .RequirePermission(LiensPermissions.LienOffer);

        group.MapGet("/marketplace", (ICurrentRequestContext ctx) =>
            Results.Ok(new { message = "Marketplace listing (stub)", orgId = ctx.OrgId }))
            .RequirePermission(LiensPermissions.LienBrowse);

        group.MapPost("/{lienId:guid}/purchase", (Guid lienId, ICurrentRequestContext ctx) =>
            Results.Ok(new { message = "Lien purchased (stub)", lienId, orgId = ctx.OrgId }))
            .RequirePermission(LiensPermissions.LienPurchase);

        group.MapPost("/{lienId:guid}/service", (Guid lienId, ICurrentRequestContext ctx) =>
            Results.Ok(new { message = "Lien serviced (stub)", lienId, orgId = ctx.OrgId }))
            .RequirePermission(LiensPermissions.LienService);

        group.MapPost("/{lienId:guid}/settle", (Guid lienId, ICurrentRequestContext ctx) =>
            Results.Ok(new { message = "Lien settled (stub)", lienId, orgId = ctx.OrgId }))
            .RequirePermission(LiensPermissions.LienSettle);
    }
}
