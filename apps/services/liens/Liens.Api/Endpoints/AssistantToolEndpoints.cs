using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using BuildingBlocks.Context;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Domain;
using Liens.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

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

            var (createdFromUtc, createdToUtc) = NormalizeDateWindow(p.CreatedFrom, p.CreatedTo);
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

            return Results.Ok(new SynqLienCaseSearchOutcome(
                true,
                "completed",
                null,
                result.TotalCount,
                result.Items.Select(ToCaseSearchResult).ToList()));
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
            lien.UpdatedAtUtc);
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
            linkedLiens.Items.Select(lien => ToLienSearchResult(lien, item.CaseNumber)).ToList());
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
            lien.UpdatedAtUtc);

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
            item.UpdatedAtUtc);

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
        var createdFromUtc = p.CreatedFrom?.ToUniversalTime();
        var createdToUtc = p.CreatedTo?.ToUniversalTime();

        if (createdFromUtc.HasValue || createdToUtc.HasValue)
        {
            if (createdFromUtc.HasValue && createdToUtc.HasValue && createdFromUtc > createdToUtc)
                (createdFromUtc, createdToUtc) = (createdToUtc, createdFromUtc);

            return (createdFromUtc, createdToUtc);
        }

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
    public int? RecentTop { get; init; }
}

internal sealed class AssistantCaseSearchParams
{
    public string? Search { get; init; }
    public string? ClientName { get; init; }
    public string? CaseNumber { get; init; }
    public string? Status { get; init; }
    public int? Top { get; init; }
}

internal sealed class AssistantCaseLookupParams
{
    public int? LiensTop { get; init; }
}
