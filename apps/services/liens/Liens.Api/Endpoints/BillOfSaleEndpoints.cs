using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using BuildingBlocks.Context;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Domain;

namespace Liens.Api.Endpoints;

public static class BillOfSaleEndpoints
{
    public static void MapBillOfSaleEndpoints(this WebApplication app)
    {
        var bosGroup = app.MapGroup("/api/liens/bill-of-sales")
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(LiensPermissions.ProductCode);

        bosGroup.MapGet("/", SearchBillsOfSale)
            .RequirePermission(LiensPermissions.LienRead);

        bosGroup.MapGet("/{id:guid}", GetBillOfSaleById)
            .RequirePermission(LiensPermissions.LienRead);

        bosGroup.MapGet("/by-number/{billOfSaleNumber}", GetBillOfSaleByNumber)
            .RequirePermission(LiensPermissions.LienRead);

        var lienBosGroup = app.MapGroup("/api/liens/liens/{lienId:guid}/bill-of-sales")
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(LiensPermissions.ProductCode);

        lienBosGroup.MapGet("/", GetBillsOfSaleByLienId)
            .RequirePermission(LiensPermissions.LienRead);
    }

    private static Guid RequireTenantId(ICurrentRequestContext ctx)
    {
        return ctx.TenantId
            ?? throw new UnauthorizedAccessException("Tenant context is required.");
    }

    private static async Task<IResult> SearchBillsOfSale(
        IBillOfSaleService bosService,
        ICurrentRequestContext ctx,
        string? search = null,
        string? status = null,
        Guid? lienId = null,
        Guid? sellerOrgId = null,
        Guid? buyerOrgId = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var result = await bosService.SearchAsync(
            tenantId, lienId, status, buyerOrgId, sellerOrgId, search, page, pageSize, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetBillOfSaleById(
        Guid id,
        IBillOfSaleService bosService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var result = await bosService.GetByIdAsync(tenantId, id, ct);
        return result is null
            ? Results.NotFound(new { error = new { code = "not_found", message = $"BillOfSale '{id}' not found." } })
            : Results.Ok(result);
    }

    private static async Task<IResult> GetBillOfSaleByNumber(
        string billOfSaleNumber,
        IBillOfSaleService bosService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var result = await bosService.GetByBillOfSaleNumberAsync(tenantId, billOfSaleNumber, ct);
        return result is null
            ? Results.NotFound(new { error = new { code = "not_found", message = $"BillOfSale with number '{billOfSaleNumber}' not found." } })
            : Results.Ok(result);
    }

    private static async Task<IResult> GetBillsOfSaleByLienId(
        Guid lienId,
        IBillOfSaleService bosService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var result = await bosService.GetByLienIdAsync(tenantId, lienId, ct);
        return Results.Ok(result);
    }
}
