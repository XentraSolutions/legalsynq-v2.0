using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using BuildingBlocks.Context;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Domain;

namespace Liens.Api.Endpoints;

public static class SellingEndpoints
{
    public static void MapSellingEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/liens/selling")
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(LiensPermissions.ProductCode)
            .RequireSellMode();

        var portfolios = group.MapGroup("/portfolios");

        portfolios.MapGet("/", SearchPortfolios)
            .RequirePermission(LiensPermissions.LienSaleRead);

        portfolios.MapGet("/{id:guid}", GetPortfolioById)
            .RequirePermission(LiensPermissions.LienSaleRead);

        portfolios.MapPost("/", CreatePortfolio)
            .RequirePermission(LiensPermissions.LienSaleCreate);

        portfolios.MapPut("/{id:guid}", UpdatePortfolio)
            .RequirePermission(LiensPermissions.LienSaleUpdate);

        portfolios.MapPost("/{id:guid}/liens", AddLiens)
            .RequirePermission(LiensPermissions.LienSaleUpdate);

        portfolios.MapPost("/{id:guid}/liens/remove", RemoveLiens)
            .RequirePermission(LiensPermissions.LienSaleUpdate);

        portfolios.MapPost("/{id:guid}/buyers", AddBuyers)
            .RequirePermission(LiensPermissions.LienSaleUpdate);

        portfolios.MapPost("/{id:guid}/liens/{lienIdOrCode}/buyer-email", SendBuyerEmail)
            .RequirePermission(LiensPermissions.LienOffer);

        portfolios.MapPost("/{id:guid}/status", TransitionStatus)
            .RequirePermission(LiensPermissions.LienSaleUpdate);

        portfolios.MapPost("/{id:guid}/publish", Publish)
            .RequirePermission(LiensPermissions.LienSalePublish);

        portfolios.MapPost("/{id:guid}/withdraw", Withdraw)
            .RequirePermission(LiensPermissions.LienSaleWithdraw);

        portfolios.MapGet("/{id:guid}/status-history", GetStatusHistory)
            .RequirePermission(LiensPermissions.LienSaleRead);

        portfolios.MapGet("/{id:guid}/activity", GetActivity)
            .RequirePermission(LiensPermissions.LienSaleRead);

        portfolios.MapGet("/{id:guid}/analytics", GetAnalytics)
            .RequirePermission(LiensPermissions.LienSaleViewAnalytics);
    }

    private static Guid RequireTenantId(ICurrentRequestContext ctx)
    {
        return ctx.TenantId
            ?? throw new UnauthorizedAccessException("Tenant context is required.");
    }

    private static Guid RequireUserId(ICurrentRequestContext ctx)
    {
        return ctx.UserId
            ?? throw new UnauthorizedAccessException("User context is required.");
    }

    private static Guid RequireOrgId(ICurrentRequestContext ctx)
    {
        return ctx.OrgId
            ?? throw new UnauthorizedAccessException("Organization context is required.");
    }

    private static async Task<IResult> SearchPortfolios(
        ISellingPortfolioService service,
        ICurrentRequestContext ctx,
        string? search = null,
        string? status = null,
        Guid? buyerOrgId = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var sellerOrgId = RequireOrgId(ctx);
        var result = await service.SearchAsync(
            tenantId, sellerOrgId, search, status, buyerOrgId, page, pageSize, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetPortfolioById(
        Guid id,
        ISellingPortfolioService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var sellerOrgId = RequireOrgId(ctx);
        var result = await service.GetByIdAsync(tenantId, id, sellerOrgId, ct);
        return result is null
            ? Results.NotFound(new { error = new { code = "not_found", message = $"Selling portfolio '{id}' not found." } })
            : Results.Ok(result);
    }

    private static async Task<IResult> CreatePortfolio(
        CreateSellingPortfolioRequest request,
        ISellingPortfolioService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var sellerOrgId = RequireOrgId(ctx);
        var userId = RequireUserId(ctx);
        var result = await service.CreateAsync(tenantId, sellerOrgId, userId, request, ct);
        return Results.Created($"/api/liens/selling/portfolios/{result.Id}", result);
    }

    private static async Task<IResult> UpdatePortfolio(
        Guid id,
        UpdateSellingPortfolioRequest request,
        ISellingPortfolioService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var sellerOrgId = RequireOrgId(ctx);
        var userId = RequireUserId(ctx);
        var result = await service.UpdateAsync(tenantId, id, sellerOrgId, userId, request, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> AddLiens(
        Guid id,
        AddSellingPortfolioLiensRequest request,
        ISellingPortfolioService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var sellerOrgId = RequireOrgId(ctx);
        var userId = RequireUserId(ctx);
        var result = await service.AddLiensAsync(tenantId, id, sellerOrgId, userId, request, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> RemoveLiens(
        Guid id,
        RemoveSellingPortfolioLiensRequest request,
        ISellingPortfolioService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var sellerOrgId = RequireOrgId(ctx);
        var userId = RequireUserId(ctx);
        var result = await service.RemoveLiensAsync(tenantId, id, sellerOrgId, userId, request, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> AddBuyers(
        Guid id,
        AddSellingPortfolioBuyersRequest request,
        ISellingPortfolioService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var sellerOrgId = RequireOrgId(ctx);
        var userId = RequireUserId(ctx);
        var result = await service.AddBuyersAsync(tenantId, id, sellerOrgId, userId, request, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> SendBuyerEmail(
        Guid id,
        string lienIdOrCode,
        SendLienBuyerEmailRequest request,
        ISellingPortfolioService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var sellerOrgId = RequireOrgId(ctx);
        var userId = RequireUserId(ctx);
        var result = await service.SendBuyerEmailAsync(tenantId, id, lienIdOrCode, sellerOrgId, userId, request, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> TransitionStatus(
        Guid id,
        TransitionSellingPortfolioStatusRequest request,
        ISellingPortfolioService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var sellerOrgId = RequireOrgId(ctx);
        var userId = RequireUserId(ctx);
        var result = await service.TransitionStatusAsync(tenantId, id, sellerOrgId, userId, request, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> Publish(
        Guid id,
        TransitionSellingPortfolioStatusRequest? request,
        ISellingPortfolioService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var sellerOrgId = RequireOrgId(ctx);
        var userId = RequireUserId(ctx);
        var result = await service.PublishAsync(tenantId, id, sellerOrgId, userId, request?.Notes, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> Withdraw(
        Guid id,
        TransitionSellingPortfolioStatusRequest? request,
        ISellingPortfolioService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var sellerOrgId = RequireOrgId(ctx);
        var userId = RequireUserId(ctx);
        var result = await service.WithdrawAsync(tenantId, id, sellerOrgId, userId, request?.Notes, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetStatusHistory(
        Guid id,
        ISellingPortfolioService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var sellerOrgId = RequireOrgId(ctx);
        var result = await service.GetStatusHistoryAsync(tenantId, id, sellerOrgId, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetActivity(
        Guid id,
        ISellingPortfolioService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var sellerOrgId = RequireOrgId(ctx);
        var result = await service.GetActivityAsync(tenantId, id, sellerOrgId, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetAnalytics(
        Guid id,
        ISellingPortfolioService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var sellerOrgId = RequireOrgId(ctx);
        var result = await service.GetAnalyticsAsync(tenantId, id, sellerOrgId, ct);
        return Results.Ok(result);
    }
}
