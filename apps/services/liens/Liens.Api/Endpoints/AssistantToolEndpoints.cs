using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using BuildingBlocks.Context;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Domain;
using Liens.Domain.Enums;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;

namespace Liens.Api.Endpoints;

public static class AssistantToolEndpoints
{
    private static readonly string[] QueueStatuses = LienStatus.All.ToArray();
    private static readonly string[] DraftStatusGroup = [LienStatus.Draft];
    private static readonly string[] MarketplaceStatusGroup = [LienStatus.Offered, LienStatus.UnderReview];
    private static readonly string[] ServicingStatusGroup = [LienStatus.Sold, LienStatus.Active, LienStatus.Disputed];

    public static void MapAssistantToolEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/assistant-tools")
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(LiensPermissions.ProductCode);

        group.MapGet("/liens/search", async (
            [AsParameters] AssistantLienSearchParams p,
            ILienService liens,
            ICaseService cases,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var tenantId = RequireTenantId(ctx);
            var visibility = BuildLienVisibilityScope(ctx);
            if (!visibility.CanReadAnyLien)
                return Results.Forbid();

            var caseId = await ResolveCaseIdAsync(cases, tenantId, p.CaseNumber, ct);
            if (!string.IsNullOrWhiteSpace(p.CaseNumber) && caseId is null)
            {
                return Results.Ok(new SynqLienLienSearchOutcome(true, "completed", null, 0, []));
            }

            var (createdFromUtc, createdToUtc) = ResolveDateWindow(p.DatePreset, p.CreatedFrom, p.CreatedTo);
            var result = await SearchLiensAsync(
                liens,
                tenantId,
                search: p.SubjectName ?? p.Search,
                status: NormalizeLienStatus(p.Status),
                statusGroup: NormalizeLienStatusGroup(p.StatusGroup),
                lienType: NormalizeLienType(p.LienType),
                caseId: caseId,
                visibility: visibility,
                pageSize: Math.Clamp(p.Top ?? 8, 1, 25),
                createdFromUtc: createdFromUtc,
                createdToUtc: createdToUtc,
                ct: ct);

            return Results.Ok(new SynqLienLienSearchOutcome(
                true,
                "completed",
                null,
                result.TotalCount,
                result.Items.Select(item => ToLienSearchResult(item, caseNumber: null)).ToList()));
        });

        group.MapGet("/liens/queue-summary", async (
            [AsParameters] AssistantLienQueueSummaryParams p,
            ILienService liens,
            ICaseService cases,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var tenantId = RequireTenantId(ctx);
            var visibility = BuildLienVisibilityScope(ctx);
            if (!visibility.CanReadAnyLien)
                return Results.Forbid();

            var caseId = await ResolveCaseIdAsync(cases, tenantId, p.CaseNumber, ct);
            if (!string.IsNullOrWhiteSpace(p.CaseNumber) && caseId is null)
            {
                return Results.Ok(EmptyQueueSummary());
            }

            var (windowFromUtc, windowToUtc) = ResolveCreatedWindow(p);
            var search = p.SubjectName ?? p.Search;
            var status = NormalizeLienStatus(p.Status);
            var statusGroup = NormalizeLienStatusGroup(p.StatusGroup);
            var lienType = NormalizeLienType(p.LienType);
            var recentTop = Math.Clamp(p.RecentTop ?? 5, 1, 10);

            var counts = new List<SynqLienStatusCount>(QueueStatuses.Length);
            foreach (var value in QueueStatuses)
            {
                var countResult = await liens.SearchAsync(
                    tenantId: tenantId,
                    search: search,
                    status: value,
                    lienType: lienType,
                    caseId: caseId,
                    facilityId: null,
                    page: 1,
                    pageSize: 1,
                    ct: ct,
                    createdFromUtc: windowFromUtc,
                    createdToUtc: windowToUtc,
                    visibleOrgId: visibility.OrgId,
                    includeSellerOrg: visibility.IncludeSellerOrg,
                    includeBuyerOrg: visibility.IncludeBuyerOrg,
                    includeHolderOrg: visibility.IncludeHolderOrg,
                    includeMarketplace: visibility.IncludeMarketplace);

                counts.Add(new SynqLienStatusCount(value, countResult.TotalCount));
            }

            var totalVisible = await liens.SearchAsync(
                tenantId: tenantId,
                search: null,
                status: null,
                lienType: null,
                caseId: null,
                facilityId: null,
                page: 1,
                pageSize: 1,
                ct: ct,
                visibleOrgId: visibility.OrgId,
                includeSellerOrg: visibility.IncludeSellerOrg,
                includeBuyerOrg: visibility.IncludeBuyerOrg,
                includeHolderOrg: visibility.IncludeHolderOrg,
                includeMarketplace: visibility.IncludeMarketplace);

            var recent = await SearchLiensAsync(
                liens,
                tenantId,
                search,
                status,
                statusGroup,
                lienType,
                caseId,
                visibility,
                recentTop,
                windowFromUtc,
                windowToUtc,
                ct);

            counts = counts
                .Where(item => item.Count > 0)
                .OrderByDescending(item => item.Count)
                .ThenBy(item => item.Status, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var matchingStatuses = ResolveStatusFilterValues(status, statusGroup);
            var windowLienCount = counts.Sum(item => item.Count);
            var matchingLienCount = matchingStatuses.Count == 0
                ? windowLienCount
                : counts
                    .Where(item => matchingStatuses.Contains(item.Status, StringComparer.OrdinalIgnoreCase))
                    .Sum(item => item.Count);
            var draftLienCount = counts
                .Where(item => DraftStatusGroup.Contains(item.Status, StringComparer.OrdinalIgnoreCase))
                .Sum(item => item.Count);
            var openLienCount = counts
                .Where(item => LienStatus.Open.Contains(item.Status))
                .Sum(item => item.Count);
            var closedLienCount = counts
                .Where(item => LienStatus.Terminal.Contains(item.Status))
                .Sum(item => item.Count);

            return Results.Ok(new SynqLienQueueSummaryOutcome(
                true,
                "completed",
                null,
                totalVisible.TotalCount,
                windowLienCount,
                matchingLienCount,
                draftLienCount,
                openLienCount,
                closedLienCount,
                windowFromUtc,
                windowToUtc,
                status,
                statusGroup,
                counts,
                recent.Items.Select(item => ToLienSearchResult(item, caseNumber: null)).ToList()));
        });

        group.MapGet("/liens/{id:guid}", async (
            Guid id,
            ILienService liens,
            ICaseService cases,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var tenantId = RequireTenantId(ctx);
            var visibility = BuildLienVisibilityScope(ctx);
            if (!visibility.CanReadAnyLien)
                return Results.Forbid();

            var lien = await liens.GetByIdAsync(tenantId, id, ct);
            if (lien is null)
                return Results.NotFound();

            if (!CanReadLien(lien, visibility))
                return Results.Forbid();

            return Results.Ok(new SynqLienLienLookupOutcome(true, "completed", null, await ToLienLookupResultAsync(lien, cases, tenantId, ct)));
        });

        group.MapGet("/liens/by-number/{lienNumber}", async (
            string lienNumber,
            ILienService liens,
            ICaseService cases,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var tenantId = RequireTenantId(ctx);
            var visibility = BuildLienVisibilityScope(ctx);
            if (!visibility.CanReadAnyLien)
                return Results.Forbid();

            var lien = await liens.GetByLienNumberAsync(tenantId, lienNumber, ct);
            if (lien is null)
                return Results.NotFound();

            if (!CanReadLien(lien, visibility))
                return Results.Forbid();

            return Results.Ok(new SynqLienLienLookupOutcome(true, "completed", null, await ToLienLookupResultAsync(lien, cases, tenantId, ct)));
        });

        group.MapGet("/cases/search", async (
            [AsParameters] AssistantCaseSearchParams p,
            ICaseService cases,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var tenantId = RequireTenantId(ctx);
            if (!string.IsNullOrWhiteSpace(p.CaseNumber))
            {
                var single = await cases.GetByCaseNumberAsync(tenantId, p.CaseNumber.Trim(), ct);
                return Results.Ok(new SynqLienCaseSearchOutcome(
                    true,
                    "completed",
                    null,
                    single is null ? 0 : 1,
                    single is null ? [] : [ToCaseSearchResult(single)]));
            }

            var result = await cases.SearchV3Async(
                tenantId,
                keyword: p.ClientName ?? p.Search,
                statusId: NormalizeCaseStatus(p.Status),
                page: 1,
                limit: Math.Clamp(p.Top ?? 8, 1, 25),
                sortBy: null,
                sortDirection: null,
                lawFirmOrgId: null,
                accidentTypeId: null,
                caseManagerId: null,
                ct: ct);

            var (openedFromUtc, openedToUtc) = ResolveDateWindow(
                p.DatePreset,
                p.OpenedFrom,
                p.OpenedTo);

            var filtered = result.Items
                .Where(item => MatchesText(NullIfWhiteSpace(item.LawFirm), p.LawFirm))
                .Where(item => MatchesText(NullIfWhiteSpace(item.CaseManager), p.CaseManager))
                .Where(item => MatchesText(NullIfWhiteSpace(item.CaseType), p.CaseType))
                .Where(item => MatchesText(NullIfWhiteSpace(item.AccidentType), p.AccidentType))
                .Where(item => MatchesState(item.StateOfIncident, p.State))
                .Where(item => IsWithinWindow(item.OpenedAtUtc ?? item.CreatedAtUtc, openedFromUtc, openedToUtc))
                .Take(Math.Clamp(p.Top ?? 8, 1, 25))
                .ToList();

            return Results.Ok(new SynqLienCaseSearchOutcome(
                true,
                "completed",
                null,
                filtered.Count == result.Items.Count ? result.TotalCount : filtered.Count,
                filtered.Select(ToCaseSearchResult).ToList()));
        })
        .RequirePermission(LiensPermissions.CaseRead);

        group.MapGet("/cases/{id:guid}", async (
            Guid id,
            [AsParameters] AssistantCaseLookupParams p,
            ICaseService cases,
            ILienService liens,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var tenantId = RequireTenantId(ctx);
            var item = await cases.GetByIdAsync(tenantId, id, ct);
            var visibility = BuildLienVisibilityScope(ctx);
            return item is null
                ? Results.NotFound()
                : Results.Ok(new SynqLienCaseLookupOutcome(true, "completed", null, await ToCaseLookupResultAsync(item, liens, tenantId, visibility, p.LiensTop, ct)));
        })
        .RequirePermission(LiensPermissions.CaseRead);

        group.MapGet("/cases/by-number/{caseNumber}", async (
            string caseNumber,
            [AsParameters] AssistantCaseLookupParams p,
            ICaseService cases,
            ILienService liens,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var tenantId = RequireTenantId(ctx);
            var item = await cases.GetByCaseNumberAsync(tenantId, caseNumber, ct);
            var visibility = BuildLienVisibilityScope(ctx);
            return item is null
                ? Results.NotFound()
                : Results.Ok(new SynqLienCaseLookupOutcome(true, "completed", null, await ToCaseLookupResultAsync(item, liens, tenantId, visibility, p.LiensTop, ct)));
        })
        .RequirePermission(LiensPermissions.CaseRead);

        group.MapGet("/cases/{id:guid}/insights", async (
            Guid id,
            [AsParameters] AssistantCaseInsightsParams p,
            ICaseService cases,
            ILienService liens,
            ILienCaseNoteService notes,
            IServicingItemService servicing,
            ILienTaskService tasks,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var tenantId = RequireTenantId(ctx);
            var item = await cases.GetByIdAsync(tenantId, id, ct);
            if (item is null)
                return Results.NotFound();

            var visibility = BuildLienVisibilityScope(ctx);
            var insights = await BuildCaseInsightsAsync(
                item,
                p,
                liens,
                notes,
                servicing,
                tasks,
                tenantId,
                visibility,
                ctx,
                ct);

            return Results.Ok(new SynqLienCaseInsightsOutcome(true, "completed", null, insights));
        })
        .RequirePermission(LiensPermissions.CaseRead);

        group.MapGet("/cases/by-number/{caseNumber}/insights", async (
            string caseNumber,
            [AsParameters] AssistantCaseInsightsParams p,
            ICaseService cases,
            ILienService liens,
            ILienCaseNoteService notes,
            IServicingItemService servicing,
            ILienTaskService tasks,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var tenantId = RequireTenantId(ctx);
            var item = await cases.GetByCaseNumberAsync(tenantId, caseNumber, ct);
            if (item is null)
                return Results.NotFound();

            var visibility = BuildLienVisibilityScope(ctx);
            var insights = await BuildCaseInsightsAsync(
                item,
                p,
                liens,
                notes,
                servicing,
                tasks,
                tenantId,
                visibility,
                ctx,
                ct);

            return Results.Ok(new SynqLienCaseInsightsOutcome(true, "completed", null, insights));
        })
        .RequirePermission(LiensPermissions.CaseRead);

        group.MapGet("/tasks/search", async (
            [AsParameters] AssistantTaskSearchParams p,
            ILienTaskService tasks,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var tenantId = RequireTenantId(ctx);
            var outcome = await SearchTasksForAssistantAsync(tasks, tenantId, ctx, p, ct);
            return Results.Ok(outcome);
        })
        .RequirePermission(LiensPermissions.TaskRead);

        group.MapGet("/servicing/search", async (
            [AsParameters] AssistantServicingSearchParams p,
            IServicingItemService servicing,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var tenantId = RequireTenantId(ctx);
            var outcome = await SearchServicingForAssistantAsync(servicing, tenantId, p, ct);
            return Results.Ok(outcome);
        })
        .RequirePermission(LiensPermissions.LienService);

        group.MapGet("/reports/summary", async (
            [AsParameters] AssistantReportSummaryParams p,
            ICaseService cases,
            ILienService liens,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var tenantId = RequireTenantId(ctx);
            var visibility = BuildLienVisibilityScope(ctx);
            if (!visibility.CanReadAnyLien)
                return Results.Forbid();

            var outcome = await BuildReportSummaryAsync(cases, liens, tenantId, visibility, p, ct);
            return Results.Ok(outcome);
        })
        .RequirePermission(LiensPermissions.CaseRead);
    }

    private static Guid RequireTenantId(ICurrentRequestContext ctx)
        => ctx.TenantId ?? throw new UnauthorizedAccessException("Tenant context is required.");

    private static LienVisibilityScope BuildLienVisibilityScope(ICurrentRequestContext ctx)
    {
        if (IsTenantAdminOrAbove(ctx) || HasPermission(ctx, LiensPermissions.LienRead))
            return LienVisibilityScope.All;

        var canReadOwn = HasPermission(ctx, LiensPermissions.LienReadOwn);
        var canBrowse = HasPermission(ctx, LiensPermissions.LienBrowse);
        var canReadHeld = HasPermission(ctx, LiensPermissions.LienReadHeld);

        if (!canReadOwn && !canBrowse && !canReadHeld)
            return LienVisibilityScope.None;

        if (ctx.OrgId is not { } orgId || orgId == Guid.Empty)
            return LienVisibilityScope.None;

        return new LienVisibilityScope(
            CanReadAnyLien: true,
            OrgId: orgId,
            IncludeSellerOrg: canReadOwn,
            IncludeBuyerOrg: canReadHeld,
            IncludeHolderOrg: canReadHeld,
            IncludeMarketplace: canBrowse);
    }

    private static bool CanReadLien(LienResponse lien, LienVisibilityScope visibility)
    {
        if (!visibility.CanReadAnyLien)
            return false;

        if (!visibility.OrgId.HasValue)
            return true;

        var orgId = visibility.OrgId.Value;
        return (visibility.IncludeSellerOrg && (lien.OrgId == orgId || lien.SellingOrgId == orgId)) ||
               (visibility.IncludeBuyerOrg && lien.BuyingOrgId == orgId) ||
               (visibility.IncludeHolderOrg && lien.HoldingOrgId == orgId) ||
               (visibility.IncludeMarketplace && MarketplaceStatusGroup.Contains(lien.Status, StringComparer.OrdinalIgnoreCase));
    }

    private static bool IsTenantAdminOrAbove(ICurrentRequestContext ctx)
        => ctx.IsPlatformAdmin || ctx.Roles.Contains(Roles.TenantAdmin, StringComparer.OrdinalIgnoreCase);

    private static bool HasPermission(ICurrentRequestContext ctx, string permission)
        => ctx.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);

    private static async Task<Guid?> ResolveCaseIdAsync(
        ICaseService cases,
        Guid tenantId,
        string? caseNumber,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(caseNumber))
            return null;

        var item = await cases.GetByCaseNumberAsync(tenantId, caseNumber.Trim(), ct);
        return item?.Id;
    }

    private static async Task<SynqLienLienLookupResult> ToLienLookupResultAsync(
        LienResponse lien,
        ICaseService cases,
        Guid tenantId,
        CancellationToken ct)
    {
        CaseResponse? caseItem = null;
        if (lien.CaseId.HasValue)
            caseItem = await cases.GetByIdAsync(tenantId, lien.CaseId.Value, ct);

        return new SynqLienLienLookupResult(
            lien.Id,
            lien.LienNumber,
            lien.Status,
            lien.LienType,
            BuildSubjectDisplayName(lien),
            lien.CaseId,
            caseItem?.CaseNumber,
            NullIfWhiteSpace(caseItem?.Title),
            lien.OriginalAmount,
            lien.CurrentBalance,
            lien.OfferPrice,
            lien.PurchasePrice,
            lien.PayoffAmount,
            NullIfWhiteSpace(lien.Jurisdiction),
            lien.IsConfidential,
            lien.CreatedAtUtc,
            lien.UpdatedAtUtc,
            lien.IncidentDate,
            NullIfWhiteSpace(lien.PurchaseDate),
            lien.InitialServiceDate,
            lien.EndServiceDate,
            lien.TotalPurchase,
            lien.TotalBilling,
            ComputeReductionAmount(lien.TotalBilling, lien.TotalPurchase),
            ParseBooleanish(lien.IsServicing),
            NullIfWhiteSpace(lien.Description),
            0);
    }

    private static async Task<SynqLienCaseLookupResult> ToCaseLookupResultAsync(
        CaseResponse item,
        ILienService liens,
        Guid tenantId,
        LienVisibilityScope visibility,
        int? liensTop,
        CancellationToken ct)
    {
        var linkedLiens = await liens.SearchAsync(
            tenantId: tenantId,
            search: null,
            status: null,
            lienType: null,
            caseId: item.Id,
            facilityId: null,
            page: 1,
            pageSize: Math.Clamp(liensTop ?? 8, 1, 25),
            ct: ct,
            visibleOrgId: visibility.OrgId,
            includeSellerOrg: visibility.IncludeSellerOrg,
            includeBuyerOrg: visibility.IncludeBuyerOrg,
            includeHolderOrg: visibility.IncludeHolderOrg,
            includeMarketplace: visibility.IncludeMarketplace);

        return new SynqLienCaseLookupResult(
            item.Id,
            item.CaseNumber,
            item.ClientDisplayName,
            item.Status,
            NullIfWhiteSpace(item.Title),
            NullIfWhiteSpace(item.CaseType),
            NullIfWhiteSpace(item.CurrentMedicalStatus),
            NullIfWhiteSpace(item.LawFirm),
            NullIfWhiteSpace(item.CaseManager),
            item.DemandAmount,
            item.SettlementAmount,
            item.CreatedAtUtc,
            item.UpdatedAtUtc,
            linkedLiens.Items.Select(lien => ToLienSearchResult(lien, item.CaseNumber)).ToList(),
            item.DateOfIncident,
            item.ClientDob,
            IsMinor(item.ClientDob),
            NullIfWhiteSpace(item.ClientPhone),
            NullIfWhiteSpace(item.ClientEmail),
            BuildClientAddress(item),
            NullIfWhiteSpace(item.StateOfIncident),
            NullIfWhiteSpace(item.AccidentType),
            item.OpenedAtUtc,
            item.ClosedAtUtc);
    }

    private static SynqLienLienSearchResult ToLienSearchResult(LienResponse lien, string? caseNumber)
        => new(
            lien.Id,
            lien.LienNumber,
            lien.Status,
            lien.LienType,
            BuildSubjectDisplayName(lien),
            lien.CaseId,
            NullIfWhiteSpace(caseNumber),
            lien.OriginalAmount,
            lien.CurrentBalance,
            lien.CreatedAtUtc,
            lien.UpdatedAtUtc,
            NullIfWhiteSpace(lien.PurchaseDate),
            lien.TotalPurchase,
            lien.TotalBilling,
            0);

    private static SynqLienCaseSearchResult ToCaseSearchResult(CaseResponse item)
        => new(
            item.Id,
            item.CaseNumber,
            item.ClientDisplayName,
            item.Status,
            NullIfWhiteSpace(item.Title),
            NullIfWhiteSpace(item.CaseType),
            NullIfWhiteSpace(item.CurrentMedicalStatus),
            NullIfWhiteSpace(item.LawFirm),
            item.CreatedAtUtc,
            item.UpdatedAtUtc,
            NullIfWhiteSpace(item.CaseManager),
            NullIfWhiteSpace(item.StateOfIncident),
            NullIfWhiteSpace(item.AccidentType),
            item.DateOfIncident,
            item.OpenedAtUtc,
            item.ClosedAtUtc);

    private static async Task<PaginatedResult<LienResponse>> SearchLiensAsync(
        ILienService liens,
        Guid tenantId,
        string? search,
        string? status,
        string? statusGroup,
        string? lienType,
        Guid? caseId,
        LienVisibilityScope visibility,
        int pageSize,
        DateTime? createdFromUtc,
        DateTime? createdToUtc,
        CancellationToken ct)
    {
        var statuses = ResolveStatusFilterValues(status, statusGroup);
        if (statuses.Count == 0)
        {
            return await liens.SearchAsync(
                tenantId: tenantId,
                search: search,
                status: null,
                lienType: lienType,
                caseId: caseId,
                facilityId: null,
                page: 1,
                pageSize: pageSize,
                ct: ct,
                createdFromUtc: createdFromUtc,
                createdToUtc: createdToUtc,
                visibleOrgId: visibility.OrgId,
                includeSellerOrg: visibility.IncludeSellerOrg,
                includeBuyerOrg: visibility.IncludeBuyerOrg,
                includeHolderOrg: visibility.IncludeHolderOrg,
                includeMarketplace: visibility.IncludeMarketplace);
        }

        var items = new List<LienResponse>();
        var total = 0;
        foreach (var value in statuses)
        {
            var result = await liens.SearchAsync(
                tenantId: tenantId,
                search: search,
                status: value,
                lienType: lienType,
                caseId: caseId,
                facilityId: null,
                page: 1,
                pageSize: pageSize,
                ct: ct,
                createdFromUtc: createdFromUtc,
                createdToUtc: createdToUtc,
                visibleOrgId: visibility.OrgId,
                includeSellerOrg: visibility.IncludeSellerOrg,
                includeBuyerOrg: visibility.IncludeBuyerOrg,
                includeHolderOrg: visibility.IncludeHolderOrg,
                includeMarketplace: visibility.IncludeMarketplace);

            total += result.TotalCount;
            items.AddRange(result.Items);
        }

        return new PaginatedResult<LienResponse>
        {
            Items = items
                .OrderByDescending(item => item.CreatedAtUtc)
                .ThenByDescending(item => item.UpdatedAtUtc)
                .Take(pageSize)
                .ToList(),
            Page = 1,
            PageSize = pageSize,
            TotalCount = total,
        };
    }

    private static async Task<SynqLienCaseInsightsResult> BuildCaseInsightsAsync(
        CaseResponse item,
        AssistantCaseInsightsParams p,
        ILienService liens,
        ILienCaseNoteService notes,
        IServicingItemService servicing,
        ILienTaskService tasks,
        Guid tenantId,
        LienVisibilityScope visibility,
        ICurrentRequestContext ctx,
        CancellationToken ct)
    {
        var top = Math.Clamp(p.Top ?? 10, 1, 25);
        var (windowFromUtc, windowToUtc) = ResolveDateWindow(p.DatePreset, p.DateFrom, p.DateTo);
        var dateWindow = new SynqLienDateWindow(NormalizeDatePreset(p.DatePreset), windowFromUtc, windowToUtc);

        var linkedLienPage = await SearchLiensAsync(
            liens,
            tenantId,
            search: null,
            status: null,
            statusGroup: null,
            lienType: null,
            caseId: item.Id,
            visibility: visibility,
            pageSize: 100,
            createdFromUtc: null,
            createdToUtc: null,
            ct: ct);

        var lienInsights = new List<SynqLienLienInsight>();
        var caseDocuments = await GetCaseDocumentsAsync(servicing, tenantId, item.Id, ct);
        var lienDocuments = new List<SynqLienDocumentInsight>();
        var caseServicingItems = await SearchAllServicingItemsAsync(servicing, tenantId, item.Id, lienId: null, ct);
        var allServicingItems = new List<ServicingItemResponse>(caseServicingItems);

        foreach (var lien in linkedLienPage.Items)
        {
            var lienItems = await SearchAllServicingItemsAsync(servicing, tenantId, caseId: null, lien.Id, ct);
            allServicingItems.AddRange(lienItems);

            var documents = ExtractDocuments(lienItems, "lien").ToList();
            lienDocuments.AddRange(documents);
            lienInsights.Add(BuildLienInsight(lien, lienItems, documents.Count));
        }

        var allDocuments = caseDocuments.Concat(lienDocuments).ToList();
        var recentNotes = await notes.GetNotesAsync(tenantId, item.Id, ct);
        var noteInsights = recentNotes
            .Select(ToNoteInsight)
            .OrderByDescending(note => note.UpdatedAtUtc ?? note.CreatedAtUtc)
            .ToList();

        var taskOutcome = HasPermission(ctx, LiensPermissions.TaskRead) || IsTenantAdminOrAbove(ctx)
            ? await SearchTasksForAssistantAsync(
                tasks,
                tenantId,
                ctx,
                new AssistantTaskSearchParams
                {
                    CaseId = item.Id,
                    DatePreset = p.DatePreset,
                    DueFrom = p.DateFrom,
                    DueTo = p.DateTo,
                    Top = 100,
                },
                ct)
            : EmptyTaskSearchOutcome(dateWindow);

        var servicingInsights = allServicingItems
            .DistinctBy(i => i.Id)
            .Select(ToServicingInsight)
            .OrderByDescending(i => i.UpdatedAtUtc)
            .ToList();

        var windowNotes = noteInsights
            .Where(note => IsWithinWindow(note.UpdatedAtUtc ?? note.CreatedAtUtc, windowFromUtc, windowToUtc))
            .ToList();
        var windowDocuments = allDocuments
            .Where(document => IsWithinWindow(document.UploadedAtUtc, windowFromUtc, windowToUtc))
            .OrderByDescending(document => document.UploadedAtUtc)
            .ToList();
        var windowServicing = servicingInsights
            .Where(service => IsWithinWindow(service.UpdatedAtUtc, windowFromUtc, windowToUtc))
            .ToList();
        var windowTasks = taskOutcome.Tasks
            .Where(task => IsWithinWindow(task.UpdatedAtUtc, windowFromUtc, windowToUtc))
            .ToList();

        var openLiens = lienInsights.Where(lien => lien.IsOpen).ToList();
        var closedLiens = lienInsights.Where(lien => !lien.IsOpen).ToList();
        var highestBalanceLien = lienInsights
            .OrderByDescending(lien => lien.CurrentBalance ?? lien.TotalBilling ?? lien.OriginalAmount)
            .FirstOrDefault();
        var missingDocumentLiens = lienInsights
            .Where(lien => lien.MissingDocuments)
            .ToList();

        var totalPurchase = lienInsights.Sum(lien => lien.TotalPurchase ?? lien.CurrentBalance ?? lien.OriginalAmount);
        var totalBilling = lienInsights.Sum(lien => lien.TotalBilling ?? lien.OriginalAmount);
        var totalReduction = lienInsights.Sum(lien => lien.ReductionAmount);
        var outstandingBalance = lienInsights.Sum(lien => lien.CurrentBalance ?? Math.Max((lien.TotalBilling ?? lien.OriginalAmount) - lien.ReductionAmount, 0m));
        var noReductionCount = lienInsights.Count(lien => lien.ReductionAmount <= 0m);

        var requiredMissing = new List<string>();
        if (caseDocuments.Count == 0)
            requiredMissing.Add("At least one case-level supporting document");

        requiredMissing.AddRange(missingDocumentLiens.Select(lien => $"Supporting document for lien {lien.LienNumber}"));

        var allActivity = BuildActivity(item.Id, windowNotes, windowDocuments, windowServicing, windowTasks, lienInsights, windowFromUtc, windowToUtc)
            .OrderByDescending(activity => activity.IsImportant)
            .ThenByDescending(activity => activity.OccurredAtUtc)
            .Take(top)
            .ToList();

        var caseLookup = new SynqLienCaseLookupResult(
            item.Id,
            item.CaseNumber,
            item.ClientDisplayName,
            item.Status,
            NullIfWhiteSpace(item.Title),
            NullIfWhiteSpace(item.CaseType),
            NullIfWhiteSpace(item.CurrentMedicalStatus),
            NullIfWhiteSpace(item.LawFirm),
            NullIfWhiteSpace(item.CaseManager),
            item.DemandAmount,
            item.SettlementAmount,
            item.CreatedAtUtc,
            item.UpdatedAtUtc,
            linkedLienPage.Items.Select(lien => ToLienSearchResult(lien, item.CaseNumber)).ToList(),
            item.DateOfIncident,
            item.ClientDob,
            IsMinor(item.ClientDob),
            NullIfWhiteSpace(item.ClientPhone),
            NullIfWhiteSpace(item.ClientEmail),
            BuildClientAddress(item),
            NullIfWhiteSpace(item.StateOfIncident),
            NullIfWhiteSpace(item.AccidentType),
            item.OpenedAtUtc,
            item.ClosedAtUtc);

        return new SynqLienCaseInsightsResult(
            caseLookup,
            dateWindow,
            new SynqLienCaseLienMetrics(
                lienInsights.Count,
                openLiens.Count,
                closedLiens.Count,
                lienInsights.Count(lien => lien.IsMedical),
                lienInsights.Count(lien => lien.IsServicing),
                lienInsights.Count(lien => IsRejectedLienStatus(lien.Status)),
                lienInsights.Count(lien => lien.MissingPurchaseDate),
                missingDocumentLiens.Count,
                highestBalanceLien),
            new SynqLienFinancialMetrics(
                totalPurchase,
                totalBilling,
                totalReduction,
                outstandingBalance,
                item.SettlementAmount ?? item.DemandAmount,
                Math.Max(totalBilling - totalReduction, 0m),
                noReductionCount),
            new SynqLienDocumentMetrics(
                caseDocuments.Count,
                lienDocuments.Count,
                allDocuments.Count(IsMedicalRecordDocument),
                requiredMissing.Count,
                allDocuments.Count == 0 ? null : allDocuments.Max(document => document.UploadedAtUtc),
                requiredMissing,
                DocumentContentSummarizationAvailable: false),
            new SynqLienNoteMetrics(
                noteInsights.Count,
                windowNotes.Count,
                noteInsights.Count(note => note.IsImportant),
                noteInsights.Count == 0 ? null : noteInsights.Max(note => note.UpdatedAtUtc ?? note.CreatedAtUtc)),
            BuildServicingMetrics(servicingInsights),
            taskOutcome.Metrics,
            lienInsights.OrderByDescending(lien => lien.CurrentBalance ?? lien.TotalBilling ?? lien.OriginalAmount).Take(top).ToList(),
            allDocuments.OrderByDescending(document => document.UploadedAtUtc).Take(top).ToList(),
            noteInsights.Take(top).ToList(),
            servicingInsights.Take(top).ToList(),
            taskOutcome.Tasks.Take(top).ToList(),
            allActivity,
            [
                new SynqLienCapabilityStatus(
                    "document_content_summarization",
                    false,
                    "metadata_only",
                    "SynqLien assistant tools expose uploaded document metadata but not file/OCR content."),
                new SynqLienCapabilityStatus(
                    "unanswered_email_lookup",
                    false,
                    "not_correlated",
                    "No SynqLien case-email correlation source is exposed through the product assistant API."),
                new SynqLienCapabilityStatus(
                    "excel_file_generation",
                    p.IncludeExport,
                    p.IncludeExport ? "excel_ready_payload" : "not_requested",
                    p.IncludeExport
                        ? "The tool returns workbook-style sheets that a UI can export to Excel."
                        : "Set includeExport=true to include workbook-style sheet data.")
            ],
            p.IncludeExport ? BuildCaseExport(item, lienInsights, allDocuments, noteInsights, servicingInsights, taskOutcome.Tasks) : null);
    }

    private static async Task<SynqLienTaskSearchOutcome> SearchTasksForAssistantAsync(
        ILienTaskService tasks,
        Guid tenantId,
        ICurrentRequestContext ctx,
        AssistantTaskSearchParams p,
        CancellationToken ct)
    {
        var top = Math.Clamp(p.Top ?? 10, 1, 100);
        var (dueFromUtc, dueToUtc) = ResolveDateWindow(p.DatePreset, p.DueFrom, p.DueTo);
        var dateWindow = new SynqLienDateWindow(NormalizeDatePreset(p.DatePreset), dueFromUtc, dueToUtc);
        var status = NormalizeTaskStatus(p.StatusGroup is null ? p.Status : null);
        var statusGroup = NormalizeTaskStatusGroup(p.StatusGroup);
        var priority = NormalizeTaskPriority(p.Priority);
        var assignedUserId = p.AssignedUserId;
        var assignmentScope = NormalizeAssignmentScope(p.AssignmentScope);

        if (string.Equals(assignmentScope, "me", StringComparison.OrdinalIgnoreCase))
            assignedUserId = ctx.UserId;

        var result = await tasks.SearchAsync(
            tenantId,
            p.Search,
            status,
            priority,
            assignedUserId,
            p.CaseId,
            p.LienId,
            workflowStageId: null,
            assignmentScope: assignmentScope,
            currentUserId: ctx.UserId,
            page: 1,
            pageSize: 100,
            ct);

        var statusValues = ResolveTaskStatusValues(status, statusGroup);
        var taskInsights = result.Items
            .Select(task => ToTaskInsight(task, ctx.UserId))
            .Where(task => statusValues.Count == 0 || statusValues.Contains(task.Status, StringComparer.OrdinalIgnoreCase))
            .Where(task => IsDateWithinWindow(task.DueDateUtc, dueFromUtc, dueToUtc))
            .Where(task => p.Overdue is not true || task.IsOverdue)
            .Where(task => p.DueToday is not true || task.IsDueToday)
            .OrderByDescending(task => task.IsOverdue)
            .ThenBy(task => task.DueDateUtc ?? DateTime.MaxValue)
            .ThenByDescending(task => task.UpdatedAtUtc)
            .Take(top)
            .ToList();

        return new SynqLienTaskSearchOutcome(
            true,
            "completed",
            null,
            taskInsights.Count,
            dateWindow,
            BuildTaskMetrics(result.Items.Select(task => ToTaskInsight(task, ctx.UserId)).ToList(), ctx.UserId),
            taskInsights);
    }

    private static async Task<SynqLienServicingSearchOutcome> SearchServicingForAssistantAsync(
        IServicingItemService servicing,
        Guid tenantId,
        AssistantServicingSearchParams p,
        CancellationToken ct)
    {
        var top = Math.Clamp(p.Top ?? 10, 1, 100);
        var (dueFromUtc, dueToUtc) = ResolveDateWindow(p.DatePreset, p.DueFrom, p.DueTo);
        var dateWindow = new SynqLienDateWindow(NormalizeDatePreset(p.DatePreset), dueFromUtc, dueToUtc);
        var status = NormalizeServicingStatus(p.StatusGroup is null ? p.Status : null);
        var statusGroup = NormalizeServicingStatusGroup(p.StatusGroup);
        var priority = NormalizeServicingPriority(p.Priority);

        var result = await servicing.SearchAsync(
            tenantId,
            p.Search,
            status,
            priority,
            p.AssignedTo,
            p.CaseId,
            p.LienId,
            page: 1,
            pageSize: 100,
            ct);

        var statusValues = ResolveServicingStatusValues(status, statusGroup);
        var insights = result.Items
            .Select(ToServicingInsight)
            .Where(item => statusValues.Count == 0 || statusValues.Contains(item.Status, StringComparer.OrdinalIgnoreCase))
            .Where(item => IsDateWithinWindow(ToDateTime(item.DueDate), dueFromUtc, dueToUtc))
            .Where(item => p.Overdue is not true || item.IsOverdue)
            .OrderByDescending(item => item.IsOverdue)
            .ThenBy(item => item.DueDate ?? DateOnly.MaxValue)
            .ThenByDescending(item => item.UpdatedAtUtc)
            .Take(top)
            .ToList();

        var allInsights = result.Items.Select(ToServicingInsight).ToList();
        return new SynqLienServicingSearchOutcome(
            true,
            "completed",
            null,
            insights.Count,
            dateWindow,
            BuildServicingMetrics(allInsights),
            insights);
    }

    private static async Task<SynqLienReportSummaryOutcome> BuildReportSummaryAsync(
        ICaseService cases,
        ILienService liens,
        Guid tenantId,
        LienVisibilityScope visibility,
        AssistantReportSummaryParams p,
        CancellationToken ct)
    {
        var top = Math.Clamp(p.Top ?? 10, 1, 25);
        var (fromUtc, toUtc) = ResolveDateWindow(p.DatePreset, p.DateFrom, p.DateTo);
        var dateWindow = new SynqLienDateWindow(NormalizeDatePreset(p.DatePreset), fromUtc, toUtc);

        var allCases = await SearchAllCasesAsync(cases, tenantId, ct);
        var caseStatus = NormalizeCaseStatus(p.CaseStatusGroup is null ? p.CaseStatus : null);
        var caseStatusValues = ResolveCaseStatusValues(caseStatus, NormalizeCaseStatusGroup(p.CaseStatusGroup));
        var filteredCases = allCases
            .Where(item => caseStatusValues.Count == 0 || caseStatusValues.Contains(item.Status, StringComparer.OrdinalIgnoreCase))
            .Where(item => MatchesText(item.LawFirm, p.LawFirm))
            .Where(item => MatchesText(item.CaseManager, p.CaseManager))
            .Where(item => MatchesText(item.CaseType, p.CaseType))
            .Where(item => MatchesText(item.AccidentType, p.AccidentType))
            .Where(item => MatchesState(item.StateOfIncident, p.State))
            .Where(item => MatchesCaseSearch(item, p.Search))
            .Where(item => IsWithinWindow(item.OpenedAtUtc ?? item.CreatedAtUtc, fromUtc, toUtc))
            .ToList();

        var lienStatus = NormalizeLienStatus(p.LienStatusGroup is null ? p.LienStatus : null);
        var lienStatusGroup = NormalizeLienStatusGroup(p.LienStatusGroup);
        var lienResult = await SearchLiensAsync(
            liens,
            tenantId,
            search: p.Search,
            status: lienStatus,
            statusGroup: lienStatusGroup,
            lienType: null,
            caseId: null,
            visibility: visibility,
            pageSize: 100,
            createdFromUtc: fromUtc,
            createdToUtc: toUtc,
            ct: ct);

        var activeCases = filteredCases
            .Where(item => !string.Equals(item.Status, CaseStatus.Closed, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return new SynqLienReportSummaryOutcome(
            true,
            "completed",
            null,
            dateWindow,
            filteredCases.Count,
            activeCases.Count,
            filteredCases.Count(item => IsWithinWindow(item.OpenedAtUtc ?? item.CreatedAtUtc, fromUtc, toUtc)),
            lienResult.TotalCount,
            lienResult.Items.Count(item => LienStatus.Terminal.Contains(item.Status)),
            activeCases
                .GroupBy(item => NullIfWhiteSpace(item.CaseManager) ?? "Unassigned", StringComparer.OrdinalIgnoreCase)
                .Select(group => new SynqLienGroupCount(group.Key, group.Count()))
                .OrderByDescending(group => group.Count)
                .ThenBy(group => group.Key)
                .Take(top)
                .ToList(),
            activeCases
                .GroupBy(item => NullIfWhiteSpace(item.LawFirm) ?? "Unassigned", StringComparer.OrdinalIgnoreCase)
                .Select(group => new SynqLienGroupCount(group.Key, group.Count()))
                .OrderByDescending(group => group.Count)
                .ThenBy(group => group.Key)
                .Take(top)
                .ToList(),
            filteredCases
                .OrderByDescending(item => item.OpenedAtUtc ?? item.CreatedAtUtc)
                .Take(top)
                .Select(ToCaseSearchResult)
                .ToList(),
            lienResult.Items
                .OrderByDescending(item => item.UpdatedAtUtc)
                .Take(top)
                .Select(item => ToLienSearchResult(item, caseNumber: null))
                .ToList());
    }

    private static SynqLienTaskSearchOutcome EmptyTaskSearchOutcome(SynqLienDateWindow dateWindow)
        => new(
            true,
            "completed",
            null,
            0,
            dateWindow,
            new SynqLienTaskMetrics(0, 0, 0, 0, 0, 0, []),
            []);

    private static async Task<List<SynqLienDocumentInsight>> GetCaseDocumentsAsync(
        IServicingItemService servicing,
        Guid tenantId,
        Guid caseId,
        CancellationToken ct)
    {
        var items = await SearchAllServicingItemsAsync(servicing, tenantId, caseId, lienId: null, ct);
        return ExtractDocuments(items.Where(i => string.Equals(i.TaskType, "LegacyCaseDocument", StringComparison.Ordinal)), "case")
            .ToList();
    }

    private static async Task<List<ServicingItemResponse>> SearchAllServicingItemsAsync(
        IServicingItemService servicing,
        Guid tenantId,
        Guid? caseId,
        Guid? lienId,
        CancellationToken ct)
    {
        const int pageSize = 100;
        var page = 1;
        var items = new List<ServicingItemResponse>();

        while (true)
        {
            var result = await servicing.SearchAsync(
                tenantId,
                search: null,
                status: null,
                priority: null,
                assignedTo: null,
                caseId,
                lienId,
                page,
                pageSize,
                ct);

            if (result.Items.Count == 0)
                break;

            items.AddRange(result.Items);
            if (items.Count >= result.TotalCount)
                break;

            page++;
        }

        return items;
    }

    private static IEnumerable<SynqLienDocumentInsight> ExtractDocuments(
        IEnumerable<ServicingItemResponse> items,
        string source)
    {
        foreach (var item in items.Where(IsDocumentServicingItem))
        {
            var fields = ParseLegacyNoteFields(item.Notes);
            var fileName = fields.GetValueOrDefault("filename",
                fields.GetValueOrDefault("originalFileName", item.Description));
            var documentTypeId = fields.TryGetValue("typeId", out var typeId)
                ? typeId
                : fields.TryGetValue("documentTypeId", out var legacyTypeId)
                    ? legacyTypeId
                    : null;

            yield return new SynqLienDocumentInsight(
                item.Id,
                item.CaseId,
                item.LienId,
                string.IsNullOrWhiteSpace(fileName) ? item.Description : fileName,
                documentTypeId,
                GetLegacyDocumentUrl(fields),
                source,
                item.CreatedAtUtc,
                item.UpdatedAtUtc);
        }
    }

    private static bool IsDocumentServicingItem(ServicingItemResponse item)
        => string.Equals(item.TaskType, "LegacyCaseDocument", StringComparison.Ordinal) ||
           string.Equals(item.TaskType, "LegacyLienDocument", StringComparison.Ordinal) ||
           string.Equals(item.TaskType, "LegacyMedicalDocument", StringComparison.Ordinal);

    private static SynqLienLienInsight BuildLienInsight(
        LienResponse lien,
        IReadOnlyList<ServicingItemResponse> servicingItems,
        int supportingDocumentCount)
    {
        var medicalCodes = servicingItems
            .Where(item => string.Equals(item.TaskType, "LegacyMedicalCode", StringComparison.Ordinal))
            .Select(item => ParseLegacyNoteFields(item.Notes))
            .ToList();

        var totalPurchase = medicalCodes.Sum(fields => ParseDecimal(fields.GetValueOrDefault("purchaseAmount")));
        var totalBilling = medicalCodes.Sum(fields => ParseDecimal(fields.GetValueOrDefault("billingAmount")));

        if (totalPurchase <= 0m)
            totalPurchase = lien.TotalPurchase ?? lien.PurchasePrice ?? lien.CurrentBalance ?? 0m;

        if (totalBilling <= 0m)
            totalBilling = lien.TotalBilling ?? lien.OriginalAmount;

        var reduction = Math.Max(totalBilling - totalPurchase, 0m);
        var purchaseDate = NullIfWhiteSpace(lien.PurchaseDate) ?? FormatDate(lien.IncidentDate);
        var isMedical = string.Equals(lien.LienType, LienType.MedicalLien, StringComparison.OrdinalIgnoreCase);

        return new SynqLienLienInsight(
            lien.Id,
            lien.LienNumber,
            lien.Status,
            lien.LienType,
            BuildSubjectDisplayName(lien),
            lien.OriginalAmount,
            lien.CurrentBalance,
            totalPurchase,
            totalBilling,
            reduction,
            purchaseDate,
            lien.InitialServiceDate,
            lien.EndServiceDate,
            LienStatus.Open.Contains(lien.Status),
            isMedical,
            ParseBooleanish(lien.IsServicing) ||
                string.Equals(lien.Status, LienStatus.Sold, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(lien.Status, LienStatus.Active, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(lien.Status, LienStatus.Disputed, StringComparison.OrdinalIgnoreCase),
            string.IsNullOrWhiteSpace(purchaseDate),
            supportingDocumentCount == 0,
            supportingDocumentCount,
            lien.CreatedAtUtc,
            lien.UpdatedAtUtc);
    }

    private static SynqLienNoteInsight ToNoteInsight(CaseNoteResponse note)
    {
        var isImportant = note.IsPinned || IsImportantText(note.Content) || IsImportantText(note.Category);
        return new SynqLienNoteInsight(
            note.Id,
            note.Content,
            note.Category,
            note.IsPinned,
            note.CreatedByName,
            note.CreatedAtUtc,
            note.UpdatedAtUtc,
            isImportant);
    }

    private static SynqLienServicingInsight ToServicingInsight(ServicingItemResponse item)
    {
        var isActive = !string.Equals(item.Status, ServicingStatus.Completed, StringComparison.OrdinalIgnoreCase);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return new SynqLienServicingInsight(
            item.Id,
            item.TaskNumber,
            item.TaskType,
            item.Description,
            item.Status,
            item.Priority,
            item.AssignedTo,
            item.AssignedToUserId,
            item.CaseId,
            item.LienId,
            item.DueDate,
            isActive,
            isActive && item.DueDate.HasValue && item.DueDate.Value < today,
            item.CreatedAtUtc,
            item.UpdatedAtUtc);
    }

    private static SynqLienTaskInsight ToTaskInsight(TaskResponse task, Guid? currentUserId)
    {
        var isOpen = !IsTerminalTaskStatus(task.Status);
        var dueDateUtc = task.DueDate?.ToUniversalTime();
        var now = DateTime.UtcNow;
        return new SynqLienTaskInsight(
            task.Id,
            task.Title,
            NullIfWhiteSpace(task.Description),
            task.Status,
            task.Priority,
            task.AssignedUserId,
            task.CaseId,
            task.LinkedLiens.Select(link => link.LienId).Distinct().ToList(),
            dueDateUtc,
            isOpen,
            isOpen && dueDateUtc.HasValue && dueDateUtc.Value < now.Date,
            dueDateUtc.HasValue && dueDateUtc.Value.Date == now.Date,
            IsHighPriority(task.Priority),
            task.CreatedAtUtc,
            task.UpdatedAtUtc);
    }

    private static IReadOnlyList<SynqLienActivityInsight> BuildActivity(
        Guid caseId,
        IReadOnlyList<SynqLienNoteInsight> notes,
        IReadOnlyList<SynqLienDocumentInsight> documents,
        IReadOnlyList<SynqLienServicingInsight> servicing,
        IReadOnlyList<SynqLienTaskInsight> tasks,
        IReadOnlyList<SynqLienLienInsight> liens,
        DateTime? fromUtc,
        DateTime? toUtc)
    {
        var activity = new List<SynqLienActivityInsight>();

        activity.AddRange(notes.Select(note => new SynqLienActivityInsight(
            "note",
            $"Note by {note.CreatedByName}",
            note.Content,
            caseId,
            null,
            note.NoteId,
            note.UpdatedAtUtc ?? note.CreatedAtUtc,
            note.IsImportant)));

        activity.AddRange(documents.Select(document => new SynqLienActivityInsight(
            "document",
            $"Document uploaded: {document.FileName}",
            document.Url,
            caseId,
            document.LienId,
            document.ServicingItemId,
            document.UploadedAtUtc,
            IsMedicalRecordDocument(document))));

        activity.AddRange(servicing.Select(item => new SynqLienActivityInsight(
            "servicing",
            $"{item.TaskType}: {item.Status}",
            item.Description,
            item.CaseId ?? caseId,
            item.LienId,
            item.ServicingItemId,
            item.UpdatedAtUtc,
            item.IsOverdue || IsHighPriority(item.Priority))));

        activity.AddRange(tasks.Select(task => new SynqLienActivityInsight(
            "task",
            $"{task.Title}: {task.Status}",
            task.Description,
            task.CaseId ?? caseId,
            task.LienIds.Count == 0 ? null : task.LienIds[0],
            task.TaskId,
            task.UpdatedAtUtc,
            task.IsOverdue || task.IsHighPriority)));

        activity.AddRange(liens
            .Where(lien => IsWithinWindow(lien.UpdatedAtUtc, fromUtc, toUtc))
            .Select(lien => new SynqLienActivityInsight(
                "lien",
                $"{lien.LienNumber}: {lien.Status}",
                lien.SubjectDisplayName,
                caseId,
                lien.LienId,
                lien.LienId,
                lien.UpdatedAtUtc,
                lien.MissingDocuments || lien.MissingPurchaseDate)));

        return activity
            .Where(item => IsWithinWindow(item.OccurredAtUtc, fromUtc, toUtc))
            .ToList();
    }

    private static SynqLienCaseExport BuildCaseExport(
        CaseResponse item,
        IReadOnlyList<SynqLienLienInsight> liens,
        IReadOnlyList<SynqLienDocumentInsight> documents,
        IReadOnlyList<SynqLienNoteInsight> notes,
        IReadOnlyList<SynqLienServicingInsight> servicing,
        IReadOnlyList<SynqLienTaskInsight> tasks)
    {
        var sheets = new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, object?>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Case"] =
            [
                Row(
                    ("CaseNumber", item.CaseNumber),
                    ("Client", item.ClientDisplayName),
                    ("Status", item.Status),
                    ("LawFirm", item.LawFirm),
                    ("CaseManager", item.CaseManager),
                    ("DateOfLoss", item.DateOfIncident),
                    ("SettlementAmount", item.SettlementAmount),
                    ("DemandAmount", item.DemandAmount))
            ],
            ["Liens"] = liens.Select(lien => Row(
                ("LienNumber", lien.LienNumber),
                ("Status", lien.Status),
                ("LienType", lien.LienType),
                ("Subject", lien.SubjectDisplayName),
                ("Billing", lien.TotalBilling),
                ("Purchase", lien.TotalPurchase),
                ("Reduction", lien.ReductionAmount),
                ("Balance", lien.CurrentBalance),
                ("MissingDocuments", lien.MissingDocuments))).ToList(),
            ["Documents"] = documents.Select(document => Row(
                ("FileName", document.FileName),
                ("Source", document.Source),
                ("LienId", document.LienId),
                ("UploadedAtUtc", document.UploadedAtUtc),
                ("Url", document.Url))).ToList(),
            ["Notes"] = notes.Select(note => Row(
                ("CreatedAtUtc", note.CreatedAtUtc),
                ("CreatedBy", note.CreatedByName),
                ("Category", note.Category),
                ("Pinned", note.IsPinned),
                ("Content", note.Content))).ToList(),
            ["Servicing"] = servicing.Select(item => Row(
                ("TaskNumber", item.TaskNumber),
                ("TaskType", item.TaskType),
                ("Status", item.Status),
                ("Priority", item.Priority),
                ("AssignedTo", item.AssignedTo),
                ("DueDate", item.DueDate),
                ("LienId", item.LienId))).ToList(),
            ["Tasks"] = tasks.Select(task => Row(
                ("Title", task.Title),
                ("Status", task.Status),
                ("Priority", task.Priority),
                ("AssignedUserId", task.AssignedUserId),
                ("DueDateUtc", task.DueDateUtc),
                ("Overdue", task.IsOverdue))).ToList(),
        };

        return new SynqLienCaseExport(
            $"synqlien-case-{item.CaseNumber}-{DateTime.UtcNow:yyyyMMdd}.xlsx",
            sheets);
    }

    private static IReadOnlyDictionary<string, object?> Row(params (string Key, object? Value)[] values)
        => values.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);

    private static SynqLienTaskMetrics BuildTaskMetrics(
        IReadOnlyList<SynqLienTaskInsight> tasks,
        Guid? currentUserId)
        => new(
            tasks.Count,
            tasks.Count(task => task.IsOpen),
            tasks.Count(task => task.IsOverdue),
            tasks.Count(task => task.IsDueToday),
            tasks.Count(task => task.IsHighPriority),
            currentUserId.HasValue ? tasks.Count(task => task.AssignedUserId == currentUserId.Value) : 0,
            tasks.GroupBy(task => task.Status, StringComparer.OrdinalIgnoreCase)
                .Select(group => new SynqLienStatusCount(group.Key, group.Count()))
                .OrderByDescending(group => group.Count)
                .ThenBy(group => group.Status)
                .ToList());

    private static SynqLienServicingMetrics BuildServicingMetrics(IReadOnlyList<SynqLienServicingInsight> items)
        => new(
            items.Count,
            items.Count(item => item.IsActive),
            items.Count(item => item.IsOverdue),
            items.GroupBy(item => item.Status, StringComparer.OrdinalIgnoreCase)
                .Select(group => new SynqLienStatusCount(group.Key, group.Count()))
                .OrderByDescending(group => group.Count)
                .ThenBy(group => group.Status)
                .ToList());

    private static async Task<List<CaseResponse>> SearchAllCasesAsync(
        ICaseService cases,
        Guid tenantId,
        CancellationToken ct)
    {
        const int pageSize = 100;
        var page = 1;
        var items = new List<CaseResponse>();

        while (true)
        {
            var result = await cases.SearchV3Async(
                tenantId,
                keyword: null,
                statusId: null,
                page: page,
                limit: pageSize,
                sortBy: null,
                sortDirection: null,
                lawFirmOrgId: null,
                accidentTypeId: null,
                caseManagerId: null,
                ct: ct);

            if (result.Items.Count == 0)
                break;

            items.AddRange(result.Items);
            if (items.Count >= result.TotalCount)
                break;

            page++;
        }

        return items;
    }

    private static Dictionary<string, string> ParseLegacyNoteFields(string? notes)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(notes))
            return result;

        foreach (var segment in notes.Split("; ", StringSplitOptions.RemoveEmptyEntries))
        {
            var idx = segment.IndexOf('=');
            if (idx <= 0)
                continue;

            var key = segment[..idx].Trim();
            var value = segment[(idx + 1)..].Trim();
            if (!string.IsNullOrWhiteSpace(key))
                result[key] = value;
        }

        return result;
    }

    private static string? GetLegacyDocumentUrl(Dictionary<string, string> fields)
    {
        var url = fields.GetValueOrDefault("url");
        return string.IsNullOrWhiteSpace(url)
            ? NullIfWhiteSpace(fields.GetValueOrDefault("documentUrl"))
            : url;
    }

    private static bool IsMedicalRecordDocument(SynqLienDocumentInsight document)
        => ContainsAny(document.FileName, "medical", "record", "bill", "invoice") ||
           ContainsAny(document.DocumentTypeId, "medical", "record", "bill", "invoice");

    private static bool IsImportantText(string? value)
        => ContainsAny(value, "important", "urgent", "critical", "deadline", "overdue", "settlement", "reduction", "escalat");

    private static bool ContainsAny(string? value, params string[] tokens)
        => !string.IsNullOrWhiteSpace(value) &&
           tokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase));

    private static decimal ParseDecimal(string? value)
        => decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0m;

    private static decimal ComputeReductionAmount(decimal? totalBilling, decimal? totalPurchase)
        => totalBilling.HasValue && totalPurchase.HasValue
            ? Math.Max(totalBilling.Value - totalPurchase.Value, 0m)
            : 0m;

    private static bool ParseBooleanish(string? value)
        => !string.IsNullOrWhiteSpace(value) &&
           (value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("y", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("1", StringComparison.OrdinalIgnoreCase));

    private static string? FormatDate(DateOnly? value)
        => value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static bool? IsMinor(DateOnly? dateOfBirth)
    {
        if (!dateOfBirth.HasValue)
            return null;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var age = today.Year - dateOfBirth.Value.Year;
        if (dateOfBirth.Value > today.AddYears(-age))
            age--;

        return age < 18;
    }

    private static string? BuildClientAddress(CaseResponse item)
    {
        if (!string.IsNullOrWhiteSpace(item.ClientAddress))
            return item.ClientAddress.Trim();

        var parts = new[]
            {
                item.ClientStreetAddress,
                item.ClientCity,
                item.ClientState,
                item.ClientZipcode,
            }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .ToList();

        return parts.Count == 0 ? null : string.Join(", ", parts);
    }

    private static bool IsRejectedLienStatus(string status)
        => status.Equals("Rejected", StringComparison.OrdinalIgnoreCase) ||
           status.Equals("Declined", StringComparison.OrdinalIgnoreCase);

    private static bool IsTerminalTaskStatus(string status)
        => status.Equals("COMPLETED", StringComparison.OrdinalIgnoreCase) ||
           status.Equals("CANCELLED", StringComparison.OrdinalIgnoreCase) ||
           status.Equals("CANCELED", StringComparison.OrdinalIgnoreCase);

    private static bool IsHighPriority(string? priority)
        => priority is not null &&
           (priority.Equals("HIGH", StringComparison.OrdinalIgnoreCase) ||
            priority.Equals("URGENT", StringComparison.OrdinalIgnoreCase) ||
            priority.Equals("High", StringComparison.OrdinalIgnoreCase) ||
            priority.Equals("Urgent", StringComparison.OrdinalIgnoreCase));

    private static DateTime? ToDateTime(DateOnly? date)
        => date?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

    private static bool IsDateWithinWindow(DateTime? value, DateTime? fromUtc, DateTime? toUtc)
        => value.HasValue
            ? IsWithinWindow(value.Value, fromUtc, toUtc)
            : !fromUtc.HasValue && !toUtc.HasValue;

    private static bool IsWithinWindow(DateTime value, DateTime? fromUtc, DateTime? toUtc)
    {
        var utc = value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
        if (fromUtc.HasValue && utc < fromUtc.Value)
            return false;

        if (toUtc.HasValue && utc > toUtc.Value)
            return false;

        return true;
    }

    private static bool MatchesText(string? value, string? filter)
        => string.IsNullOrWhiteSpace(filter) ||
           (!string.IsNullOrWhiteSpace(value) &&
            value.Contains(filter.Trim(), StringComparison.OrdinalIgnoreCase));

    private static bool MatchesState(string? value, string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
            return true;

        if (string.IsNullOrWhiteSpace(value))
            return false;

        var normalizedFilter = NormalizeState(filter);
        var normalizedValue = NormalizeState(value);
        return normalizedValue.Equals(normalizedFilter, StringComparison.OrdinalIgnoreCase) ||
               value.Contains(filter.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeState(string value)
    {
        var normalized = value.Trim();
        return normalized.Equals("Alabama", StringComparison.OrdinalIgnoreCase) ? "AL" :
            normalized.Equals("Alaska", StringComparison.OrdinalIgnoreCase) ? "AK" :
            normalized.Equals("Arizona", StringComparison.OrdinalIgnoreCase) ? "AZ" :
            normalized.Equals("Arkansas", StringComparison.OrdinalIgnoreCase) ? "AR" :
            normalized.Equals("California", StringComparison.OrdinalIgnoreCase) ? "CA" :
            normalized.Equals("Florida", StringComparison.OrdinalIgnoreCase) ? "FL" :
            normalized.Equals("Georgia", StringComparison.OrdinalIgnoreCase) ? "GA" :
            normalized.Equals("New York", StringComparison.OrdinalIgnoreCase) ? "NY" :
            normalized.Equals("Texas", StringComparison.OrdinalIgnoreCase) ? "TX" :
            normalized.ToUpperInvariant();
    }

    private static bool MatchesCaseSearch(CaseResponse item, string? search)
        => string.IsNullOrWhiteSpace(search) ||
           item.CaseNumber.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase) ||
           item.ClientDisplayName.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase) ||
           (!string.IsNullOrWhiteSpace(item.Title) && item.Title.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase)) ||
           (!string.IsNullOrWhiteSpace(item.ExternalReference) && item.ExternalReference.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase));

    private static string? NormalizeTaskStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Trim()
            .Replace("-", "_", StringComparison.OrdinalIgnoreCase)
            .Replace(" ", "_", StringComparison.OrdinalIgnoreCase)
            .ToUpperInvariant();

        return normalized switch
        {
            "NEW" => "OPEN",
            "OPEN" => "OPEN",
            "IN_PROGRESS" or "INPROGRESS" => "IN_PROGRESS",
            "WAITING" or "BLOCKED" or "WAITING_BLOCKED" => "WAITING_BLOCKED",
            "DONE" or "COMPLETE" or "COMPLETED" => "COMPLETED",
            "CANCELLED" or "CANCELED" => "CANCELLED",
            _ => normalized,
        };
    }

    private static string? NormalizeTaskStatusGroup(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Trim().Replace("-", "_", StringComparison.OrdinalIgnoreCase).Replace(" ", "_", StringComparison.OrdinalIgnoreCase).ToLowerInvariant();
        return normalized switch
        {
            "open" or "active" => "open",
            "closed" or "complete" or "completed" => "closed",
            _ => null,
        };
    }

    private static IReadOnlyList<string> ResolveTaskStatusValues(string? status, string? statusGroup)
    {
        if (!string.IsNullOrWhiteSpace(status))
            return [status];

        return statusGroup switch
        {
            "open" => ["OPEN", "NEW", "IN_PROGRESS", "WAITING_BLOCKED"],
            "closed" => ["COMPLETED", "CANCELLED"],
            _ => [],
        };
    }

    private static string? NormalizeTaskPriority(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Trim().ToUpperInvariant();
        return normalized switch
        {
            "NORMAL" => "MEDIUM",
            "MED" => "MEDIUM",
            _ => normalized,
        };
    }

    private static string? NormalizeAssignmentScope(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Trim().ToLowerInvariant();
        return normalized switch
        {
            "me" or "mine" or "my" => "me",
            "unassigned" => "unassigned",
            "others" => "others",
            _ => null,
        };
    }

    private static string? NormalizeServicingStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Trim().Replace("-", string.Empty, StringComparison.OrdinalIgnoreCase).Replace("_", string.Empty, StringComparison.OrdinalIgnoreCase).Replace(" ", string.Empty, StringComparison.OrdinalIgnoreCase).ToUpperInvariant();
        return normalized switch
        {
            "PENDING" or "OPEN" => ServicingStatus.Pending,
            "INPROGRESS" => ServicingStatus.InProgress,
            "COMPLETED" or "DONE" => ServicingStatus.Completed,
            "ESCALATED" => ServicingStatus.Escalated,
            "ONHOLD" => ServicingStatus.OnHold,
            _ => value.Trim(),
        };
    }

    private static string? NormalizeServicingStatusGroup(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Trim().ToLowerInvariant();
        return normalized switch
        {
            "open" or "active" => "open",
            "closed" or "completed" => "closed",
            _ => null,
        };
    }

    private static IReadOnlyList<string> ResolveServicingStatusValues(string? status, string? statusGroup)
    {
        if (!string.IsNullOrWhiteSpace(status))
            return [status];

        return statusGroup switch
        {
            "open" => [ServicingStatus.Pending, ServicingStatus.InProgress, ServicingStatus.Escalated, ServicingStatus.OnHold],
            "closed" => [ServicingStatus.Completed],
            _ => [],
        };
    }

    private static string? NormalizeServicingPriority(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Trim();
        return normalized.ToUpperInvariant() switch
        {
            "LOW" => ServicingPriority.Low,
            "NORMAL" or "MEDIUM" => ServicingPriority.Normal,
            "HIGH" => ServicingPriority.High,
            "URGENT" => ServicingPriority.Urgent,
            _ => normalized,
        };
    }

    private static string? NormalizeCaseStatusGroup(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Trim().ToLowerInvariant();
        return normalized switch
        {
            "open" or "active" => "open",
            "closed" => "closed",
            _ => null,
        };
    }

    private static IReadOnlyList<string> ResolveCaseStatusValues(string? status, string? statusGroup)
    {
        if (!string.IsNullOrWhiteSpace(status))
            return [status];

        return statusGroup switch
        {
            "open" => [CaseStatus.PreDemand, CaseStatus.DemandSent, CaseStatus.InNegotiation, CaseStatus.CaseSettled],
            "closed" => [CaseStatus.Closed],
            _ => [],
        };
    }

    private static IReadOnlyList<string> ResolveStatusFilterValues(string? status, string? statusGroup)
    {
        if (!string.IsNullOrWhiteSpace(status))
            return [status];

        return statusGroup switch
        {
            "draft" => DraftStatusGroup,
            "open" => LienStatus.Open.ToArray(),
            "closed" => LienStatus.Terminal.ToArray(),
            "marketplace" => MarketplaceStatusGroup,
            "servicing" => ServicingStatusGroup,
            _ => [],
        };
    }

    private static (DateTime? FromUtc, DateTime? ToUtc) ResolveCreatedWindow(AssistantLienQueueSummaryParams p)
    {
        var (createdFromUtc, createdToUtc) = ResolveDateWindow(p.DatePreset, p.CreatedFrom, p.CreatedTo);

        if (createdFromUtc.HasValue || createdToUtc.HasValue)
            return (createdFromUtc, createdToUtc);

        if (p.Days is not > 0)
            return (null, null);

        var windowToUtc = DateTime.UtcNow;
        return (windowToUtc.AddDays(-Math.Clamp(p.Days.Value, 1, 365)), windowToUtc);
    }

    private static (DateTime? FromUtc, DateTime? ToUtc) NormalizeDateWindow(DateTime? fromUtc, DateTime? toUtc)
    {
        var from = fromUtc?.ToUniversalTime();
        var to = toUtc?.ToUniversalTime();
        if (from.HasValue && to.HasValue && from > to)
            (from, to) = (to, from);

        return (from, to);
    }

    private static (DateTime? FromUtc, DateTime? ToUtc) ResolveDateWindow(
        string? preset,
        DateTime? fromUtc,
        DateTime? toUtc)
    {
        var explicitWindow = NormalizeDateWindow(fromUtc, toUtc);
        if (explicitWindow.FromUtc.HasValue || explicitWindow.ToUtc.HasValue)
            return explicitWindow;

        var now = DateTime.UtcNow;
        var today = now.Date;
        var normalized = NormalizeDatePreset(preset);

        return normalized switch
        {
            "today" => (today, today.AddDays(1).AddTicks(-1)),
            "yesterday" => (today.AddDays(-1), today.AddTicks(-1)),
            "this_week" => (StartOfWeek(today), now),
            "last_week" => (StartOfWeek(today).AddDays(-7), StartOfWeek(today).AddTicks(-1)),
            "this_month" => (new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc), now),
            "last_month" => LastMonthWindow(today),
            "last_30_days" => (now.AddDays(-30), now),
            "last_60_days" => (now.AddDays(-60), now),
            "last_90_days" => (now.AddDays(-90), now),
            "life_to_date" => (null, null),
            _ => (null, null),
        };
    }

    private static string? NormalizeDatePreset(string? preset)
    {
        if (string.IsNullOrWhiteSpace(preset))
            return null;

        var normalized = preset.Trim()
            .Replace("-", "_", StringComparison.Ordinal)
            .Replace(" ", "_", StringComparison.Ordinal)
            .ToLowerInvariant();

        return normalized switch
        {
            "lifetime" or "life_to_date" or "all_time" or "all" => "life_to_date",
            "current_week" => "this_week",
            "current_month" => "this_month",
            "past_30_days" or "30_days" => "last_30_days",
            "past_60_days" or "60_days" => "last_60_days",
            "past_90_days" or "90_days" => "last_90_days",
            _ => normalized,
        };
    }

    private static DateTime StartOfWeek(DateTime utcDate)
    {
        var offset = ((int)utcDate.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return utcDate.AddDays(-offset);
    }

    private static (DateTime FromUtc, DateTime ToUtc) LastMonthWindow(DateTime today)
    {
        var thisMonth = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var lastMonth = thisMonth.AddMonths(-1);
        return (lastMonth, thisMonth.AddTicks(-1));
    }

    private static SynqLienQueueSummaryOutcome EmptyQueueSummary()
        => new(true, "completed", null, 0, 0, 0, 0, 0, 0, null, null, null, null, [], []);

    private static string BuildSubjectDisplayName(LienResponse lien)
    {
        if (lien.IsConfidential)
            return "Confidential subject";

        if (!string.IsNullOrWhiteSpace(lien.SubjectDisplayName))
            return lien.SubjectDisplayName.Trim();

        var combined = string.Join(' ', new[] { lien.SubjectFirstName, lien.SubjectLastName }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim()));
        return string.IsNullOrWhiteSpace(combined) ? "Unnamed subject" : combined;
    }

    private static string? NormalizeLienStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Trim()
            .Replace("-", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("_", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(" ", string.Empty, StringComparison.OrdinalIgnoreCase)
            .ToUpperInvariant();

        return normalized switch
        {
            "DRAFT" => LienStatus.Draft,
            "OFFERED" => LienStatus.Offered,
            "UNDERREVIEW" => LienStatus.UnderReview,
            "SOLD" => LienStatus.Sold,
            "ACTIVE" => LienStatus.Active,
            "SETTLED" => LienStatus.Settled,
            "WITHDRAWN" => LienStatus.Withdrawn,
            "CANCELLED" or "CANCELED" => LienStatus.Cancelled,
            "DISPUTED" => LienStatus.Disputed,
            _ => value.Trim(),
        };
    }

    private static string? NormalizeCaseStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Trim()
            .Replace("-", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("_", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(" ", string.Empty, StringComparison.OrdinalIgnoreCase)
            .ToUpperInvariant();

        return normalized switch
        {
            "PREDEMAND" => CaseStatus.PreDemand,
            "DEMANDSENT" => CaseStatus.DemandSent,
            "INNEGOTIATION" => CaseStatus.InNegotiation,
            "CASESETTLED" or "SETTLED" => CaseStatus.CaseSettled,
            "CLOSED" => CaseStatus.Closed,
            _ => value.Trim(),
        };
    }

    private static string? NormalizeLienStatusGroup(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Trim()
            .Replace("-", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("_", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(" ", string.Empty, StringComparison.OrdinalIgnoreCase)
            .ToUpperInvariant();

        return normalized switch
        {
            "DRAFT" or "NEW" or "INTAKE" => "draft",
            "OPEN" or "ACTIVE" => "open",
            "CLOSED" or "TERMINAL" or "RESOLVED" => "closed",
            "MARKETPLACE" or "SALE" or "SELLING" => "marketplace",
            "SERVICING" or "SERVICE" => "servicing",
            _ => null,
        };
    }

    private static string? NormalizeLienType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Trim()
            .Replace("-", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("_", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(" ", string.Empty, StringComparison.OrdinalIgnoreCase)
            .ToUpperInvariant();

        return normalized switch
        {
            "MEDICAL" or "MEDICALLIEN" => LienType.MedicalLien,
            "ATTORNEY" or "ATTORNEYLIEN" => LienType.AttorneyLien,
            "SETTLEMENTADVANCE" or "ADVANCE" => LienType.SettlementAdvance,
            "WORKERSCOMP" or "WORKERSCOMPLIEN" => LienType.WorkersCompLien,
            "PROPERTY" or "PROPERTYLIEN" => LienType.PropertyLien,
            "OTHER" => LienType.Other,
            _ => value.Trim(),
        };
    }

    private static string? NullIfWhiteSpace(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private readonly record struct LienVisibilityScope(
        bool CanReadAnyLien,
        Guid? OrgId,
        bool IncludeSellerOrg,
        bool IncludeBuyerOrg,
        bool IncludeHolderOrg,
        bool IncludeMarketplace)
    {
        public static LienVisibilityScope All { get; } = new(
            CanReadAnyLien: true,
            OrgId: null,
            IncludeSellerOrg: false,
            IncludeBuyerOrg: false,
            IncludeHolderOrg: false,
            IncludeMarketplace: false);

        public static LienVisibilityScope None { get; } = new(
            CanReadAnyLien: false,
            OrgId: Guid.Empty,
            IncludeSellerOrg: false,
            IncludeBuyerOrg: false,
            IncludeHolderOrg: false,
            IncludeMarketplace: false);
    }
}

internal sealed class AssistantLienSearchParams
{
    public string? Search { get; init; }
    public string? SubjectName { get; init; }
    public string? CaseNumber { get; init; }
    public string? Status { get; init; }
    public string? StatusGroup { get; init; }
    public string? LienType { get; init; }
    public DateTime? CreatedFrom { get; init; }
    public DateTime? CreatedTo { get; init; }
    public string? DatePreset { get; init; }
    public int? Top { get; init; }
}

internal sealed class AssistantLienQueueSummaryParams
{
    public string? Search { get; init; }
    public string? SubjectName { get; init; }
    public string? CaseNumber { get; init; }
    public string? Status { get; init; }
    public string? StatusGroup { get; init; }
    public string? LienType { get; init; }
    public int? Days { get; init; }
    public DateTime? CreatedFrom { get; init; }
    public DateTime? CreatedTo { get; init; }
    public string? DatePreset { get; init; }
    public int? RecentTop { get; init; }
}

internal sealed class AssistantCaseSearchParams
{
    public string? Search { get; init; }
    public string? ClientName { get; init; }
    public string? CaseNumber { get; init; }
    public string? Status { get; init; }
    public string? LawFirm { get; init; }
    public string? CaseManager { get; init; }
    public string? CaseType { get; init; }
    public string? AccidentType { get; init; }
    public string? State { get; init; }
    public DateTime? OpenedFrom { get; init; }
    public DateTime? OpenedTo { get; init; }
    public string? DatePreset { get; init; }
    public int? Top { get; init; }
}

internal sealed class AssistantCaseLookupParams
{
    public int? LiensTop { get; init; }
}

internal sealed class AssistantCaseInsightsParams
{
    public string? DatePreset { get; init; }
    public DateTime? DateFrom { get; init; }
    public DateTime? DateTo { get; init; }
    public int? Top { get; init; }
    public bool IncludeExport { get; init; }
}

internal sealed class AssistantTaskSearchParams
{
    public string? Search { get; init; }
    public string? Status { get; init; }
    public string? StatusGroup { get; init; }
    public string? Priority { get; init; }
    public Guid? AssignedUserId { get; init; }
    public string? AssignmentScope { get; init; }
    public Guid? CaseId { get; init; }
    public Guid? LienId { get; init; }
    public DateTime? DueFrom { get; init; }
    public DateTime? DueTo { get; init; }
    public string? DatePreset { get; init; }
    public bool? Overdue { get; init; }
    public bool? DueToday { get; init; }
    public int? Top { get; init; }
}

internal sealed class AssistantServicingSearchParams
{
    public string? Search { get; init; }
    public string? Status { get; init; }
    public string? StatusGroup { get; init; }
    public string? Priority { get; init; }
    public string? AssignedTo { get; init; }
    public Guid? CaseId { get; init; }
    public Guid? LienId { get; init; }
    public DateTime? DueFrom { get; init; }
    public DateTime? DueTo { get; init; }
    public string? DatePreset { get; init; }
    public bool? Overdue { get; init; }
    public int? Top { get; init; }
}

internal sealed class AssistantReportSummaryParams
{
    public string? Search { get; init; }
    public string? CaseStatus { get; init; }
    public string? CaseStatusGroup { get; init; }
    public string? LienStatus { get; init; }
    public string? LienStatusGroup { get; init; }
    public string? LawFirm { get; init; }
    public string? CaseManager { get; init; }
    public string? CaseType { get; init; }
    public string? AccidentType { get; init; }
    public string? State { get; init; }
    public DateTime? DateFrom { get; init; }
    public DateTime? DateTo { get; init; }
    public string? DatePreset { get; init; }
    public int? Top { get; init; }
}
