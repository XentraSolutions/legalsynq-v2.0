using BuildingBlocks.Authentication.ServiceTokens;
using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using BuildingBlocks.Context;
using BuildingBlocks.Exceptions;
using BuildingBlocks.Notifications;
using HtmlAgilityPack;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Domain;
using Liens.Domain.Entities;
using Liens.Domain.Enums;
using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Liens.Api.Endpoints;

public static class SellingEndpoints
{
    private const long SellingImportMaxBytes = 50L * 1024 * 1024;
    private const string SellingPatientDetailsTemplate = "SELLING_PATIENT_DETAILS_REPORT";
    private const string DocumentsServiceAudience = "documents-service";
    private static readonly HashSet<string> AllowedSellingImportExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".xls",
        ".xlsx",
    };

    private static readonly HashSet<string> AllowedSellingBulkImportExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".csv",
        ".xls",
        ".xlsx",
    };
    private const string SellingBulkImportTemplateType = "SellingLienImport";
    private static readonly string[] SellingBulkImportTemplateColumns =
    [
        "Case Code*",
        "Lien Status*",
        "Purchase Date*",
        "Initial Service Date*",
        "End Service Date",
        "Notes",
        "Funding Company",
        "Facility Name*",
        "Contact Person",
        "Facility Email Address",
        "Medical Provider Name",
        "Medical Code & Description*",
        "Medicare Cost",
        "Billing Amount*",
        "Purchase Amount*",
        "Payee",
        "Outbound Check Number",
        "Document Type*",
        "Attachment",
    ];
    private static readonly string[] SellingBulkImportTemplateExample =
    [
        "CASE-10001",
        "Open",
        "01/15/2026",
        "01/10/2026",
        "01/12/2026",
        "Example selling lien import",
        "Example Funding Co.",
        "Example Medical Center",
        "Jamie Smith",
        "billing@example-medical-center.test",
        "Example Medical Center",
        "99213 - Office visit",
        "82.00",
        "250.00",
        "175.00",
        "Example Medical Center",
        "CHK-10001",
        "Medical bill",
        "",
    ];

    private static readonly string[] SellingDocumentTaskTypes =
    [
        "LegacyCaseDocument",
        "LegacyLienDocument",
        "LegacyMedicalDocument",
        "SellingDocumentReference",
    ];

    public static void MapSellingEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/liens/selling")
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(LiensPermissions.ProductCode)
            .RequireSellMode();

        group.MapPost("/imports/patient-details", ImportPatientDetailsReport)
            .RequirePermission(LiensPermissions.LienSaleCreate)
            .WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(SellingImportMaxBytes));

        group.MapGet("/bulk-import-template", DownloadBulkImportTemplate)
            .RequirePermission(LiensPermissions.LienSaleRead);

        group.MapPost("/bulk-imports", CreateBulkImport)
            .RequirePermission(LiensPermissions.LienSaleCreate)
            .WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(SellingImportMaxBytes))
            .DisableAntiforgery();

        group.MapGet("/dashboard", GetDashboard)
            .RequirePermission(LiensPermissions.LienSaleRead);

        group.MapGet("/liens", GetLiens)
            .RequirePermission(LiensPermissions.LienSaleRead);

        var buyerGroup = app.MapGroup("/api/liens/selling/buyer")
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(LiensPermissions.ProductCode);

        buyerGroup.MapGet("/dashboard", GetBuyerDashboard)
            .RequirePermission(LiensPermissions.LienBrowse, ProductRoleCodes.SynqLienBuyer);

        buyerGroup.MapGet("/liens", GetBuyerOfferedLiens)
            .RequirePermission(LiensPermissions.LienBrowse, ProductRoleCodes.SynqLienBuyer);

        buyerGroup.MapGet("/liens/{accessLinkId:guid}", GetBuyerOfferedLien)
            .RequirePermission(LiensPermissions.LienBrowse, ProductRoleCodes.SynqLienBuyer);

        buyerGroup.MapGet("/liens/{accessLinkId:guid}/documents/{documentId:guid}/view", ViewBuyerOfferedLienDocument)
            .RequirePermission(LiensPermissions.LienBrowse, ProductRoleCodes.SynqLienBuyer);

        buyerGroup.MapGet("/liens/{accessLinkId:guid}/documents/{documentId:guid}/download", DownloadBuyerOfferedLienDocument)
            .RequirePermission(LiensPermissions.LienBrowse, ProductRoleCodes.SynqLienBuyer);

        buyerGroup.MapPost("/liens/{accessLinkId:guid}/messages", PostBuyerOfferedLienMessage)
            .RequirePermission(LiensPermissions.LienBrowse, ProductRoleCodes.SynqLienBuyer);

        buyerGroup.MapPost("/liens/{accessLinkId:guid}/accept", AcceptBuyerOfferedLien)
            .RequirePermission(LiensPermissions.LienBrowse, ProductRoleCodes.SynqLienBuyer);

        buyerGroup.MapPost("/liens/{accessLinkId:guid}/decline", DeclineBuyerOfferedLien)
            .RequirePermission(LiensPermissions.LienBrowse, ProductRoleCodes.SynqLienBuyer);

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

    private static async Task<IResult> GetDashboard(
        ISellingDashboardService service,
        ICurrentRequestContext ctx,
        string? tab = null,
        string? search = null,
        Guid? fundingCompanyId = null,
        Guid? lawFirmId = null,
        Guid? caseManagerId = null,
        Guid? facilityId = null,
        DateOnly? initialServiceDateFrom = null,
        DateOnly? initialServiceDateTo = null,
        string? sortBy = null,
        string? sortDirection = null,
        int page = 1,
        int pageSize = 25,
        CancellationToken ct = default)
    {
        var result = await service.GetAsync(
            RequireTenantId(ctx),
            RequireOrgId(ctx),
            new SellingDashboardQuery
            {
                Tab = tab ?? "pending",
                Search = search,
                FundingCompanyId = fundingCompanyId,
                LawFirmId = lawFirmId,
                CaseManagerId = caseManagerId,
                FacilityId = facilityId,
                InitialServiceDateFrom = initialServiceDateFrom,
                InitialServiceDateTo = initialServiceDateTo,
                SortBy = sortBy ?? "initialServiceDate",
                SortDirection = sortDirection ?? "desc",
                Page = page,
                PageSize = pageSize,
            },
            ct);

        return Results.Ok(result);
    }

    private static async Task<IResult> GetLiens(
        ISellingDashboardService service,
        ICurrentRequestContext ctx,
        string? tab = null,
        string? search = null,
        Guid? fundingCompanyId = null,
        Guid? lawFirmId = null,
        Guid? caseManagerId = null,
        Guid? facilityId = null,
        DateOnly? initialServiceDateFrom = null,
        DateOnly? initialServiceDateTo = null,
        string? sortBy = null,
        string? sortDirection = null,
        int page = 1,
        int pageSize = 25,
        CancellationToken ct = default)
    {
        var result = await service.GetAsync(
            RequireTenantId(ctx),
            RequireOrgId(ctx),
            new SellingDashboardQuery
            {
                Tab = tab ?? "pending",
                Search = search,
                FundingCompanyId = fundingCompanyId,
                LawFirmId = lawFirmId,
                CaseManagerId = caseManagerId,
                FacilityId = facilityId,
                InitialServiceDateFrom = initialServiceDateFrom,
                InitialServiceDateTo = initialServiceDateTo,
                SortBy = sortBy ?? "initialServiceDate",
                SortDirection = sortDirection ?? "desc",
                Page = page,
                PageSize = pageSize,
            },
            ct);

        return Results.Ok(new SellingLienListResponse
        {
            Items = result.Items,
            Page = result.Page,
            PageSize = result.PageSize,
            TotalCount = result.TotalCount,
        });
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

    private static async Task<IResult> ConfirmSale(
        Guid lienId,
        ConfirmSellingLienSaleRequest request,
        ISellingPortfolioService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var sellerOrgId = RequireOrgId(ctx);
        var userId = RequireUserId(ctx);
        var result = await service.ConfirmSaleAsync(
            tenantId,
            lienId,
            sellerOrgId,
            userId,
            request,
            ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetBuyerOfferedLiens(
        LiensDbContext db,
        ICurrentRequestContext ctx,
        ISellerOrganizationDisplayResolver sellerDisplayResolver,
        string? status = null,
        string? search = null,
        string? sort = null,
        string? direction = null,
        int page = 1,
        int pageSize = 10,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var sources = await LoadBuyerOfferedLienSourcesAsync(db, ctx, ct);
        var sellerDisplays = await ResolveSellerDisplaysAsync(db, sellerDisplayResolver, tenantId, sources, ct);
        var providerNames = await ResolveProviderNamesAsync(db, tenantId, sources, ct);

        IEnumerable<BuyerOfferedLienRow> rows = sources
            .Select(source => MapBuyerOfferedLienRow(source, sellerDisplays, providerNames));

        var statusFilter = NormalizeBuyerOfferedLienStatusFilter(status);
        if (!string.IsNullOrWhiteSpace(statusFilter))
        {
            rows = rows.Where(row => string.Equals(row.Status, statusFilter, StringComparison.OrdinalIgnoreCase));
        }

        var searchTerm = search?.Trim();
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            rows = rows.Where(row => BuyerOfferedLienMatchesSearch(row, searchTerm));
        }

        var sortedRows = SortBuyerOfferedLiens(rows, sort, direction).ToList();
        var total = sortedRows.Count;
        var pagedRows = sortedRows
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Results.Ok(new BuyerOfferedLiensResult(pagedRows, page, pageSize, total));
    }

    private static async Task<IResult> GetBuyerOfferedLien(
        Guid accessLinkId,
        LiensDbContext db,
        ICurrentRequestContext ctx,
        ISellerOrganizationDisplayResolver sellerDisplayResolver,
        CancellationToken ct = default)
    {
        var (accessLink, error) = await ResolveBuyerOfferedLienAccessLinkAsync(accessLinkId, db, ctx, ct);
        if (error is not null)
            return error;

        var tenantId = accessLink!.TenantId;

        var lien = await db.Liens
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == accessLink.LienId, ct);

        if (lien is null)
            return Results.NotFound(new { error = new { code = "not_found", message = $"Offered lien '{accessLinkId}' not found." } });

        var buyerContact = await db.Contacts
            .AsNoTracking()
            .FirstOrDefaultAsync(contact =>
                contact.TenantId == tenantId &&
                contact.Id == accessLink.BuyerContactId &&
                contact.OrgId == accessLink.BuyerOrgId,
                ct);

        var sellerContacts = await db.Contacts
            .AsNoTracking()
            .Where(contact => contact.TenantId == tenantId && contact.OrgId == accessLink.SellerOrgId && contact.IsActive)
            .ToListAsync(ct);

        var sellerDisplay = await sellerDisplayResolver.ResolveAsync(
            tenantId,
            accessLink.SellerOrgId,
            sellerContacts,
            sellerUserId: accessLink.CreatedByUserId,
            fallbackEmail: null,
            ct: ct);
        var providerName = await ResolveProviderNameAsync(db, tenantId, lien.FacilityId, ct);
        var documents = await ResolveBuyerOfferedLienDocumentsAsync(db, tenantId, accessLink.Id, lien, ct);
        var messages = await ResolveBuyerOfferedLienMessagesAsync(db, accessLink, ct);

        return Results.Ok(MapBuyerOfferedLienDetail(
            accessLink,
            lien,
            sellerDisplay,
            buyerContact,
            providerName,
            documents,
            messages));
    }

    private static Task<IResult> ViewBuyerOfferedLienDocument(
        Guid accessLinkId,
        Guid documentId,
        LiensDbContext db,
        ICurrentRequestContext ctx,
        IHttpClientFactory httpClientFactory,
        IServiceTokenIssuer serviceTokenIssuer,
        ILoggerFactory loggerFactory,
        CancellationToken ct = default)
        => RedirectBuyerOfferedLienDocument(
            accessLinkId,
            documentId,
            "view",
            db,
            ctx,
            httpClientFactory,
            serviceTokenIssuer,
            loggerFactory,
            ct);

    private static Task<IResult> DownloadBuyerOfferedLienDocument(
        Guid accessLinkId,
        Guid documentId,
        LiensDbContext db,
        ICurrentRequestContext ctx,
        IHttpClientFactory httpClientFactory,
        IServiceTokenIssuer serviceTokenIssuer,
        ILoggerFactory loggerFactory,
        CancellationToken ct = default)
        => RedirectBuyerOfferedLienDocument(
            accessLinkId,
            documentId,
            "download",
            db,
            ctx,
            httpClientFactory,
            serviceTokenIssuer,
            loggerFactory,
            ct);

    private static async Task<IResult> RedirectBuyerOfferedLienDocument(
        Guid accessLinkId,
        Guid documentId,
        string accessType,
        LiensDbContext db,
        ICurrentRequestContext ctx,
        IHttpClientFactory httpClientFactory,
        IServiceTokenIssuer serviceTokenIssuer,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        if (documentId == Guid.Empty)
        {
            return Results.BadRequest(new
            {
                error = new
                {
                    code = "document_required",
                    message = "A valid document id is required.",
                },
            });
        }

        var (accessLink, error) = await ResolveBuyerOfferedLienAccessLinkAsync(accessLinkId, db, ctx, ct);
        if (error is not null)
            return error;

        var documentReference = await ResolveBuyerOfferedLienDocumentReferenceAsync(
            db,
            accessLink!.TenantId,
            accessLink.LienId,
            documentId,
            ct);
        if (documentReference is null)
        {
            return Results.NotFound(new
            {
                error = new
                {
                    code = "document_not_found",
                    message = "This document is not attached to the offered lien.",
                },
            });
        }

        var redeemUrl = await IssueBuyerOfferedLienDocumentAccessUrlAsync(
            httpClientFactory,
            serviceTokenIssuer,
            loggerFactory,
            accessLink,
            documentReference.Value,
            accessType,
            RequireUserId(ctx),
            ct);
        if (string.IsNullOrWhiteSpace(redeemUrl))
        {
            return Results.Problem(
                title: "Document unavailable",
                detail: "The document could not be opened right now.",
                statusCode: StatusCodes.Status502BadGateway);
        }

        return Results.Redirect(redeemUrl, permanent: false, preserveMethod: false);
    }

    private static async Task<IResult> PostBuyerOfferedLienMessage(
        Guid accessLinkId,
        SellingPublicEndpoints.PublicPortalMessageRequest? request,
        HttpContext httpContext,
        INotificationPublisher notifications,
        ISellingBuyerAccessLinkService accessLinks,
        ILoggerFactory loggerFactory,
        IConfiguration configuration,
        LiensDbContext db,
        ICurrentRequestContext ctx,
        ISellerOrganizationDisplayResolver sellerDisplayResolver,
        CancellationToken ct = default)
    {
        var (accessLink, error) = await ResolveBuyerOfferedLienAccessLinkAsync(accessLinkId, db, ctx, ct);
        if (error is not null)
            return error;

        return await SellingPublicEndpoints.PostResolvedBuyerPortalMessage(
            accessLink!,
            null,
            request,
            httpContext,
            notifications,
            accessLinks,
            loggerFactory,
            configuration,
            sellerDisplayResolver,
            db,
            ct,
            currentBuyerAccountEmail: ctx.Email);
    }

    private static async Task<IResult> AcceptBuyerOfferedLien(
        Guid accessLinkId,
        SellingPublicEndpoints.PublicBuyerAcceptLienRequest? request,
        HttpContext httpContext,
        INotificationPublisher notifications,
        ILoggerFactory loggerFactory,
        LiensDbContext db,
        ICurrentRequestContext ctx,
        ISellerOrganizationDisplayResolver sellerDisplayResolver,
        CancellationToken ct = default)
    {
        var (accessLink, error) = await ResolveBuyerOfferedLienAccessLinkAsync(accessLinkId, db, ctx, ct);
        if (error is not null)
            return error;

        return await SellingPublicEndpoints.AcceptResolvedBuyerPortal(
            accessLink!,
            request,
            httpContext,
            notifications,
            loggerFactory,
            sellerDisplayResolver,
            db,
            ct,
            currentBuyerAccountEmail: ctx.Email);
    }

    private static async Task<IResult> DeclineBuyerOfferedLien(
        Guid accessLinkId,
        SellingPublicEndpoints.PublicBuyerDeclineLienRequest? request,
        HttpContext httpContext,
        INotificationPublisher notifications,
        ILoggerFactory loggerFactory,
        LiensDbContext db,
        ICurrentRequestContext ctx,
        ISellerOrganizationDisplayResolver sellerDisplayResolver,
        CancellationToken ct = default)
    {
        var (accessLink, error) = await ResolveBuyerOfferedLienAccessLinkAsync(accessLinkId, db, ctx, ct);
        if (error is not null)
            return error;

        return await SellingPublicEndpoints.DeclineResolvedBuyerPortal(
            accessLink!,
            request,
            httpContext,
            notifications,
            loggerFactory,
            sellerDisplayResolver,
            db,
            ct,
            currentBuyerAccountEmail: ctx.Email);
    }

    private static async Task<(SellingBuyerAccessLink? AccessLink, IResult? Error)> ResolveBuyerOfferedLienAccessLinkAsync(
        Guid accessLinkId,
        LiensDbContext db,
        ICurrentRequestContext ctx,
        CancellationToken ct)
    {
        var tenantId = RequireTenantId(ctx);
        var buyerContactIds = await ResolveBuyerContactIdsAsync(db, tenantId, ctx.Email, ct);

        var accessLink = await db.SellingBuyerAccessLinks
            .FirstOrDefaultAsync(link =>
                link.TenantId == tenantId &&
                link.Id == accessLinkId &&
                link.Purpose == SellingAccessLinkPurposes.ConfirmSaleBuyerResponse &&
                link.RevokedAtUtc == null &&
                buyerContactIds.Contains(link.BuyerContactId),
                ct);

        if (accessLink is null)
        {
            return (null, Results.NotFound(new
            {
                error = new
                {
                    code = "not_found",
                    message = $"Offered lien '{accessLinkId}' not found.",
                },
            }));
        }

        return (accessLink, null);
    }

    private static async Task<List<BuyerOfferedLienSource>> LoadBuyerOfferedLienSourcesAsync(
        LiensDbContext db,
        ICurrentRequestContext ctx,
        CancellationToken ct)
    {
        var tenantId = RequireTenantId(ctx);
        var buyerContactIds = await ResolveBuyerContactIdsAsync(db, tenantId, ctx.Email, ct);
        var linkQuery = db.SellingBuyerAccessLinks
            .AsNoTracking()
            .Where(link =>
                link.TenantId == tenantId &&
                link.Purpose == SellingAccessLinkPurposes.ConfirmSaleBuyerResponse &&
                link.RevokedAtUtc == null);

        var links = await LoadBuyerAccessLinksAsync(linkQuery, buyerContactIds, ct);
        var lienIds = links.Select(link => link.LienId).Distinct().ToArray();
        var liens = await LoadLiensByIdAsync(db, tenantId, lienIds, ct);

        return links
            .Where(link => liens.ContainsKey(link.LienId))
            .Select(link =>
            {
                var lien = liens[link.LienId];
                return new BuyerOfferedLienSource(
                    link.Id,
                    lien.Id,
                    link.SellerOrgId,
                    link.CreatedByUserId,
                    lien.FacilityId,
                    lien.LienNumber,
                    lien.Status,
                    lien.SellerStatus,
                    lien.ExternalReference,
                    lien.SubjectFirstName,
                    lien.SubjectLastName,
                    lien.InitialServiceDate,
                    lien.OriginalAmount,
                    lien.OfferPrice,
                    lien.AskAmount,
                    lien.HighestBidAmount,
                    link.CreatedAtUtc,
                    link.ExpiresAtUtc,
                    link.NotificationSubmittedAtUtc,
                    lien.SubmittedForSaleAtUtc,
                    link.ResponseStatus,
                    link.ResponseAmount,
                    link.RespondedAtUtc);
            })
            .ToList();
    }

    private static async Task<List<SellingBuyerAccessLink>> LoadBuyerAccessLinksAsync(
        IQueryable<SellingBuyerAccessLink> linkQuery,
        HashSet<Guid> buyerContactIds,
        CancellationToken ct)
    {
        if (buyerContactIds.Count == 0)
            return [];

        return await linkQuery
            .Where(link => buyerContactIds.Contains(link.BuyerContactId))
            .ToListAsync(ct);
    }

    private static async Task<Dictionary<Guid, Lien>> LoadLiensByIdAsync(
        LiensDbContext db,
        Guid tenantId,
        IReadOnlyCollection<Guid> lienIds,
        CancellationToken ct)
    {
        var liens = new Dictionary<Guid, Lien>();

        foreach (var lienId in lienIds)
        {
            var lien = await db.Liens
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == lienId, ct);
            if (lien is not null)
                liens[lien.Id] = lien;
        }

        return liens;
    }

    private static async Task<HashSet<Guid>> ResolveBuyerContactIdsAsync(
        LiensDbContext db,
        Guid tenantId,
        string? email,
        CancellationToken ct)
    {
        var buyerContactIds = new HashSet<Guid>();
        var normalizedEmail = email?.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(normalizedEmail))
            return buyerContactIds;

        var contactIds = await db.Contacts
            .AsNoTracking()
            .Where(contact =>
                contact.TenantId == tenantId &&
                contact.IsActive &&
                contact.Email != null &&
                contact.Email.ToLower() == normalizedEmail &&
                (contact.ContactType == ContactType.LienHolder ||
                 contact.ContactType == ContactType.FundingCompany))
            .Select(contact => contact.Id)
            .ToListAsync(ct);

        foreach (var contactId in contactIds)
            buyerContactIds.Add(contactId);

        return buyerContactIds;
    }

    private static async Task<Dictionary<SellerDisplayKey, SellerOrganizationDisplay>> ResolveSellerDisplaysAsync(
        LiensDbContext db,
        ISellerOrganizationDisplayResolver sellerDisplayResolver,
        Guid tenantId,
        IEnumerable<BuyerOfferedLienSource> sources,
        CancellationToken ct)
    {
        var sellerKeys = sources
            .Select(source => new SellerDisplayKey(source.SellerOrgId, source.SellerUserId))
            .Distinct()
            .ToArray();
        if (sellerKeys.Length == 0)
            return [];

        var names = new Dictionary<SellerDisplayKey, SellerOrganizationDisplay>();
        var contactCache = new Dictionary<Guid, List<Contact>>();
        foreach (var sellerKey in sellerKeys)
        {
            if (!contactCache.TryGetValue(sellerKey.SellerOrgId, out var contacts))
            {
                contacts = await db.Contacts
                    .AsNoTracking()
                    .Where(item =>
                        item.TenantId == tenantId &&
                        item.IsActive &&
                        item.OrgId == sellerKey.SellerOrgId)
                    .ToListAsync(ct);
                contactCache[sellerKey.SellerOrgId] = contacts;
            }

            names[sellerKey] = await sellerDisplayResolver.ResolveAsync(
                tenantId,
                sellerKey.SellerOrgId,
                contacts,
                sellerUserId: sellerKey.SellerUserId,
                fallbackEmail: null,
                ct: ct);
        }

        return names;
    }

    private static async Task<Dictionary<Guid, string>> ResolveProviderNamesAsync(
        LiensDbContext db,
        Guid tenantId,
        IReadOnlyCollection<BuyerOfferedLienSource> sources,
        CancellationToken ct)
    {
        var names = new Dictionary<Guid, string>();
        var facilityIds = sources
            .Select(source => source.FacilityId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToArray();

        if (facilityIds.Length > 0)
        {
            foreach (var facilityId in facilityIds)
            {
                var facility = await db.Facilities
                    .AsNoTracking()
                    .Where(item => item.TenantId == tenantId && item.Id == facilityId)
                    .Select(item => new { item.Id, item.Name })
                    .FirstOrDefaultAsync(ct);

                if (!string.IsNullOrWhiteSpace(facility?.Name))
                    names[facility.Id] = facility.Name.Trim();
            }
        }

        return names;
    }

    private static async Task<string?> ResolveProviderNameAsync(
        LiensDbContext db,
        Guid tenantId,
        Guid? facilityId,
        CancellationToken ct)
    {
        if (!facilityId.HasValue)
            return null;

        var facility = await db.Facilities
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.Id == facilityId.Value)
            .Select(item => new { item.Name })
            .FirstOrDefaultAsync(ct);

        return FirstNonEmpty(new[] { facility?.Name });
    }

    private static async Task<IReadOnlyList<BuyerOfferedLienDocument>> ResolveBuyerOfferedLienDocumentsAsync(
        LiensDbContext db,
        Guid tenantId,
        Guid accessLinkId,
        Lien lien,
        CancellationToken ct)
    {
        var query = db.ServicingItems
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.LienId == lien.Id);

        var items = await query
            .OrderBy(item => item.CreatedAtUtc)
            .ThenBy(item => item.Id)
            .ToListAsync(ct);

        return items
            .Where(item => SellingDocumentTaskTypes.Contains(item.TaskType, StringComparer.Ordinal))
            .Select(item => MapBuyerOfferedLienDocument(item, accessLinkId))
            .Where(document => !string.IsNullOrWhiteSpace(document.FileName))
            .DistinctBy(document => document.FileName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static BuyerOfferedLienDocument MapBuyerOfferedLienDocument(ServicingItem item, Guid accessLinkId)
    {
        var fields = ParseLegacyNoteFields(item.Notes);
        var fileName = FirstNonEmpty(new[]
        {
            fields.GetValueOrDefault("originalFileName"),
            fields.GetValueOrDefault("displayName"),
            fields.GetValueOrDefault("filename"),
            item.Description,
        }) ?? string.Empty;

        var category = FirstNonEmpty(new[]
        {
            fields.GetValueOrDefault("documentCategory"),
            fields.GetValueOrDefault("category"),
            FormatSellingDocumentType(fields.GetValueOrDefault("documentType")),
            HumanizeDocumentTaskType(item.TaskType),
        });

        var sizeOrType = FirstNonEmpty(new[]
        {
            fields.GetValueOrDefault("size"),
            fields.GetValueOrDefault("fileSize"),
            fields.GetValueOrDefault("contentLength"),
            ResolveFileExtension(fileName),
        });
        var documentId = TryResolveDocumentId(fields, out var resolvedDocumentId)
            ? resolvedDocumentId
            : (Guid?)null;

        return new BuyerOfferedLienDocument(
            item.Id,
            fileName.Trim(),
            category,
            FormatDocumentSize(sizeOrType),
            FirstNonEmpty(new[]
            {
                fields.GetValueOrDefault("url"),
                documentId.HasValue ? $"/documents/{documentId.Value:D}" : null,
            }),
            BuildBuyerOfferedLienDocumentActionUrl(accessLinkId, documentId, "view"),
            BuildBuyerOfferedLienDocumentActionUrl(accessLinkId, documentId, "download"),
            item.CreatedAtUtc);
    }

    private static async Task<Guid?> ResolveBuyerOfferedLienDocumentReferenceAsync(
        LiensDbContext db,
        Guid tenantId,
        Guid lienId,
        Guid documentId,
        CancellationToken ct)
    {
        var items = await db.ServicingItems
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.LienId == lienId)
            .Select(item => new { item.TaskType, item.Notes })
            .ToListAsync(ct);

        foreach (var item in items.Where(item => SellingDocumentTaskTypes.Contains(item.TaskType, StringComparer.Ordinal)))
        {
            var fields = ParseLegacyNoteFields(item.Notes);
            if (TryResolveDocumentId(fields, out var resolvedDocumentId) &&
                resolvedDocumentId == documentId)
            {
                return resolvedDocumentId;
            }
        }

        return null;
    }

    private static async Task<string?> IssueBuyerOfferedLienDocumentAccessUrlAsync(
        IHttpClientFactory httpClientFactory,
        IServiceTokenIssuer serviceTokenIssuer,
        ILoggerFactory loggerFactory,
        SellingBuyerAccessLink accessLink,
        Guid documentId,
        string accessType,
        Guid actorUserId,
        CancellationToken ct)
    {
        var normalizedAccessType = string.Equals(accessType, "download", StringComparison.OrdinalIgnoreCase)
            ? "download"
            : "view";
        var path = normalizedAccessType == "download"
            ? $"/documents/{documentId:D}/download-url"
            : $"/documents/{documentId:D}/view-url";

        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        ApplyBuyerOfferedLienDocumentAuthorization(
            request,
            serviceTokenIssuer,
            loggerFactory,
            accessLink.TenantId,
            actorUserId);
        request.Headers.TryAddWithoutValidation("X-Organization-Id", accessLink.SellerOrgId.ToString());

        try
        {
            var client = httpClientFactory.CreateClient("DocumentsService");
            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
                return null;

            using var body = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(ct));
            var data = body.RootElement.TryGetProperty("data", out var dataElement)
                ? dataElement
                : body.RootElement;

            if (data.TryGetProperty("redeemUrl", out var redeemUrl) &&
                !string.IsNullOrWhiteSpace(redeemUrl.GetString()))
            {
                return NormalizeBuyerOfferedLienDocumentsRedeemUrl(redeemUrl.GetString()!);
            }

            if (data.TryGetProperty("accessToken", out var accessToken) &&
                !string.IsNullOrWhiteSpace(accessToken.GetString()))
            {
                return $"/documents/access/{Uri.EscapeDataString(accessToken.GetString()!)}";
            }
        }
        catch (HttpRequestException ex)
        {
            loggerFactory
                .CreateLogger(nameof(SellingEndpoints))
                .LogWarning(ex, "Documents access token request failed for buyer offered lien document {DocumentId}", documentId);
        }
        catch (JsonException ex)
        {
            loggerFactory
                .CreateLogger(nameof(SellingEndpoints))
                .LogWarning(ex, "Documents access token response was invalid for buyer offered lien document {DocumentId}", documentId);
        }

        return null;
    }

    private static void ApplyBuyerOfferedLienDocumentAuthorization(
        HttpRequestMessage request,
        IServiceTokenIssuer serviceTokenIssuer,
        ILoggerFactory loggerFactory,
        Guid tenantId,
        Guid actorUserId)
    {
        if (!serviceTokenIssuer.IsConfigured)
            return;

        try
        {
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                serviceTokenIssuer.IssueToken(tenantId.ToString(), actorUserId.ToString(), DocumentsServiceAudience));
        }
        catch (Exception ex)
        {
            loggerFactory
                .CreateLogger(nameof(SellingEndpoints))
                .LogWarning(ex, "Unable to mint Documents service token for tenant {TenantId}", tenantId);
        }
    }

    private static string NormalizeBuyerOfferedLienDocumentsRedeemUrl(string redeemUrl)
    {
        var trimmed = redeemUrl.Trim();
        if (trimmed.StartsWith("/documents/access/", StringComparison.OrdinalIgnoreCase))
            return trimmed;
        if (trimmed.StartsWith("/access/", StringComparison.OrdinalIgnoreCase))
            return $"/documents{trimmed}";
        return trimmed;
    }

    private static string? BuildBuyerOfferedLienDocumentActionUrl(Guid accessLinkId, Guid? documentId, string action)
    {
        if (!documentId.HasValue)
            return null;

        var normalizedAction = string.Equals(action, "download", StringComparison.OrdinalIgnoreCase)
            ? "download"
            : "view";
        return $"/api/lien/api/liens/selling/buyer/liens/{accessLinkId:D}/documents/{documentId.Value:D}/{normalizedAction}";
    }

    private static async Task<IReadOnlyList<SellingPortalMessage>> ResolveBuyerOfferedLienMessagesAsync(
        LiensDbContext db,
        SellingBuyerAccessLink accessLink,
        CancellationToken ct)
        => await db.SellingPortalMessages
            .AsNoTracking()
            .Where(message =>
                message.TenantId == accessLink.TenantId &&
                message.LienId == accessLink.LienId &&
                message.SellerOrgId == accessLink.SellerOrgId &&
                message.BuyerOrgId == accessLink.BuyerOrgId &&
                message.BuyerContactId == accessLink.BuyerContactId)
            .OrderBy(message => message.CreatedAtUtc)
            .ThenBy(message => message.Id)
            .ToListAsync(ct);

    private static BuyerOfferedLienDetailResponse MapBuyerOfferedLienDetail(
        SellingBuyerAccessLink accessLink,
        Lien lien,
        SellerOrganizationDisplay sellerDisplay,
        Contact? buyerContact,
        string? providerName,
        IReadOnlyList<BuyerOfferedLienDocument> documents,
        IReadOnlyList<SellingPortalMessage> messages)
    {
        var status = GetBuyerOfferedLienStatus(accessLink.ResponseStatus);
        var askAmount = lien.AskAmount ?? lien.OfferPrice;
        var submittedAtUtc = accessLink.NotificationSubmittedAtUtc ?? lien.SubmittedForSaleAtUtc ?? accessLink.CreatedAtUtc;
        var sellerName = FirstNonEmpty(new[] { sellerDisplay.Name, sellerDisplay.Company, "Seller unavailable" }) ?? "Seller unavailable";
        var resolvedSellerCompany = FirstNonEmpty(new[] { sellerDisplay.Company, sellerDisplay.Name });
        var title = FirstNonEmpty(new[] { sellerName, ResolveLienSubjectName(lien), lien.LienNumber }) ?? lien.Id.ToString();
        var subtitle = FirstNonEmpty(new[] { resolvedSellerCompany, providerName, lien.LienNumber });
        var canRespond = status == BuyerOfferedLienStatuses.Pending &&
            IsBuyerResponseActionableOffer(lien.Status, lien.SellerStatus);
        var allowedActions = canRespond
            ? new[] { "view", "accept", "decline" }
            : new[] { "view" };

        return new BuyerOfferedLienDetailResponse(
            accessLink.Id,
            lien.Id,
            lien.LienNumber,
            title,
            subtitle,
            new BuyerOfferedLienSellerDetail(
                sellerName,
                resolvedSellerCompany,
                sellerDisplay.Email),
            new BuyerOfferedLienBuyerDetail(
                FirstNonEmpty(new[] { buyerContact?.DisplayName }),
                FirstNonEmpty(new[] { buyerContact?.Organization }),
                buyerContact?.Email,
                buyerContact?.Phone),
            FirstNonEmpty(new[] { providerName }),
            status,
            submittedAtUtc,
            lien.InitialServiceDate,
            lien.EndServiceDate,
            lien.OriginalAmount,
            askAmount,
            lien.HighestBidAmount,
            accessLink.ResponseAmount,
            FirstNonEmpty(new[] { lien.Description, lien.Notes }),
            accessLink.ExpiresAtUtc,
            accessLink.ResponseStatus,
            accessLink.ResponseNotes,
            accessLink.RespondedAtUtc,
            allowedActions,
            documents,
            messages.Select(MapBuyerOfferedLienMessage).ToList(),
            BuildBuyerOfferedLienActivity(accessLink));
    }

    private static BuyerOfferedLienMessage MapBuyerOfferedLienMessage(SellingPortalMessage message)
        => new(
            message.Id,
            message.SenderType,
            message.SenderName,
            BuildInitials(message.SenderName),
            message.SenderEmail,
            message.Message,
            message.CreatedAtUtc,
            string.Equals(message.SenderType, SellingPortalMessageSenderType.Buyer, StringComparison.Ordinal));

    private static IReadOnlyList<BuyerOfferedLienActivityItem> BuildBuyerOfferedLienActivity(
        SellingBuyerAccessLink accessLink)
    {
        if (string.IsNullOrWhiteSpace(accessLink.ResponseStatus) || !accessLink.RespondedAtUtc.HasValue)
            return [];

        var label = string.Equals(accessLink.ResponseStatus, SellingBuyerResponseStatus.Accepted, StringComparison.Ordinal)
            ? "Pending -> Accepted"
            : "Pending -> Declined";

        return
        [
            new BuyerOfferedLienActivityItem(
                $"{accessLink.Id:N}-response",
                label,
                accessLink.RespondedAtUtc.Value,
                accessLink.ResponseNotes)
        ];
    }

    private static BuyerOfferedLienRow MapBuyerOfferedLienRow(
        BuyerOfferedLienSource source,
        IReadOnlyDictionary<SellerDisplayKey, SellerOrganizationDisplay> sellerDisplays,
        IReadOnlyDictionary<Guid, string> providerNames)
    {
        var status = GetBuyerOfferedLienStatus(source.ResponseStatus);
        var askAmount = source.AskAmount ?? source.OfferPrice;
        var offeredAmount = source.ResponseAmount ?? askAmount ?? 0m;
        var receivedAtUtc = source.NotificationSubmittedAtUtc ?? source.SubmittedForSaleAtUtc ?? source.CreatedAtUtc;
        var canRespond = status == BuyerOfferedLienStatuses.Pending &&
            IsBuyerResponseActionableOffer(source.LienStatus, source.SellerStatus);
        IReadOnlyList<string> allowedActions = canRespond
            ? ["view", "accept", "decline"]
            : new[] { "view" };
        var sellerDisplay = sellerDisplays.TryGetValue(new SellerDisplayKey(source.SellerOrgId, source.SellerUserId), out var resolvedSellerDisplay)
            ? resolvedSellerDisplay
            : new SellerOrganizationDisplay(
                "Seller unavailable",
                "Seller company unavailable",
                null);

        return new BuyerOfferedLienRow(
            source.AccessLinkId,
            source.LienNumber,
            source.FacilityId.HasValue && providerNames.TryGetValue(source.FacilityId.Value, out var providerName)
                ? providerName
                : "Provider unavailable",
            sellerDisplay.Name,
            source.InitialServiceDate,
            source.InitialServiceDate,
            source.OriginalAmount,
            source.OriginalAmount,
            askAmount,
            source.HighestBidAmount,
            source.HighestBidAmount,
            offeredAmount,
            receivedAtUtc,
            status,
            source.ExpiresAtUtc,
            allowedActions,
            $"/funding/offered-liens/{source.AccessLinkId}",
            sellerDisplay.Company,
            sellerDisplay.Name,
            BuildSearchText(source));
    }

    private static IEnumerable<BuyerOfferedLienRow> SortBuyerOfferedLiens(
        IEnumerable<BuyerOfferedLienRow> rows,
        string? sort,
        string? direction)
    {
        var descending = string.Equals(direction, "desc", StringComparison.OrdinalIgnoreCase);
        return NormalizeSortKey(sort) switch
        {
            "lienNumber" => Order(rows, row => row.LienNumber, descending),
            "sellerName" => Order(rows, row => row.SellerName, descending),
            "initialServiceDate" => Order(rows, row => row.InitialServiceDate ?? DateOnly.MinValue, descending),
            "billingAmount" => Order(rows, row => row.BillingAmount ?? decimal.MinValue, descending),
            "askAmount" => Order(rows, row => row.AskAmount ?? decimal.MinValue, descending),
            "highestBidAmount" => Order(rows, row => row.HighestBidAmount ?? decimal.MinValue, descending),
            "status" => Order(rows, row => StatusSortRank(row.Status), descending)
                .ThenBy(row => row.LienNumber, StringComparer.OrdinalIgnoreCase),
            _ => Order(rows, row => row.ReceivedAtUtc, descending: true)
                .ThenBy(row => row.LienNumber, StringComparer.OrdinalIgnoreCase),
        };
    }

    private static IOrderedEnumerable<BuyerOfferedLienRow> Order<TKey>(
        IEnumerable<BuyerOfferedLienRow> rows,
        Func<BuyerOfferedLienRow, TKey> keySelector,
        bool descending)
        => descending
            ? rows.OrderByDescending(keySelector)
            : rows.OrderBy(keySelector);

    private static IOrderedEnumerable<BuyerOfferedLienRow> Order(
        IEnumerable<BuyerOfferedLienRow> rows,
        Func<BuyerOfferedLienRow, string> keySelector,
        bool descending)
        => descending
            ? rows.OrderByDescending(keySelector, StringComparer.OrdinalIgnoreCase)
            : rows.OrderBy(keySelector, StringComparer.OrdinalIgnoreCase);

    private static bool BuyerOfferedLienMatchesSearch(BuyerOfferedLienRow row, string searchTerm)
        => ContainsSearch(row.LienNumber, searchTerm) ||
           ContainsSearch(row.ProviderName, searchTerm) ||
           ContainsSearch(row.SellerName, searchTerm) ||
           ContainsSearch(row.Status, searchTerm) ||
           ContainsSearch(row.SearchText, searchTerm) ||
           ContainsSearch(row.InitialServiceDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), searchTerm) ||
           ContainsSearch(row.BillingAmount?.ToString(CultureInfo.InvariantCulture), searchTerm) ||
           ContainsSearch(row.AskAmount?.ToString(CultureInfo.InvariantCulture), searchTerm) ||
           ContainsSearch(row.HighestBidAmount?.ToString(CultureInfo.InvariantCulture), searchTerm);

    private static bool ContainsSearch(string? value, string searchTerm)
        => !string.IsNullOrWhiteSpace(value) &&
           value.Contains(searchTerm, StringComparison.OrdinalIgnoreCase);

    private static string? NormalizeBuyerOfferedLienStatusFilter(string? status)
    {
        var trimmed = status?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed) ||
            string.Equals(trimmed, "All", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return trimmed switch
        {
            var value when string.Equals(value, BuyerOfferedLienStatuses.Pending, StringComparison.OrdinalIgnoreCase)
                => BuyerOfferedLienStatuses.Pending,
            var value when string.Equals(value, BuyerOfferedLienStatuses.Accepted, StringComparison.OrdinalIgnoreCase)
                => BuyerOfferedLienStatuses.Accepted,
            var value when string.Equals(value, BuyerOfferedLienStatuses.Declined, StringComparison.OrdinalIgnoreCase)
                => BuyerOfferedLienStatuses.Declined,
            _ => trimmed,
        };
    }

    private static string NormalizeSortKey(string? sort)
        => sort?.Trim() switch
        {
            "lienId" or "lienNumber" => "lienNumber",
            "seller" or "sellerName" => "sellerName",
            "initialServiceDate" or "serviceDate" => "initialServiceDate",
            "billingAmount" or "originalAmount" => "billingAmount",
            "askAmount" or "offeredAmount" => "askAmount",
            "highestBid" or "highestBidAmount" => "highestBidAmount",
            "status" => "status",
            _ => "receivedAtUtc",
        };

    private static int StatusSortRank(string status)
        => status switch
        {
            BuyerOfferedLienStatuses.Pending => 0,
            BuyerOfferedLienStatuses.Accepted => 1,
            BuyerOfferedLienStatuses.Declined => 2,
            _ => 3,
        };

    private static string GetBuyerOfferedLienStatus(string? responseStatus)
        => responseStatus switch
        {
            var value when string.Equals(value, SellingBuyerResponseStatus.Accepted, StringComparison.OrdinalIgnoreCase)
                => BuyerOfferedLienStatuses.Accepted,
            var value when string.Equals(value, SellingBuyerResponseStatus.Declined, StringComparison.OrdinalIgnoreCase)
                => BuyerOfferedLienStatuses.Declined,
            _ => BuyerOfferedLienStatuses.Pending,
        };

    private static bool IsBuyerResponseActionableOffer(string? lienStatus, string? sellerStatus)
        => string.Equals(sellerStatus, SellingLienStatus.SubmittedForSale, StringComparison.Ordinal) ||
           (string.IsNullOrWhiteSpace(sellerStatus) && IsBuyerResponseActionableLienStatus(lienStatus));

    private static bool IsBuyerResponseActionableLienStatus(string? status)
        => string.Equals(status, LienStatus.Offered, StringComparison.Ordinal) ||
           string.Equals(status, LienStatus.UnderReview, StringComparison.Ordinal);

    private static string? FirstNonEmpty(IEnumerable<string?> values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private static string? ResolveLienSubjectName(Lien lien)
        => FirstNonEmpty(new[]
        {
            string.Join(' ', new[] { lien.SubjectFirstName, lien.SubjectLastName }
                .Where(value => !string.IsNullOrWhiteSpace(value))),
        });

    private static Dictionary<string, string> ParseLegacyNoteFields(string? notes)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(notes))
            return result;

        var trimmed = notes.Trim();
        if (trimmed.StartsWith('{'))
        {
            try
            {
                using var document = JsonDocument.Parse(trimmed);
                if (document.RootElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var property in document.RootElement.EnumerateObject())
                    {
                        var value = property.Value.ValueKind == JsonValueKind.String
                            ? property.Value.GetString()
                            : property.Value.GetRawText();
                        if (!string.IsNullOrWhiteSpace(value))
                            result[property.Name] = value;
                    }
                }
            }
            catch (JsonException)
            {
                // Fall back to the legacy key/value parser below.
            }
        }

        foreach (var segment in notes.Split("; ", StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = segment.IndexOf('=');
            if (eq <= 0)
                continue;

            var key = segment[..eq].Trim();
            var value = segment[(eq + 1)..].Trim();
            if (!string.IsNullOrWhiteSpace(key))
                result[key] = value;
        }

        return result;
    }

    private static bool TryResolveDocumentId(
        IReadOnlyDictionary<string, string> fields,
        out Guid documentId)
    {
        if (Guid.TryParse(fields.GetValueOrDefault("documentId"), out documentId))
            return true;

        var url = fields.GetValueOrDefault("url");
        if (string.IsNullOrWhiteSpace(url))
            return false;

        var segment = url.Trim().TrimEnd('/').Split('/').LastOrDefault();
        return Guid.TryParse(segment, out documentId);
    }

    private static string? FormatSellingDocumentType(string? documentType)
    {
        if (string.IsNullOrWhiteSpace(documentType))
            return null;

        return documentType.Trim() switch
        {
            "LienAgreement" => "Signed Lien / LOP (Letter of Protection)",
            "MedicalBill" => "Itemized Bill / HCFA-1500 Form",
            "MedicalRecord" => "Clinical Chart Notes / Medical Records",
            "SettlementStatement" => "Settlement Statement",
            "Other" => "Supporting Document",
            var value => SplitCamelCase(value),
        };
    }

    private static string SplitCamelCase(string value)
    {
        var label = new StringBuilder(value.Length + 8);
        for (var i = 0; i < value.Length; i++)
        {
            var current = value[i];
            if (i > 0 && char.IsUpper(current) && char.IsLower(value[i - 1]))
                label.Append(' ');

            label.Append(current);
        }

        return label.ToString();
    }

    private static string FormatDocumentSize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var trimmed = value.Trim();
        if (!long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var bytes))
            return trimmed;

        if (bytes >= 1024L * 1024L)
            return $"{bytes / (1024m * 1024m):0.#} MB";
        if (bytes >= 1024L)
            return $"{bytes / 1024m:0.#} KB";

        return $"{bytes} B";
    }

    private static string? ResolveFileExtension(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        return string.IsNullOrWhiteSpace(extension)
            ? null
            : extension.TrimStart('.').ToUpperInvariant();
    }

    private static string HumanizeDocumentTaskType(string taskType)
        => taskType switch
        {
            "LegacyCaseDocument" => "Case Document",
            "LegacyLienDocument" => "Lien Document",
            "LegacyMedicalDocument" => "Medical Document",
            _ => "Document",
        };

    private static string BuildInitials(string value)
    {
        var parts = value
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length >= 2)
            return $"{parts[0][0]}{parts[1][0]}".ToUpperInvariant();

        return value.Length >= 2
            ? value[..2].ToUpperInvariant()
            : value.ToUpperInvariant();
    }

    private static string BuildSearchText(BuyerOfferedLienSource source)
        => string.Join(' ', new[]
        {
            source.ExternalReference,
            source.SubjectFirstName,
            source.SubjectLastName,
        }.Where(value => !string.IsNullOrWhiteSpace(value)));

    private static IReadOnlyDictionary<string, BuyerFundingMetricTrend?> BuildBuyerFundingMetricTrends(
        IReadOnlyCollection<BuyerDashboardOffer> offers,
        DateTime nowUtc)
    {
        var currentMonthStart = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var nextMonthStart = currentMonthStart.AddMonths(1);
        var previousMonthStart = currentMonthStart.AddMonths(-1);
        var previousMonthEndExclusive = currentMonthStart;
        var label = $"vs {FormatTrendMonthDay(previousMonthStart)} - {FormatTrendMonthDay(previousMonthEndExclusive.AddDays(-1))}";

        var currentMonth = new BuyerDashboardWindow(currentMonthStart, nextMonthStart);
        var previousMonth = new BuyerDashboardWindow(previousMonthStart, previousMonthEndExclusive);

        var currentPendingOffers = FilterTrendOffers(offers, currentMonth, BuyerOfferedLienStatuses.Pending).ToList();
        var previousPendingOffers = FilterTrendOffers(offers, previousMonth, BuyerOfferedLienStatuses.Pending).ToList();
        var currentAcceptedOffers = FilterTrendOffers(offers, currentMonth, BuyerOfferedLienStatuses.Accepted).ToList();
        var previousAcceptedOffers = FilterTrendOffers(offers, previousMonth, BuyerOfferedLienStatuses.Accepted).ToList();

        return new Dictionary<string, BuyerFundingMetricTrend?>
        {
            ["totalLienPending"] = BuildBuyerFundingMetricTrend(
                currentPendingOffers.Count,
                previousPendingOffers.Count,
                label),
            ["totalPendingOffered"] = BuildBuyerFundingMetricTrend(
                currentPendingOffers.Sum(offer => offer.Row.OfferedAmount),
                previousPendingOffers.Sum(offer => offer.Row.OfferedAmount),
                label),
            ["purchasedLiens"] = BuildBuyerFundingMetricTrend(
                currentAcceptedOffers.Count,
                previousAcceptedOffers.Count,
                label),
            ["capitalDeployed"] = BuildBuyerFundingMetricTrend(
                currentAcceptedOffers.Sum(offer => offer.Row.OfferedAmount),
                previousAcceptedOffers.Sum(offer => offer.Row.OfferedAmount),
                label),
        };
    }

    private static IEnumerable<BuyerDashboardOffer> FilterTrendOffers(
        IEnumerable<BuyerDashboardOffer> offers,
        BuyerDashboardWindow window,
        string status)
        => offers.Where(offer =>
            string.Equals(offer.Row.Status, status, StringComparison.Ordinal) &&
            IsWithinDashboardWindow(GetBuyerDashboardActivityAt(offer.Source), window));

    private static BuyerFundingMetricTrend BuildBuyerFundingMetricTrend(
        decimal currentValue,
        decimal previousValue,
        string label)
    {
        if (currentValue == 0m && previousValue == 0m)
            return new BuyerFundingMetricTrend(0m, "flat", label);

        if (previousValue == 0m)
            return new BuyerFundingMetricTrend(100m, "up", label);

        var percentChange = ((currentValue - previousValue) / previousValue) * 100m;
        var direction = percentChange switch
        {
            > 0m => "up",
            < 0m => "down",
            _ => "flat",
        };

        return new BuyerFundingMetricTrend(
            Math.Round(Math.Abs(percentChange), 1, MidpointRounding.AwayFromZero),
            direction,
            label);
    }

    private static string FormatTrendMonthDay(DateTime value)
        => value.ToString("MMM d", CultureInfo.InvariantCulture);

    private static IReadOnlyList<BuyerFundingPipelineStage> BuildBuyerFundingPipelineStages(
        IReadOnlyCollection<BuyerDashboardOffer> offers)
    {
        var rowsByStatus = offers
            .GroupBy(offer => offer.Row.Status, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => NormalizeBuyerFundingPipelineKey(group.Key), group => group.ToList(), StringComparer.Ordinal);

        return new[]
            {
                (Key: "pending", Label: BuyerOfferedLienStatuses.Pending),
                (Key: "accepted", Label: BuyerOfferedLienStatuses.Accepted),
                (Key: "declined", Label: BuyerOfferedLienStatuses.Declined),
            }
            .Select(stage =>
            {
                var stageOffers = rowsByStatus.GetValueOrDefault(stage.Key) ?? new List<BuyerDashboardOffer>();
                return new BuyerFundingPipelineStage(
                    stage.Key,
                    stage.Label,
                    stageOffers.Count,
                    stageOffers.Sum(offer => offer.Row.OfferedAmount),
                    null);
            })
            .Where(stage => stage.Count > 0)
            .ToList();
    }

    private static IReadOnlyList<BuyerFundingProviderPerformanceRow> BuildBuyerFundingProviderPerformance(
        IReadOnlyCollection<BuyerDashboardOffer> offers)
        => offers
            .GroupBy(offer => new
            {
                ProviderId = ResolveBuyerFundingProviderId(offer.Source, offer.Row.ProviderName),
                offer.Row.ProviderName,
            })
            .Select(group =>
            {
                var respondedHours = group
                    .Where(offer => offer.Source.RespondedAtUtc.HasValue)
                    .Select(offer => (offer.Source.RespondedAtUtc!.Value - offer.Row.ReceivedAtUtc).TotalHours)
                    .Where(hours => hours >= 0)
                    .ToList();

                return new BuyerFundingProviderPerformanceRow(
                    group.Key.ProviderId,
                    group.Key.ProviderName,
                    group.Count(),
                    group.Sum(offer => offer.Row.OfferedAmount),
                    group
                        .Where(offer => string.Equals(offer.Row.Status, BuyerOfferedLienStatuses.Accepted, StringComparison.Ordinal))
                        .Sum(offer => offer.Row.OfferedAmount),
                    respondedHours.Count == 0 ? null : Math.Round(respondedHours.Average(), 2));
            })
            .OrderByDescending(row => row.LienCount)
            .ThenBy(row => row.ProviderName, StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToList();

    private static string ResolveBuyerFundingProviderId(BuyerOfferedLienSource source, string providerName)
        => source.FacilityId?.ToString("D")
           ?? $"provider:{providerName.Trim().ToLowerInvariant()}";

    private static string NormalizeBuyerFundingPipelineKey(string status)
        => status switch
        {
            var value when string.Equals(value, BuyerOfferedLienStatuses.Accepted, StringComparison.OrdinalIgnoreCase)
                => "accepted",
            var value when string.Equals(value, BuyerOfferedLienStatuses.Declined, StringComparison.OrdinalIgnoreCase)
                => "declined",
            _ => "pending",
        };

    private static DateTime GetBuyerDashboardActivityAt(BuyerOfferedLienSource source)
        => source.RespondedAtUtc
           ?? source.NotificationSubmittedAtUtc
           ?? source.SubmittedForSaleAtUtc
           ?? source.CreatedAtUtc;

    private static DateTime GetBuyerDashboardReceivedAt(BuyerOfferedLienSource source)
        => source.NotificationSubmittedAtUtc
           ?? source.SubmittedForSaleAtUtc
           ?? source.CreatedAtUtc;

    private static BuyerDashboardWindow ResolveBuyerDashboardWindow(
        string? range,
        string? from,
        string? to,
        DateTime nowUtc)
    {
        if (string.Equals(range, "custom", StringComparison.OrdinalIgnoreCase))
        {
            var startDate = ParseDashboardDate(from);
            var endDate = ParseDashboardDate(to);
            if (!startDate.HasValue || !endDate.HasValue || endDate.Value < startDate.Value)
                return BuyerDashboardWindow.Empty;

            return new BuyerDashboardWindow(
                startDate.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                endDate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        }

        var days = string.Equals(range, "last7Days", StringComparison.OrdinalIgnoreCase) ? 7 : 30;
        var todayUtc = DateOnly.FromDateTime(nowUtc);
        return new BuyerDashboardWindow(
            todayUtc.AddDays(-(days - 1)).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            todayUtc.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
    }

    private static DateOnly? ParseDashboardDate(string? value)
        => DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : null;

    private static bool IsWithinDashboardWindow(DateTime value, BuyerDashboardWindow window)
        => !window.IsEmpty &&
           (!window.StartUtc.HasValue || value >= window.StartUtc.Value) &&
           (!window.EndExclusiveUtc.HasValue || value < window.EndExclusiveUtc.Value);

    private static bool IsCustomDashboardRange(string? range)
        => string.Equals(range, "custom", StringComparison.OrdinalIgnoreCase);

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

    private static async Task<IResult> ImportPatientDetailsReport(
        HttpRequest request,
        LiensDbContext db,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId = RequireUserId(ctx);

        if (!request.HasFormContentType)
        {
            return Results.BadRequest(new
            {
                error = new
                {
                    code = "invalid_content_type",
                    message = "Content-Type must be multipart/form-data.",
                },
            });
        }

        var form = await request.ReadFormAsync(ct);
        var file = form.Files["file"];
        var validationError = ValidateSellingImportFile(file);
        if (validationError is not null)
            return validationError;

        await using var stream = file!.OpenReadStream();
        var parsed = ParsePatientDetailsWorkbook(stream, file.FileName);
        var label = string.IsNullOrWhiteSpace(form["label"])
            ? Path.GetFileNameWithoutExtension(file.FileName)
            : form["label"].ToString().Trim();

        var dataContext = JsonSerializer.Serialize(parsed.Rows);
        var batch = BatchUpload.Create(
            tenantId,
            userId,
            label,
            SellingPatientDetailsTemplate,
            file.FileName,
            parsed.Rows.Count,
            dataContext);

        var details = parsed.Rows.Select((row, index) =>
            BatchUploadDetail.Create(
                tenantId,
                batch.Id,
                index + 1,
                JsonSerializer.Serialize(row),
                userId)).ToList();

        db.BatchUploads.Add(batch);
        db.BatchUploadDetails.AddRange(details);
        await db.SaveChangesAsync(ct);

        return Results.Ok(new
        {
            id = batch.Id,
            label = batch.Label,
            template = batch.Template,
            fileName = batch.FileName,
            rowCount = parsed.Rows.Count,
            columnCount = parsed.Columns.Count,
            columns = parsed.Columns,
            previewRows = parsed.Rows.Take(5).ToList(),
        });
    }

    private static async Task<IResult> GetBuyerDashboard(
        LiensDbContext db,
        ICurrentRequestContext ctx,
        ISellerOrganizationDisplayResolver sellerDisplayResolver,
        string? range = null,
        string? from = null,
        string? to = null,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var sources = await LoadBuyerOfferedLienSourcesAsync(db, ctx, ct);
        var sellerDisplays = await ResolveSellerDisplaysAsync(db, sellerDisplayResolver, tenantId, sources, ct);
        var providerNames = await ResolveProviderNamesAsync(db, tenantId, sources, ct);

        var offers = sources
            .Select(source => new BuyerDashboardOffer(
                source,
                MapBuyerOfferedLienRow(source, sellerDisplays, providerNames)))
            .OrderByDescending(offer => offer.Row.ReceivedAtUtc)
            .ThenBy(offer => offer.Row.LienNumber, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var nowUtc = DateTime.UtcNow;
        var window = ResolveBuyerDashboardWindow(range, from, to, nowUtc);
        var dashboardOffers = offers
            .Where(offer => IsWithinDashboardWindow(GetBuyerDashboardReceivedAt(offer.Source), window))
            .ToList();

        var pendingOffers = dashboardOffers
            .Where(offer => string.Equals(offer.Row.Status, BuyerOfferedLienStatuses.Pending, StringComparison.Ordinal))
            .ToList();

        var acceptedOffers = dashboardOffers
            .Where(offer => string.Equals(offer.Row.Status, BuyerOfferedLienStatuses.Accepted, StringComparison.Ordinal))
            .ToList();

        var trends = IsCustomDashboardRange(range)
            ? new Dictionary<string, BuyerFundingMetricTrend?>()
            : BuildBuyerFundingMetricTrends(dashboardOffers, nowUtc);

        var summary = new BuyerFundingDashboardSummary(
            pendingOffers.Count,
            pendingOffers.Sum(offer => offer.Row.BillingAmount ?? 0m),
            pendingOffers.Count,
            pendingOffers.Sum(offer => offer.Row.OfferedAmount),
            acceptedOffers.Count,
            acceptedOffers.Sum(offer => offer.Row.OfferedAmount),
            trends);

        var response = new BuyerFundingDashboardResponse(
            summary,
            pendingOffers
                .Take(5)
                .Select(offer => new BuyerFundingPendingOfferRow(
                    offer.Row.Id,
                    offer.Row.LienNumber,
                    offer.Row.ProviderName,
                    offer.Row.SellerCompany,
                    offer.Row.SellerContactName,
                    offer.Row.OfferedAmount,
                    offer.Row.ReceivedAtUtc,
                    offer.Row.ResponseDueAtUtc,
                    offer.Row.Status,
                    offer.Row.DetailHref))
                .ToList(),
            BuildBuyerFundingPipelineStages(dashboardOffers),
            BuildBuyerFundingProviderPerformance(dashboardOffers),
            new BuyerFundingOfferInboxSummary(
                pendingOffers.Count,
                0,
                pendingOffers
                    .OrderByDescending(offer => offer.Row.ReceivedAtUtc)
                    .Select(offer => (DateTime?)offer.Row.ReceivedAtUtc)
                    .FirstOrDefault()));

        return Results.Ok(response);
    }

    private static IResult? ValidateSellingImportFile(IFormFile? file)
    {
        if (file is null || file.Length == 0)
        {
            return Results.BadRequest(new
            {
                error = new
                {
                    code = "file_required",
                    message = "A non-empty Excel file is required.",
                },
            });
        }

        if (file.Length > SellingImportMaxBytes)
        {
            return Results.BadRequest(new
            {
                error = new
                {
                    code = "file_too_large",
                    message = $"The file exceeds the maximum allowed size of {SellingImportMaxBytes / (1024 * 1024)} MB.",
                },
            });
        }

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedSellingImportExtensions.Contains(extension))
        {
            return Results.BadRequest(new
            {
                error = new
                {
                    code = "unsupported_file_type",
                    message = "Only .xls and .xlsx files are supported.",
                },
            });
        }

        return null;
    }

    private static IResult DownloadBulkImportTemplate()
    {
        var csv = string.Join(
            Environment.NewLine,
            [
                string.Join(',', SellingBulkImportTemplateColumns.Select(EscapeCsvField)),
                string.Join(',', SellingBulkImportTemplateExample.Select(EscapeCsvField)),
            ]);

        return Results.File(
            Encoding.UTF8.GetBytes(csv),
            "text/csv; charset=utf-8",
            "selling-lien-import-template.csv");
    }

    private static async Task<IResult> CreateBulkImport(
        HttpRequest request,
        LiensDbContext db,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var userId = RequireUserId(ctx);

        if (!request.HasFormContentType)
        {
            return Results.BadRequest(new
            {
                error = new
                {
                    code = "invalid_content_type",
                    message = "Content-Type must be multipart/form-data.",
                },
            });
        }

        var form = await request.ReadFormAsync(ct);
        var file = form.Files["file"];
        var validationError = ValidateSellingBulkImportFile(file);
        if (validationError is not null)
            return validationError;

        var templateType = form["templateType"].ToString();
        if (string.IsNullOrWhiteSpace(templateType))
            templateType = SellingBulkImportTemplateType;

        if (!string.Equals(templateType, SellingBulkImportTemplateType, StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest(new
            {
                error = new
                {
                    code = "unsupported_template_type",
                    message = $"templateType must be '{SellingBulkImportTemplateType}'.",
                },
            });
        }

        if (!TryResolveSellingBulkImportOption(
                form["defaultListingVisibility"].ToString(),
                SellingListingVisibility.Private,
                SellingListingVisibility.All,
                out var defaultListingVisibility))
        {
            return Results.BadRequest(new
            {
                error = new
                {
                    code = "invalid_default_listing_visibility",
                    message = $"defaultListingVisibility must be one of: {string.Join(", ", SellingListingVisibility.All)}.",
                },
            });
        }

        if (!TryResolveSellingBulkImportOption(
                form["defaultSellerStatus"].ToString(),
                SellingLienStatus.Pending,
                SellingLienStatus.All,
                out var defaultSellerStatus))
        {
            return Results.BadRequest(new
            {
                error = new
                {
                    code = "invalid_default_seller_status",
                    message = $"defaultSellerStatus must be one of: {string.Join(", ", SellingLienStatus.All)}.",
                },
            });
        }

        await using var stream = file!.OpenReadStream();
        var parsed = ParseSellingBulkImportFile(stream, file.FileName);
        foreach (var row in parsed.Rows)
        {
            row.TryAdd("Listing Visibility", defaultListingVisibility);
            row.TryAdd("Seller Status", defaultSellerStatus);
        }

        var fileName = Truncate(Path.GetFileName(file.FileName), 255);
        var label = Truncate($"Selling lien import - {Path.GetFileNameWithoutExtension(fileName)}", 200);
        var batch = BatchUpload.Create(
            tenantId,
            userId,
            label,
            SellingBulkImportTemplateType,
            fileName,
            parsed.Rows.Count,
            JsonSerializer.Serialize(parsed.Rows));
        batch.SetProcessStatus("UPLOADED", userId);

        var details = parsed.Rows.Select((row, index) =>
            BatchUploadDetail.Create(
                tenantId,
                batch.Id,
                index + 1,
                JsonSerializer.Serialize(row),
                userId)).ToList();

        db.BatchUploads.Add(batch);
        db.BatchUploadDetails.AddRange(details);
        await db.SaveChangesAsync(ct);

        return Results.Ok(new
        {
            importId = batch.Id,
            status = "Uploaded",
            totalRows = parsed.Rows.Count,
        });
    }

    private static IResult? ValidateSellingBulkImportFile(IFormFile? file)
    {
        if (file is null || file.Length == 0)
        {
            return Results.BadRequest(new
            {
                error = new
                {
                    code = "file_required",
                    message = "A non-empty CSV, XLS, or XLSX file is required.",
                },
            });
        }

        if (file.Length > SellingImportMaxBytes)
        {
            return Results.BadRequest(new
            {
                error = new
                {
                    code = "file_too_large",
                    message = $"The file exceeds the maximum allowed size of {SellingImportMaxBytes / (1024 * 1024)} MB.",
                },
            });
        }

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedSellingBulkImportExtensions.Contains(extension))
        {
            return Results.BadRequest(new
            {
                error = new
                {
                    code = "unsupported_file_type",
                    message = "Only .csv, .xls, and .xlsx files are supported.",
                },
            });
        }

        return null;
    }

    private static bool TryResolveSellingBulkImportOption(
        string value,
        string fallback,
        IReadOnlySet<string> allowedValues,
        out string normalizedValue)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            normalizedValue = fallback;
            return true;
        }

        normalizedValue = allowedValues.FirstOrDefault(candidate =>
                              string.Equals(candidate, value.Trim(), StringComparison.OrdinalIgnoreCase))
                          ?? string.Empty;
        return !string.IsNullOrEmpty(normalizedValue);
    }

    private static SellingBulkImportFile ParseSellingBulkImportFile(Stream stream, string fileName)
    {
        var extension = Path.GetExtension(fileName);
        var rawRows = extension.Equals(".csv", StringComparison.OrdinalIgnoreCase)
            ? ParseCsvRows(stream)
            : ParseWorkbookRows(stream, fileName);

        var headerRow = rawRows.FirstOrDefault(row => row.Any(value => !string.IsNullOrWhiteSpace(value)));
        if (headerRow is null)
        {
            throw new ValidationException("Unable to parse bulk import.",
                new Dictionary<string, string[]> { ["file"] = ["The import file must contain a header row."] });
        }

        var headerRowIndex = rawRows.IndexOf(headerRow);
        var headers = headerRow
            .Select(header => header.Trim())
            .ToList();
        if (headers.Any(string.IsNullOrWhiteSpace) ||
            headers.Distinct(StringComparer.OrdinalIgnoreCase).Count() != headers.Count)
        {
            throw new ValidationException("Unable to parse bulk import.",
                new Dictionary<string, string[]> { ["file"] = ["The header row cannot contain blank or duplicate column names."] });
        }

        var rows = new List<Dictionary<string, string>>();
        for (var rowIndex = headerRowIndex + 1; rowIndex < rawRows.Count; rowIndex++)
        {
            var sourceRow = rawRows[rowIndex];
            if (!sourceRow.Any(value => !string.IsNullOrWhiteSpace(value)))
                continue;

            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var columnIndex = 0; columnIndex < headers.Count; columnIndex++)
            {
                row[headers[columnIndex]] = columnIndex < sourceRow.Count
                    ? sourceRow[columnIndex].Trim()
                    : string.Empty;
            }

            rows.Add(row);
        }

        if (rows.Count == 0)
        {
            throw new ValidationException("Unable to parse bulk import.",
                new Dictionary<string, string[]> { ["file"] = ["The import file did not contain any data rows."] });
        }

        return new SellingBulkImportFile(headers, rows);
    }

    private static List<List<string>> ParseWorkbookRows(Stream stream, string fileName)
    {
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        buffer.Position = 0;

        using IWorkbook workbook = Path.GetExtension(fileName).Equals(".xls", StringComparison.OrdinalIgnoreCase)
            ? new HSSFWorkbook(buffer)
            : new XSSFWorkbook(buffer);
        var sheet = workbook.NumberOfSheets > 0 ? workbook.GetSheetAt(0) : null;
        if (sheet is null)
        {
            throw new ValidationException("Unable to parse bulk import.",
                new Dictionary<string, string[]> { ["file"] = ["The workbook does not contain any worksheets."] });
        }

        var formatter = new DataFormatter(CultureInfo.InvariantCulture);
        var rows = new List<List<string>>();
        for (var rowIndex = sheet.FirstRowNum; rowIndex <= sheet.LastRowNum; rowIndex++)
        {
            var row = sheet.GetRow(rowIndex);
            if (row is null || row.LastCellNum <= 0)
            {
                rows.Add([]);
                continue;
            }

            rows.Add(Enumerable.Range(0, row.LastCellNum)
                .Select(index => formatter.FormatCellValue(row.GetCell(index)).Trim())
                .ToList());
        }

        return rows;
    }

    private static List<List<string>> ParseCsvRows(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var inQuotedField = false;

        while (reader.Read() is var character && character >= 0)
        {
            var value = (char)character;
            if (value == '"')
            {
                if (inQuotedField && reader.Peek() == '"')
                {
                    field.Append(value);
                    _ = reader.Read();
                }
                else if (field.Length == 0 || inQuotedField)
                {
                    inQuotedField = !inQuotedField;
                }
                else
                {
                    field.Append(value);
                }

                continue;
            }

            if (!inQuotedField && value == ',')
            {
                row.Add(field.ToString());
                field.Clear();
                continue;
            }

            if (!inQuotedField && (value == '\r' || value == '\n'))
            {
                if (value == '\r' && reader.Peek() == '\n')
                    _ = reader.Read();

                row.Add(field.ToString());
                rows.Add(row);
                row = [];
                field.Clear();
                continue;
            }

            field.Append(value);
        }

        if (inQuotedField)
        {
            throw new ValidationException("Unable to parse bulk import.",
                new Dictionary<string, string[]> { ["file"] = ["The CSV file contains an unterminated quoted value."] });
        }

        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row);
        }

        return rows;
    }

    private static string EscapeCsvField(string value)
    {
        return value.IndexOfAny([',', '"', '\r', '\n']) >= 0
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }

    private static string Truncate(string value, int maximumLength) =>
        value.Length <= maximumLength ? value : value[..maximumLength];

    private static SellingPatientDetailsImport ParsePatientDetailsWorkbook(Stream stream, string fileName)
    {
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);

        if (LooksLikeHtmlSpreadsheet(buffer))
            return ParsePatientDetailsHtml(buffer);

        buffer.Position = 0;
        IWorkbook workbook = Path.GetExtension(fileName).Equals(".xls", StringComparison.OrdinalIgnoreCase)
            ? new HSSFWorkbook(buffer)
            : new XSSFWorkbook(buffer);

        var sheet = workbook.NumberOfSheets > 0
            ? workbook.GetSheetAt(0)
            : null;

        if (sheet is null)
            throw new ValidationException("Unable to parse workbook.",
                new Dictionary<string, string[]> { ["file"] = ["The workbook does not contain any worksheets."] });

        var formatter = new DataFormatter(CultureInfo.InvariantCulture);
        var rows = new List<List<string>>();
        for (var rowIndex = sheet.FirstRowNum; rowIndex <= sheet.LastRowNum; rowIndex++)
        {
            var row = sheet.GetRow(rowIndex);
            if (row is null || row.LastCellNum <= 0)
            {
                rows.Add([]);
                continue;
            }

            rows.Add(Enumerable.Range(0, row.LastCellNum)
                .Select(index => formatter.FormatCellValue(row.GetCell(index)).Trim())
                .ToList());
        }

        return ParsePatientDetailsRows(rows);
    }

    private static SellingPatientDetailsImport ParsePatientDetailsHtml(Stream stream)
    {
        stream.Position = 0;
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var html = reader.ReadToEnd();
        var document = new HtmlDocument();
        document.LoadHtml(html);

        var rows = document.DocumentNode
            .SelectNodes("//tr")
            ?.Select(rowNode => rowNode.SelectNodes("./th|./td")
                ?.Select(cell => HtmlEntity.DeEntitize(cell.InnerText).Trim())
                .ToList() ?? [])
            .ToList()
            ?? [];

        return ParsePatientDetailsRows(rows);
    }

    private static SellingPatientDetailsImport ParsePatientDetailsRows(IReadOnlyList<List<string>> rawRows)
    {
        var headerRowIndex = FindHeaderRowIndex(rawRows);
        if (headerRowIndex < 0)
        {
            throw new ValidationException("Unable to parse workbook.",
                new Dictionary<string, string[]> { ["file"] = ["The patient details header row could not be found."] });
        }

        var headerRow = rawRows[headerRowIndex];
        var headerIndexes = headerRow
            .Select((header, index) => new
            {
                Index = index,
                Header = header.Trim(),
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Header))
            .ToList();

        var requiredHeaders = new[] { "#", "Last Name", "First Name", "MR#", "DOB" };
        var missingHeaders = requiredHeaders
            .Where(required => !headerIndexes.Any(item => string.Equals(item.Header, required, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (missingHeaders.Count > 0)
        {
            throw new ValidationException("Unable to parse workbook.",
                new Dictionary<string, string[]> { ["file"] = [$"The workbook is missing required columns: {string.Join(", ", missingHeaders)}."] });
        }

        var rows = new List<Dictionary<string, string>>();
        for (var rowIndex = headerRowIndex + 1; rowIndex < rawRows.Count; rowIndex++)
        {
            var row = rawRows[rowIndex];
            var parsedRow = new Dictionary<string, string>(StringComparer.Ordinal);
            var hasData = false;

            foreach (var header in headerIndexes)
            {
                var value = header.Index < row.Count
                    ? row[header.Index].Trim()
                    : string.Empty;

                parsedRow[header.Header] = value;
                hasData |= !string.IsNullOrWhiteSpace(value);
            }

            if (!hasData)
                continue;

            rows.Add(parsedRow);
        }

        if (rows.Count == 0)
        {
            throw new ValidationException("Unable to parse workbook.",
                new Dictionary<string, string[]> { ["file"] = ["The workbook did not contain any patient detail rows."] });
        }

        return new SellingPatientDetailsImport(
            headerIndexes.Select(item => item.Header).ToList(),
            rows);
    }

    private static int FindHeaderRowIndex(IReadOnlyList<List<string>> rows)
    {
        for (var rowIndex = 0; rowIndex < Math.Min(rows.Count, 50); rowIndex++)
        {
            var cells = rows[rowIndex]
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (cells.Contains("#") &&
                cells.Contains("Last Name") &&
                cells.Contains("First Name") &&
                cells.Contains("MR#") &&
                cells.Contains("DOB"))
            {
                return rowIndex;
            }
        }

        return -1;
    }

    private static bool LooksLikeHtmlSpreadsheet(MemoryStream stream)
    {
        stream.Position = 0;
        var buffer = new byte[Math.Min(stream.Length, 1024)];
        _ = stream.Read(buffer, 0, buffer.Length);
        stream.Position = 0;

        var prefix = Encoding.UTF8.GetString(buffer)
            .TrimStart('\uFEFF', '\0', '\r', '\n', '\t', ' ');

        return prefix.StartsWith("<html", StringComparison.OrdinalIgnoreCase)
            || prefix.StartsWith("<!doctype html", StringComparison.OrdinalIgnoreCase)
            || prefix.StartsWith("<table", StringComparison.OrdinalIgnoreCase);
    }

    private static class BuyerOfferedLienStatuses
    {
        public const string Pending = "Pending";
        public const string Accepted = "Accepted";
        public const string Declined = "Declined";
    }

    private sealed record BuyerFundingDashboardResponse(
        BuyerFundingDashboardSummary Summary,
        IReadOnlyList<BuyerFundingPendingOfferRow> PendingOffers,
        IReadOnlyList<BuyerFundingPipelineStage> PipelineStages,
        IReadOnlyList<BuyerFundingProviderPerformanceRow> ProviderPerformance,
        BuyerFundingOfferInboxSummary OfferInbox);

    private sealed record BuyerFundingDashboardSummary(
        int TotalLienPendingCount,
        decimal TotalLienPendingAmount,
        int TotalPendingOfferCount,
        decimal TotalPendingOfferedAmount,
        int PurchasedLienCount,
        decimal CapitalDeployedAmount,
        IReadOnlyDictionary<string, BuyerFundingMetricTrend?> Trends);

    private sealed record BuyerFundingMetricTrend(
        decimal Value,
        string Direction,
        string? Label);

    private sealed record BuyerFundingPendingOfferRow(
        Guid Id,
        string LienNumber,
        string ProviderName,
        string SellerCompany,
        string SellerName,
        decimal OfferedAmount,
        DateTime ReceivedAtUtc,
        DateTime? ResponseDueAtUtc,
        string Status,
        string DetailHref);

    private sealed record BuyerFundingPipelineStage(
        string Key,
        string Label,
        int Count,
        decimal TotalAmount,
        decimal? ConversionRatePercent);

    private sealed record BuyerFundingProviderPerformanceRow(
        string ProviderId,
        string ProviderName,
        int LienCount,
        decimal OfferedAmount,
        decimal AcceptedAmount,
        double? AverageResponseHours);

    private sealed record BuyerFundingOfferInboxSummary(
        int PendingCount,
        int UnreadCount,
        DateTime? LatestReceivedAtUtc);

    private sealed record BuyerDashboardOffer(
        BuyerOfferedLienSource Source,
        BuyerOfferedLienRow Row);

    private sealed record BuyerDashboardWindow(
        DateTime? StartUtc,
        DateTime? EndExclusiveUtc,
        bool IsEmpty = false)
    {
        public static BuyerDashboardWindow Empty { get; } = new(null, null, IsEmpty: true);
    }

    private readonly record struct SellerDisplayKey(Guid SellerOrgId, Guid? SellerUserId);

    private sealed record BuyerOfferedLienSource(
        Guid AccessLinkId,
        Guid LienId,
        Guid SellerOrgId,
        Guid? SellerUserId,
        Guid? FacilityId,
        string LienNumber,
        string LienStatus,
        string? SellerStatus,
        string? ExternalReference,
        string? SubjectFirstName,
        string? SubjectLastName,
        DateOnly? InitialServiceDate,
        decimal OriginalAmount,
        decimal? OfferPrice,
        decimal? AskAmount,
        decimal? HighestBidAmount,
        DateTime CreatedAtUtc,
        DateTime ExpiresAtUtc,
        DateTime? NotificationSubmittedAtUtc,
        DateTime? SubmittedForSaleAtUtc,
        string? ResponseStatus,
        decimal? ResponseAmount,
        DateTime? RespondedAtUtc);

    private sealed record BuyerOfferedLiensResult(
        IReadOnlyList<BuyerOfferedLienRow> Rows,
        int Page,
        int PageSize,
        int Total);

    private sealed record BuyerOfferedLienRow(
        Guid Id,
        string LienNumber,
        string ProviderName,
        string SellerName,
        DateOnly? InitialServiceDate,
        DateOnly? ServiceDate,
        decimal? BillingAmount,
        decimal? OriginalAmount,
        decimal? AskAmount,
        decimal? HighestBidAmount,
        decimal? HighestBid,
        decimal OfferedAmount,
        DateTime ReceivedAtUtc,
        string Status,
        DateTime? ResponseDueAtUtc,
        IReadOnlyList<string> AllowedActions,
        string DetailHref,
        [property: JsonIgnore] string SellerCompany,
        [property: JsonIgnore] string SellerContactName,
        [property: JsonIgnore] string SearchText);

    private sealed record BuyerOfferedLienDetailResponse(
        Guid Id,
        Guid LienId,
        string LienNumber,
        string Title,
        string? Subtitle,
        BuyerOfferedLienSellerDetail Seller,
        BuyerOfferedLienBuyerDetail Buyer,
        string? ProviderName,
        string Status,
        DateTime SubmittedAtUtc,
        DateOnly? InitialServiceDate,
        DateOnly? EndServiceDate,
        decimal BillingAmount,
        decimal? AskAmount,
        decimal? HighestBidAmount,
        decimal? ResponseAmount,
        string? Notes,
        DateTime ResponseDueAtUtc,
        string? ResponseStatus,
        string? ResponseNotes,
        DateTime? RespondedAtUtc,
        IReadOnlyList<string> AllowedActions,
        IReadOnlyList<BuyerOfferedLienDocument> Documents,
        IReadOnlyList<BuyerOfferedLienMessage> Messages,
        IReadOnlyList<BuyerOfferedLienActivityItem> Activity);

    private sealed record BuyerOfferedLienSellerDetail(
        string Name,
        string? Company,
        string? Email);

    private sealed record BuyerOfferedLienBuyerDetail(
        string? ContactName,
        string? Company,
        string? Email,
        string? Phone);

    private sealed record BuyerOfferedLienDocument(
        Guid Id,
        string FileName,
        string? Category,
        string SizeOrType,
        string? Url,
        string? ViewUrl,
        string? DownloadUrl,
        DateTime CreatedAtUtc);

    private sealed record BuyerOfferedLienMessage(
        Guid Id,
        string SenderType,
        string SenderName,
        string SenderInitials,
        string? SenderEmail,
        string Message,
        DateTime CreatedAtUtc,
        bool IsCurrentUser);

    private sealed record BuyerOfferedLienActivityItem(
        string Id,
        string Label,
        DateTime OccurredAtUtc,
        string? Notes);

    private sealed record SellingPatientDetailsImport(
        List<string> Columns,
        List<Dictionary<string, string>> Rows);

    private sealed record SellingBulkImportFile(
        List<string> Columns,
        List<Dictionary<string, string>> Rows);
}
