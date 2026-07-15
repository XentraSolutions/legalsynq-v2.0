using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using BuildingBlocks.Context;
using CareConnect.Application.Authorization;
using CareConnect.Application.DTOs;
using CareConnect.Application.Interfaces;
using CareConnect.Domain;
using Microsoft.AspNetCore.Mvc;

namespace CareConnect.Api.Endpoints;

public static class AssistantToolEndpoints
{
    private static readonly string[] QueueStatuses = Referral.ValidStatuses.All.ToArray();
    private static readonly string[] NewStatusGroup =
    [
        Referral.ValidStatuses.New,
        Referral.ValidStatuses.NewOpened,
    ];
    private static readonly string[] OpenStatusGroup =
    [
        Referral.ValidStatuses.New,
        Referral.ValidStatuses.NewOpened,
        Referral.ValidStatuses.Accepted,
        Referral.ValidStatuses.InProgress,
    ];
    private static readonly string[] ClosedStatusGroup =
    [
        Referral.ValidStatuses.Completed,
        Referral.ValidStatuses.Declined,
        Referral.ValidStatuses.Cancelled,
    ];

    public static void MapAssistantToolEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/assistant-tools")
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(ProductCodes.SynqCareConnect);

        group.MapGet("/referrals/search", async (
            [AsParameters] AssistantReferralSearchParams p,
            IReferralService referrals,
            ICurrentRequestContext ctx,
            AuthorizationService authSvc,
            CancellationToken ct) =>
        {
            if (!await CanReadReferralsAsync(ctx, authSvc, ct))
                return Results.Forbid();

            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            var query = BuildScopedReferralQuery(ctx, p);
            var result = await referrals.SearchAsync(tenantId, query, ct);

            return Results.Ok(new CareConnectReferralSearchOutcome(
                true,
                "completed",
                null,
                result.TotalCount,
                result.Items.Select(ToReferralSearchResult).ToList()));
        });

        group.MapGet("/referrals/queue-summary", async (
            [AsParameters] AssistantReferralQueueSummaryParams p,
            IReferralService referrals,
            ICurrentRequestContext ctx,
            AuthorizationService authSvc,
            CancellationToken ct) =>
        {
            if (!await CanReadReferralsAsync(ctx, authSvc, ct))
                return Results.Forbid();

            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            var baseQuery = BuildScopedReferralQuery(ctx, p);
            var (windowFromUtc, windowToUtc) = ResolveCreatedWindow(p);
            var windowQuery = ApplyCreatedWindow(
                CloneReferralQuery(baseQuery, status: null, pageSize: 1),
                windowFromUtc,
                windowToUtc);
            var recentTop = Math.Clamp(p.RecentTop ?? 5, 1, 10);

            // Keep these queries sequential: the referral service shares one scoped EF DbContext,
            // and parallel SearchAsync calls on the same scope cause runtime failures.
            var counts = new List<CareConnectReferralQueueStatusCount>(QueueStatuses.Length);
            foreach (var status in QueueStatuses)
            {
                counts.Add(await CountStatusAsync(referrals, tenantId, windowQuery, status, ct));
            }

            var totalVisible = await referrals.SearchAsync(
                tenantId,
                CloneReferralQuery(baseQuery, status: null, pageSize: 1),
                ct);
            var recent = await SearchRecentSummaryItemsAsync(
                referrals,
                tenantId,
                windowQuery,
                p.Status,
                p.StatusGroup,
                recentTop,
                ct);

            counts = counts
                .Where(item => item.Count > 0)
                .OrderByDescending(item => item.Count)
                .ThenBy(item => item.Status, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var matchingStatuses = ResolveStatusFilterValues(p.Status, p.StatusGroup);
            var windowReferralCount = counts.Sum(item => item.Count);
            var matchingReferralCount = matchingStatuses.Count == 0
                ? windowReferralCount
                : counts
                    .Where(item => matchingStatuses.Contains(item.Status, StringComparer.OrdinalIgnoreCase))
                    .Sum(item => item.Count);
            var newReferralCount = counts
                .Where(item => NewStatusGroup.Contains(item.Status, StringComparer.OrdinalIgnoreCase))
                .Sum(item => item.Count);
            var openReferralCount = counts
                .Where(item => OpenStatusGroup.Contains(item.Status, StringComparer.OrdinalIgnoreCase))
                .Sum(item => item.Count);
            var closedReferralCount = counts
                .Where(item => ClosedStatusGroup.Contains(item.Status, StringComparer.OrdinalIgnoreCase))
                .Sum(item => item.Count);

            return Results.Ok(new CareConnectReferralQueueSummaryOutcome(
                true,
                "completed",
                null,
                totalVisible.TotalCount,
                windowReferralCount,
                matchingReferralCount,
                newReferralCount,
                openReferralCount,
                closedReferralCount,
                windowFromUtc,
                windowToUtc,
                NormalizeSummaryStatus(p.Status),
                NormalizeSummaryStatusGroup(p.StatusGroup),
                counts,
                recent.Select(ToReferralSearchResult).ToList()));
        });

        group.MapGet("/referrals/{id:guid}", async (
            Guid id,
            [AsParameters] AssistantReferralLookupParams p,
            IReferralService referrals,
            ICurrentRequestContext ctx,
            AuthorizationService authSvc,
            CancellationToken ct) =>
        {
            if (!await CanReadReferralsAsync(ctx, authSvc, ct))
                return Results.Forbid();

            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            var useGlobalLookup = ShouldUseGlobalReferralLookup(ctx);
            var referral = await referrals.GetByIdAsync(tenantId, id, ct, isPlatformAdmin: useGlobalLookup);

            if (!CanAccessReferral(referral, ctx))
                return Results.NotFound();

            var history = await referrals.GetHistoryAsync(tenantId, id, ct, isPlatformAdmin: useGlobalLookup);
            var historyTop = Math.Clamp(p.HistoryTop ?? 8, 1, 50);

            return Results.Ok(new CareConnectReferralLookupOutcome(
                true,
                "completed",
                null,
                new CareConnectReferralLookupResult(
                    referral.Id,
                    referral.Status,
                    referral.Urgency,
                    referral.ProviderName,
                    BuildClientDisplayName(referral.ClientFirstName, referral.ClientLastName),
                    NullIfWhiteSpace(referral.RequestedService),
                    NullIfWhiteSpace(referral.TreatmentTypeName),
                    NullIfWhiteSpace(referral.ReferringOrganizationName),
                    NullIfWhiteSpace(referral.ReferrerName),
                    referral.CreatedAtUtc,
                    referral.UpdatedAtUtc,
                    history.OrderByDescending(item => item.ChangedAtUtc)
                        .Take(historyTop)
                        .Select(ToHistoryItem)
                        .ToList())));
        });

        group.MapGet("/referrals/{id:guid}/history", async (
            Guid id,
            [AsParameters] AssistantReferralHistoryParams p,
            IReferralService referrals,
            ICurrentRequestContext ctx,
            AuthorizationService authSvc,
            CancellationToken ct) =>
        {
            if (!await CanReadReferralsAsync(ctx, authSvc, ct))
                return Results.Forbid();

            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            var useGlobalLookup = ShouldUseGlobalReferralLookup(ctx);
            var referral = await referrals.GetByIdAsync(tenantId, id, ct, isPlatformAdmin: useGlobalLookup);

            if (!CanAccessReferral(referral, ctx))
                return Results.NotFound();

            var top = Math.Clamp(p.Top ?? 10, 1, 50);
            var history = await referrals.GetHistoryAsync(tenantId, id, ct, isPlatformAdmin: useGlobalLookup);

            return Results.Ok(new CareConnectReferralHistoryLookupOutcome(
                true,
                "completed",
                null,
                new CareConnectReferralHistoryLookupResult(
                    referral.Id,
                    BuildClientDisplayName(referral.ClientFirstName, referral.ClientLastName),
                    referral.ProviderName,
                    referral.Status,
                    history.OrderByDescending(item => item.ChangedAtUtc)
                        .Take(top)
                        .Select(ToHistoryItem)
                        .ToList())));
        });

        group.MapGet("/providers/search", async (
            [AsParameters] AssistantProviderSearchParams p,
            IProviderService providers,
            ICurrentRequestContext ctx,
            AuthorizationService authSvc,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            await CareConnectAuthHelper.RequireAsync(ctx, authSvc, PermissionCodes.ProviderSearch, ct);

            var result = await providers.SearchAsync(tenantId, new GetProvidersQuery
            {
                Name = p.Name,
                City = p.City,
                State = p.State,
                AcceptingReferrals = p.AcceptingReferrals,
                IsActive = true,
                Page = 1,
                PageSize = Math.Clamp(p.Top ?? 8, 1, 25),
            }, ct);

            return Results.Ok(new CareConnectProviderSearchOutcome(
                true,
                "completed",
                null,
                result.TotalCount,
                result.Items.Select(ToProviderSearchResult).ToList()));
        });

        group.MapGet("/referrers/search", async (
            [AsParameters] AssistantReferrerSearchParams p,
            IReferralService referrals,
            ICurrentRequestContext ctx,
            AuthorizationService authSvc,
            CancellationToken ct) =>
        {
            if (!await CanReadReferralsAsync(ctx, authSvc, ct))
                return Results.Forbid();

            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            var top = Math.Clamp(p.Top ?? 8, 1, 15);
            var searchQuery = BuildScopedReferralQuery(ctx, p, Math.Clamp(Math.Max(top * 5, 25), 1, 100));
            var result = await referrals.SearchAsync(tenantId, searchQuery, ct);

            var grouped = result.Items
                .Select(item => new
                {
                    Item = item,
                    Name = string.IsNullOrWhiteSpace(item.ReferrerName) ? "Unknown Referrer" : item.ReferrerName.Trim(),
                    Email = NullIfWhiteSpace(item.ReferrerEmail),
                })
                .GroupBy(item => $"{item.Name}\u001f{item.Email}".ToUpperInvariant())
                .Select(group => new CareConnectReferrerSearchResult(
                    group.First().Name,
                    group.First().Email,
                    group.Count(),
                    group.Count(item => IsOpenStatus(item.Item.Status)),
                    group.Max(item => item.Item.UpdatedAtUtc)))
                .OrderByDescending(item => item.ReferralCount)
                .ThenByDescending(item => item.LastReferralAtUtc)
                .Take(top)
                .ToList();

            return Results.Ok(new CareConnectReferrerSearchOutcome(
                true,
                "completed",
                null,
                grouped.Count,
                grouped));
        });
    }

    private static async Task<bool> CanReadReferralsAsync(
        ICurrentRequestContext ctx,
        AuthorizationService authSvc,
        CancellationToken ct)
        => await CareConnectAuthHelper.HasAnyAsync(
            ctx,
            authSvc,
            [PermissionCodes.ReferralReadOwn, PermissionCodes.ReferralReadAddressed],
            ct);

    private static GetReferralsQuery BuildScopedReferralQuery(
        ICurrentRequestContext ctx,
        AssistantReferralSearchParams paramsModel,
        int? explicitPageSize = null)
    {
        var query = new GetReferralsQuery
        {
            Status = paramsModel.Status,
            SearchText = paramsModel.Search,
            ClientName = paramsModel.ClientName,
            CaseNumber = paramsModel.CaseNumber,
            ProviderId = paramsModel.ProviderId,
            ProviderName = paramsModel.ProviderName,
            ReferrerName = paramsModel.ReferrerName,
            CreatedFrom = paramsModel.CreatedFrom,
            CreatedTo = paramsModel.CreatedTo,
            Page = 1,
            PageSize = Math.Clamp(explicitPageSize ?? paramsModel.Top ?? 8, 1, 100),
        };

        ApplyReferralParticipantScope(query, ctx);
        return query;
    }

    private static GetReferralsQuery BuildScopedReferralQuery(
        ICurrentRequestContext ctx,
        AssistantReferralQueueSummaryParams paramsModel)
    {
        var query = new GetReferralsQuery
        {
            SearchText = paramsModel.Search,
            ProviderName = paramsModel.ProviderName,
            ReferrerName = paramsModel.ReferrerName,
            Page = 1,
            PageSize = 10,
        };

        ApplyReferralParticipantScope(query, ctx);
        return query;
    }

    private static GetReferralsQuery BuildScopedReferralQuery(
        ICurrentRequestContext ctx,
        AssistantReferrerSearchParams paramsModel,
        int pageSize)
    {
        var query = new GetReferralsQuery
        {
            Status = paramsModel.Status,
            SearchText = paramsModel.Search,
            ReferrerName = paramsModel.ReferrerName,
            Page = 1,
            PageSize = pageSize,
        };

        ApplyReferralParticipantScope(query, ctx);
        return query;
    }

    private static void ApplyReferralParticipantScope(GetReferralsQuery query, ICurrentRequestContext ctx)
        => CareConnectParticipantHelper.ApplyAssistantReferralScope(query, ctx);

    private static GetReferralsQuery CloneReferralQuery(
        GetReferralsQuery source,
        string? status,
        int pageSize)
        => new()
        {
            Status = status,
            ProviderId = source.ProviderId,
            SearchText = source.SearchText,
            ClientName = source.ClientName,
            CaseNumber = source.CaseNumber,
            ProviderName = source.ProviderName,
            ReferrerName = source.ReferrerName,
            Urgency = source.Urgency,
            CreatedFrom = source.CreatedFrom,
            CreatedTo = source.CreatedTo,
            Page = 1,
            PageSize = pageSize,
            ReferringOrgId = source.ReferringOrgId,
            ReceivingOrgId = source.ReceivingOrgId,
            ReferrerEmail = source.ReferrerEmail,
            CrossTenantReceiver = source.CrossTenantReceiver,
            CrossTenantReferrer = source.CrossTenantReferrer,
            TenantIds = source.TenantIds,
        };

    private static GetReferralsQuery ApplyCreatedWindow(
        GetReferralsQuery query,
        DateTime? createdFromUtc,
        DateTime? createdToUtc)
    {
        query.CreatedFrom = createdFromUtc;
        query.CreatedTo = createdToUtc;
        return query;
    }

    private static (DateTime? FromUtc, DateTime? ToUtc) ResolveCreatedWindow(
        AssistantReferralQueueSummaryParams paramsModel)
    {
        var createdFromUtc = paramsModel.CreatedFrom?.ToUniversalTime();
        var createdToUtc = paramsModel.CreatedTo?.ToUniversalTime();

        if (createdFromUtc.HasValue || createdToUtc.HasValue)
        {
            if (createdFromUtc.HasValue && createdToUtc.HasValue && createdFromUtc > createdToUtc)
                (createdFromUtc, createdToUtc) = (createdToUtc, createdFromUtc);

            return (createdFromUtc, createdToUtc);
        }

        if (paramsModel.Days is not > 0)
            return (null, null);

        var windowToUtc = DateTime.UtcNow;
        var clampedDays = Math.Clamp(paramsModel.Days.Value, 1, 365);
        return (windowToUtc.AddDays(-clampedDays), windowToUtc);
    }

    private static IReadOnlyList<string> ResolveStatusFilterValues(string? status, string? statusGroup)
    {
        var normalizedStatus = NormalizeSummaryStatus(status);
        if (!string.IsNullOrWhiteSpace(normalizedStatus))
            return [normalizedStatus];

        var normalizedGroup = NormalizeSummaryStatusGroup(statusGroup);
        return normalizedGroup switch
        {
            "new" => NewStatusGroup,
            "open" => OpenStatusGroup,
            "closed" => ClosedStatusGroup,
            _ => [],
        };
    }

    private static async Task<List<ReferralResponse>> SearchRecentSummaryItemsAsync(
        IReferralService referrals,
        Guid tenantId,
        GetReferralsQuery windowQuery,
        string? status,
        string? statusGroup,
        int recentTop,
        CancellationToken ct)
    {
        var matchingStatuses = ResolveStatusFilterValues(status, statusGroup);
        if (matchingStatuses.Count == 0)
        {
            var result = await referrals.SearchAsync(
                tenantId,
                CloneReferralQuery(windowQuery, status: null, pageSize: recentTop),
                ct);

            return result.Items;
        }

        if (matchingStatuses.Count == 1)
        {
            var result = await referrals.SearchAsync(
                tenantId,
                CloneReferralQuery(windowQuery, matchingStatuses[0], recentTop),
                ct);

            return result.Items;
        }

        var results = new List<ReferralResponse>();
        foreach (var value in matchingStatuses)
        {
            var result = await referrals.SearchAsync(
                tenantId,
                CloneReferralQuery(windowQuery, value, recentTop),
                ct);
            results.AddRange(result.Items);
        }

        return results
            .OrderByDescending(item => item.CreatedAtUtc)
            .ThenByDescending(item => item.UpdatedAtUtc)
            .Take(recentTop)
            .ToList();
    }

    private static string? NormalizeSummaryStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return null;

        var normalized = status.Trim()
            .Replace("-", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("_", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(" ", string.Empty, StringComparison.OrdinalIgnoreCase)
            .ToUpperInvariant();

        return normalized switch
        {
            "NEW" => Referral.ValidStatuses.New,
            "NEWOPENED" => Referral.ValidStatuses.NewOpened,
            "ACCEPTED" or "RECEIVED" or "CONTACTED" => Referral.ValidStatuses.Accepted,
            "INPROGRESS" or "SCHEDULED" => Referral.ValidStatuses.InProgress,
            "COMPLETED" => Referral.ValidStatuses.Completed,
            "DECLINED" => Referral.ValidStatuses.Declined,
            "CANCELLED" or "CANCELED" => Referral.ValidStatuses.Cancelled,
            _ => status.Trim(),
        };
    }

    private static string? NormalizeSummaryStatusGroup(string? statusGroup)
    {
        if (string.IsNullOrWhiteSpace(statusGroup))
            return null;

        var normalized = statusGroup.Trim()
            .Replace("-", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("_", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(" ", string.Empty, StringComparison.OrdinalIgnoreCase)
            .ToUpperInvariant();

        return normalized switch
        {
            "NEW" or "PENDING" or "INBOX" => "new",
            "OPEN" or "ACTIVE" => "open",
            "CLOSED" or "TERMINAL" or "RESOLVED" => "closed",
            _ => null,
        };
    }

    private static async Task<CareConnectReferralQueueStatusCount> CountStatusAsync(
        IReferralService referrals,
        Guid tenantId,
        GetReferralsQuery baseQuery,
        string status,
        CancellationToken ct)
    {
        var result = await referrals.SearchAsync(
            tenantId,
            CloneReferralQuery(baseQuery, status, 1),
            ct);

        return new CareConnectReferralQueueStatusCount(status, result.TotalCount);
    }

    private static bool CanAccessReferral(ReferralResponse referral, ICurrentRequestContext ctx)
    {
        if (CareConnectParticipantHelper.IsAdmin(ctx))
            return true;

        return (ctx.OrgId.HasValue && referral.ReferringOrganizationId == ctx.OrgId) ||
               (ctx.OrgId.HasValue && referral.ReceivingOrganizationId == ctx.OrgId) ||
               (!string.IsNullOrWhiteSpace(ctx.Email) &&
                referral.ReferringOrganizationId == null &&
                string.Equals(referral.ReferrerEmail, ctx.Email, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ShouldUseGlobalReferralLookup(ICurrentRequestContext ctx)
        => CareConnectParticipantHelper.ShouldUseAssistantGlobalReferralLookup(ctx);

    private static CareConnectReferralSearchResult ToReferralSearchResult(ReferralResponse item)
        => new(
            item.Id,
            BuildClientDisplayName(item.ClientFirstName, item.ClientLastName),
            item.Status,
            item.Urgency,
            item.ProviderName,
            NullIfWhiteSpace(item.RequestedService),
            NullIfWhiteSpace(item.TreatmentTypeName),
            NullIfWhiteSpace(item.ReferringOrganizationName),
            NullIfWhiteSpace(item.ReferrerName),
            item.CreatedAtUtc,
            item.UpdatedAtUtc);

    private static CareConnectProviderSearchResult ToProviderSearchResult(ProviderResponse item)
        => new(
            item.Id,
            item.Name,
            NullIfWhiteSpace(item.OrganizationName),
            item.City,
            item.State,
            item.AcceptingReferrals,
            item.IsActive,
            NullIfWhiteSpace(item.PrimaryCategory),
            item.DisplayLabel);

    private static CareConnectReferralHistoryLookupItem ToHistoryItem(ReferralStatusHistoryResponse item)
        => new(
            item.OldStatus,
            item.NewStatus,
            item.ChangedAtUtc,
            NormalizeNotes(item.Notes));

    private static string BuildClientDisplayName(string firstName, string lastName)
    {
        var combined = string.Join(' ', new[] { firstName?.Trim(), lastName?.Trim() }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
        return string.IsNullOrWhiteSpace(combined) ? "Unnamed client" : combined;
    }

    private static string? NullIfWhiteSpace(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeNotes(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
            return null;

        var trimmed = notes.Trim();
        return trimmed.Length <= 160 ? trimmed : trimmed[..160];
    }

    private static bool IsOpenStatus(string status)
        => !status.Equals("Completed", StringComparison.OrdinalIgnoreCase)
           && !status.Equals("Declined", StringComparison.OrdinalIgnoreCase)
           && !status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase);
}

internal sealed class AssistantReferralLookupParams
{
    public int? HistoryTop { get; init; }
}

internal sealed class AssistantReferralHistoryParams
{
    public int? Top { get; init; }
}

internal class AssistantReferralSearchParams
{
    public string? Search { get; init; }
    public string? ClientName { get; init; }
    public string? CaseNumber { get; init; }
    public Guid? ProviderId { get; init; }
    public string? ProviderName { get; init; }
    public string? ReferrerName { get; init; }
    public string? Status { get; init; }
    public DateTime? CreatedFrom { get; init; }
    public DateTime? CreatedTo { get; init; }
    public int? Top { get; init; }
}

internal sealed class AssistantProviderSearchParams
{
    public string? Name { get; init; }
    public string? City { get; init; }
    public string? State { get; init; }
    public bool? AcceptingReferrals { get; init; }
    public int? Top { get; init; }
}

internal sealed class AssistantReferrerSearchParams
{
    public string? Search { get; init; }
    public string? ReferrerName { get; init; }
    public string? Status { get; init; }
    public int? Top { get; init; }
}

internal sealed class AssistantReferralQueueSummaryParams
{
    public string? Search { get; init; }
    public string? ProviderName { get; init; }
    public string? ReferrerName { get; init; }
    public string? Status { get; init; }
    public string? StatusGroup { get; init; }
    public int? Days { get; init; }
    public DateTime? CreatedFrom { get; init; }
    public DateTime? CreatedTo { get; init; }
    public int? RecentTop { get; init; }
}
