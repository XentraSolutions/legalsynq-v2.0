using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using BuildingBlocks.Context;
using BuildingBlocks.Exceptions;
using HtmlAgilityPack;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Domain;
using Liens.Domain.Entities;
using Liens.Domain.Enums;
using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Liens.Api.Endpoints;

public static class SellingEndpoints
{
    private const long SellingImportMaxBytes = 50L * 1024 * 1024;
    private const string SellingPatientDetailsTemplate = "SELLING_PATIENT_DETAILS_REPORT";
    private static readonly HashSet<string> AllowedSellingImportExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".xls",
        ".xlsx",
    };

    public static void MapSellingEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/liens/selling")
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(LiensPermissions.ProductCode)
            .RequireSellMode();

        group.MapPost("/imports/patient-details", ImportPatientDetailsReport)
            .RequirePermission(LiensPermissions.LienSaleCreate)
            .WithMetadata(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(SellingImportMaxBytes));

        group.MapPost("/liens/{lienId:guid}/confirm-sale", ConfirmSale)
            .RequirePermission(LiensPermissions.LienSaleUpdate);

        var buyerGroup = app.MapGroup("/api/liens/selling/buyer")
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(LiensPermissions.ProductCode);

        buyerGroup.MapGet("/liens", GetBuyerOfferedLiens)
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
        HttpContext httpContext,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var sellerOrgId = RequireOrgId(ctx);
        var userId = RequireUserId(ctx);
        var idempotencyKey = httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();
        var result = await service.ConfirmSaleAsync(
            tenantId,
            lienId,
            sellerOrgId,
            userId,
            request,
            idempotencyKey,
            ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetBuyerOfferedLiens(
        LiensDbContext db,
        ICurrentRequestContext ctx,
        string? status = null,
        string? search = null,
        string? sort = null,
        string? direction = null,
        int page = 1,
        int pageSize = 10,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId(ctx);
        var authenticatedOrgId = RequireOrgId(ctx);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var buyerOrgIds = await ResolveBuyerOrgIdsAsync(db, tenantId, authenticatedOrgId, ctx.Email, ct);
        var linkQuery = db.SellingBuyerAccessLinks
            .AsNoTracking()
            .Where(link =>
                link.TenantId == tenantId &&
                link.Purpose == SellingAccessLinkPurposes.ConfirmSaleBuyerResponse &&
                link.RevokedAtUtc == null);

        var links = await LoadBuyerAccessLinksAsync(linkQuery, buyerOrgIds, ct);

        var lienIds = links.Select(link => link.LienId).Distinct().ToArray();
        var liens = await LoadLiensByIdAsync(db, tenantId, lienIds, ct);

        var sources = links
            .Where(link => liens.ContainsKey(link.LienId))
            .Select(link =>
            {
                var lien = liens[link.LienId];
                return new BuyerOfferedLienSource(
                    link.Id,
                    lien.Id,
                    link.SellerOrgId,
                    lien.FacilityId,
                    link.Token,
                    lien.LienNumber,
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
                    link.ResponseAmount);
            })
            .ToList();

        var sellerNames = await ResolveSellerNamesAsync(db, tenantId, sources.Select(source => source.SellerOrgId), ct);
        var providerNames = await ResolveProviderNamesAsync(db, tenantId, sources, ct);

        IEnumerable<BuyerOfferedLienRow> rows = sources
            .Select(source => MapBuyerOfferedLienRow(source, sellerNames, providerNames));

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

    private static async Task<List<SellingBuyerAccessLink>> LoadBuyerAccessLinksAsync(
        IQueryable<SellingBuyerAccessLink> linkQuery,
        HashSet<Guid> buyerOrgIds,
        CancellationToken ct)
    {
        var links = new Dictionary<Guid, SellingBuyerAccessLink>();
        foreach (var buyerOrgId in buyerOrgIds)
        {
            var matches = await linkQuery
                .Where(link => link.BuyerOrgId == buyerOrgId)
                .ToListAsync(ct);

            foreach (var match in matches)
            {
                links[match.Id] = match;
            }
        }

        return links.Values.ToList();
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

    private static async Task<HashSet<Guid>> ResolveBuyerOrgIdsAsync(
        LiensDbContext db,
        Guid tenantId,
        Guid authenticatedOrgId,
        string? email,
        CancellationToken ct)
    {
        var buyerOrgIds = new HashSet<Guid> { authenticatedOrgId };
        var normalizedEmail = email?.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(normalizedEmail))
            return buyerOrgIds;

        var buyerContacts = await db.Contacts
            .AsNoTracking()
            .Where(contact =>
                contact.TenantId == tenantId &&
                contact.Email != null &&
                contact.Email.ToLower() == normalizedEmail)
            .Select(contact => new BuyerContactScope(
                contact.Id,
                contact.OrgId,
                contact.ContactType))
            .ToListAsync(ct);

        foreach (var contact in buyerContacts.Where(contact => IsFundingCompanyContactType(contact.ContactType)))
        {
            buyerOrgIds.Add(contact.Id);
            buyerOrgIds.Add(contact.OrgId);
        }

        return buyerOrgIds;
    }

    private static async Task<Dictionary<Guid, string>> ResolveSellerNamesAsync(
        LiensDbContext db,
        Guid tenantId,
        IEnumerable<Guid> sellerOrgIds,
        CancellationToken ct)
    {
        var orgIds = sellerOrgIds.Distinct().ToArray();
        if (orgIds.Length == 0)
            return [];

        var names = new Dictionary<Guid, string>();
        foreach (var orgId in orgIds)
        {
            var contact = await db.Contacts
                .AsNoTracking()
                .Where(item =>
                    item.TenantId == tenantId &&
                    item.IsActive &&
                    item.OrgId == orgId &&
                    item.ContactType == ContactType.LawFirm)
                .OrderBy(item => item.CreatedAtUtc)
                .Select(item => new ContactDisplay(
                    item.Id,
                    item.OrgId,
                    item.Organization,
                    item.DisplayName))
                .FirstOrDefaultAsync(ct);

            names[orgId] = FirstNonEmpty(new[] { contact?.Organization, contact?.DisplayName }) ??
                           "Seller unavailable";
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

    private static BuyerOfferedLienRow MapBuyerOfferedLienRow(
        BuyerOfferedLienSource source,
        IReadOnlyDictionary<Guid, string> sellerNames,
        IReadOnlyDictionary<Guid, string> providerNames)
    {
        var status = GetBuyerOfferedLienStatus(source.ResponseStatus);
        var askAmount = source.AskAmount ?? source.OfferPrice;
        var offeredAmount = source.ResponseAmount ?? askAmount ?? 0m;
        var receivedAtUtc = source.NotificationSubmittedAtUtc ?? source.SubmittedForSaleAtUtc ?? source.CreatedAtUtc;
        IReadOnlyList<string> allowedActions = status == BuyerOfferedLienStatuses.Pending
            ? ["view", "accept", "decline"]
            : new[] { "view" };

        return new BuyerOfferedLienRow(
            source.AccessLinkId,
            source.LienNumber,
            source.FacilityId.HasValue && providerNames.TryGetValue(source.FacilityId.Value, out var providerName)
                ? providerName
                : "Provider unavailable",
            sellerNames.TryGetValue(source.SellerOrgId, out var sellerName)
                ? sellerName
                : "Seller unavailable",
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
            $"/selling/public/{Uri.EscapeDataString(source.Token)}",
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

    private static bool IsFundingCompanyContactType(string contactType)
        => string.Equals(contactType, ContactType.LienHolder, StringComparison.Ordinal) ||
           string.Equals(contactType, ContactType.FundingCompany, StringComparison.Ordinal);

    private static string? FirstNonEmpty(IEnumerable<string?> values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private static string BuildSearchText(BuyerOfferedLienSource source)
        => string.Join(' ', new[]
        {
            source.ExternalReference,
            source.SubjectFirstName,
            source.SubjectLastName,
        }.Where(value => !string.IsNullOrWhiteSpace(value)));

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

    private sealed record BuyerContactScope(
        Guid Id,
        Guid OrgId,
        string ContactType);

    private sealed record ContactDisplay(
        Guid Id,
        Guid OrgId,
        string? Organization,
        string DisplayName);

    private sealed record BuyerOfferedLienSource(
        Guid AccessLinkId,
        Guid LienId,
        Guid SellerOrgId,
        Guid? FacilityId,
        string Token,
        string LienNumber,
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
        decimal? ResponseAmount);

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
        [property: JsonIgnore] string SearchText);

    private sealed record SellingPatientDetailsImport(
        List<string> Columns,
        List<Dictionary<string, string>> Rows);
}
