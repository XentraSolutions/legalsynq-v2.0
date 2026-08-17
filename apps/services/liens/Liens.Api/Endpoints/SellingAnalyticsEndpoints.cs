using System.Globalization;
using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using BuildingBlocks.Context;
using BuildingBlocks.Exceptions;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Domain;
using Liens.Domain.Enums;

namespace Liens.Api.Endpoints;

public static class SellingAnalyticsEndpoints
{
    private static readonly HashSet<string> AllowedDateDimensions = new(StringComparer.Ordinal)
    {
        "submitted",
        "sold",
        "offer",
        "service",
    };

    private static readonly HashSet<string> AllowedGrains = new(StringComparer.Ordinal)
    {
        "day",
        "week",
        "month",
    };

    private static readonly HashSet<string> AllowedConcentrationDimensions = new(StringComparer.Ordinal)
    {
        "fundingCompany",
        "facility",
        "sellerStatus",
        "listingVisibility",
    };

    private static readonly HashSet<string> AllowedExportReports = new(StringComparer.Ordinal)
    {
        "overview",
        "statusBreakdown",
        "funnel",
        "timeseries",
        "offers",
        "buyerPerformance",
        "aging",
        "concentration",
    };

    public static void MapSellingAnalyticsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/liens/selling")
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(LiensPermissions.ProductCode)
            .RequireSellMode();

        var analytics = group.MapGroup("/analytics")
            .RequirePermission(LiensPermissions.LienSaleViewAnalytics);

        analytics.MapGet("/overview", GetOverview);
        analytics.MapGet("/status-breakdown", GetStatusBreakdown);
        analytics.MapGet("/funnel", GetFunnel);
        analytics.MapGet("/timeseries", GetTimeseries);
        analytics.MapGet("/offers", GetOffers);
        analytics.MapGet("/buyer-performance", GetBuyerPerformance);
        analytics.MapGet("/aging", GetAging);
        analytics.MapGet("/concentration", GetConcentration);
        analytics.MapGet("/filter-options", GetFilterOptions);
        analytics.MapGet("/receivables-dashboard", GetReceivablesDashboard);
        analytics.MapPost("/export", Export);

        group.MapGet("/liens/{lienId:guid}/analytics", GetLienAnalytics)
            .RequirePermission(LiensPermissions.LienSaleViewAnalytics);
    }

    private static Guid RequireTenantId(ICurrentRequestContext ctx)
    {
        return ctx.TenantId
            ?? throw new UnauthorizedAccessException("Tenant context is required.");
    }

    private static Guid RequireOrgId(ICurrentRequestContext ctx)
    {
        return ctx.OrgId
            ?? throw new UnauthorizedAccessException("Organization context is required.");
    }

    private static async Task<IResult> GetOverview(
        HttpRequest request,
        ISellingAnalyticsService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var filter = ParseFilter(request);
        var result = await service.GetOverviewAsync(RequireTenantId(ctx), RequireOrgId(ctx), filter, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetStatusBreakdown(
        HttpRequest request,
        ISellingAnalyticsService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var filter = ParseFilter(request);
        var result = await service.GetStatusBreakdownAsync(RequireTenantId(ctx), RequireOrgId(ctx), filter, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetFunnel(
        HttpRequest request,
        ISellingAnalyticsService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var filter = ParseFilter(request);
        var result = await service.GetFunnelAsync(RequireTenantId(ctx), RequireOrgId(ctx), filter, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetTimeseries(
        HttpRequest request,
        ISellingAnalyticsService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var filter = ParseFilter(request, requireDateDimension: true);
        var result = await service.GetTimeseriesAsync(RequireTenantId(ctx), RequireOrgId(ctx), filter, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetOffers(
        HttpRequest request,
        ISellingAnalyticsService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var filter = ParseFilter(request);
        var result = await service.GetOffersAsync(RequireTenantId(ctx), RequireOrgId(ctx), filter, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetBuyerPerformance(
        HttpRequest request,
        ISellingAnalyticsService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var filter = ParseFilter(request);
        var result = await service.GetBuyerPerformanceAsync(RequireTenantId(ctx), RequireOrgId(ctx), filter, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetAging(
        HttpRequest request,
        ISellingAnalyticsService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var filter = ParseFilter(request);
        var result = await service.GetAgingAsync(RequireTenantId(ctx), RequireOrgId(ctx), filter, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetConcentration(
        HttpRequest request,
        ISellingAnalyticsService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var filter = ParseFilter(request);
        var dimension = FirstQueryValue(request, "dimension")
            ?? FirstQueryValue(request, "concentrationDimension")
            ?? "sellerStatus";
        ValidateAllowedValue("dimension", dimension, AllowedConcentrationDimensions);
        var result = await service.GetConcentrationAsync(RequireTenantId(ctx), RequireOrgId(ctx), filter, dimension, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetFilterOptions(
        HttpRequest request,
        ISellingAnalyticsService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var filter = ParseFilter(request);
        var result = await service.GetFilterOptionsAsync(RequireTenantId(ctx), RequireOrgId(ctx), filter, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetReceivablesDashboard(
        HttpRequest request,
        HttpResponse response,
        ISellingReceivablesDashboardService service,
        ICurrentRequestContext ctx,
        TimeProvider timeProvider,
        CancellationToken ct = default)
    {
        var dashboardRequest = ParseReceivablesDashboardRequest(request, timeProvider);
        var result = await service.GetAsync(
            RequireTenantId(ctx),
            RequireOrgId(ctx),
            dashboardRequest,
            ct);
        response.Headers.CacheControl = "no-store";
        return Results.Ok(result);
    }

    private static async Task<IResult> GetLienAnalytics(
        Guid lienId,
        ISellingAnalyticsService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var result = await service.GetLienAnalyticsAsync(RequireTenantId(ctx), RequireOrgId(ctx), lienId, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> Export(
        SellingAnalyticsExportRequest request,
        ISellingAnalyticsService service,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        ValidateExportRequest(request);
        var result = await service.ExportAsync(RequireTenantId(ctx), RequireOrgId(ctx), request, ct);
        return Results.File(result.Content, result.ContentType, result.FileName);
    }

    private static SellingAnalyticsFilter ParseFilter(HttpRequest request, bool requireDateDimension = false)
    {
        var errors = new Dictionary<string, string[]>();
        var dateFrom = ParseDate(request, "dateFrom", errors);
        var dateTo = ParseDate(request, "dateTo", errors);
        if (dateFrom.HasValue && dateTo.HasValue && dateFrom.Value > dateTo.Value)
            AddError(errors, "dateRange", "dateFrom must be less than or equal to dateTo.");

        var sellerStatuses = ReadStringValues(request, "sellerStatus");
        foreach (var status in sellerStatuses)
            if (!SellingLienStatus.All.Contains(status))
                AddError(errors, "sellerStatus", $"Invalid sellerStatus '{status}'.");

        var listingVisibilities = ReadStringValues(request, "listingVisibility");
        foreach (var visibility in listingVisibilities)
            if (!SellingListingVisibility.All.Contains(visibility))
                AddError(errors, "listingVisibility", $"Invalid listingVisibility '{visibility}'.");

        var fundingCompanyIds = ReadGuidValues(request, "fundingCompanyId", errors);
        var facilityIds = ReadGuidValues(request, "facilityId", errors);
        var includeArchived = ParseBool(request, "includeArchived", errors) ?? false;
        var dateDimension = FirstQueryValue(request, "dateDimension");
        if (requireDateDimension && string.IsNullOrWhiteSpace(dateDimension))
            AddError(errors, "dateDimension", "dateDimension is required.");
        dateDimension = string.IsNullOrWhiteSpace(dateDimension) ? "submitted" : dateDimension;
        ValidateAllowedValue("dateDimension", dateDimension, AllowedDateDimensions, errors);

        var grain = FirstQueryValue(request, "grain");
        grain = string.IsNullOrWhiteSpace(grain) ? "month" : grain;
        ValidateAllowedValue("grain", grain, AllowedGrains, errors);

        if (errors.Count > 0)
            throw new ValidationException("Selling analytics filter is invalid.", errors);

        return new SellingAnalyticsFilter
        {
            DateFrom = dateFrom,
            DateTo = dateTo,
            SellerStatuses = sellerStatuses,
            ListingVisibilities = listingVisibilities,
            FundingCompanyIds = fundingCompanyIds,
            FacilityIds = facilityIds,
            IncludeArchived = includeArchived,
            DateDimension = dateDimension,
            Grain = grain,
            ConcentrationDimension = FirstQueryValue(request, "concentrationDimension"),
        };
    }

    private static void ValidateExportRequest(SellingAnalyticsExportRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (!AllowedExportReports.Contains(request.Report))
            AddError(errors, "report", $"Invalid report '{request.Report}'.");

        if (request.DateFrom.HasValue && request.DateTo.HasValue && request.DateFrom.Value > request.DateTo.Value)
            AddError(errors, "dateRange", "dateFrom must be less than or equal to dateTo.");

        foreach (var status in request.SellerStatus)
            if (!SellingLienStatus.All.Contains(status))
                AddError(errors, "sellerStatus", $"Invalid sellerStatus '{status}'.");

        foreach (var visibility in request.ListingVisibility)
            if (!SellingListingVisibility.All.Contains(visibility))
                AddError(errors, "listingVisibility", $"Invalid listingVisibility '{visibility}'.");

        ValidateAllowedValue("dateDimension", request.DateDimension, AllowedDateDimensions, errors);
        ValidateAllowedValue("grain", request.Grain, AllowedGrains, errors);

        if (string.Equals(request.Report, "timeseries", StringComparison.Ordinal) && string.IsNullOrWhiteSpace(request.DateDimension))
            AddError(errors, "dateDimension", "dateDimension is required for timeseries export.");

        if (string.Equals(request.Report, "concentration", StringComparison.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(request.ConcentrationDimension))
                AddError(errors, "concentrationDimension", "concentrationDimension is required for concentration export.");
            else
                ValidateAllowedValue("concentrationDimension", request.ConcentrationDimension, AllowedConcentrationDimensions, errors);
        }

        if (errors.Count > 0)
            throw new ValidationException("Selling analytics export request is invalid.", errors);
    }

    private static DateOnly? ParseDate(HttpRequest request, string key, Dictionary<string, string[]> errors)
    {
        var value = FirstQueryValue(request, key);
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return date;

        AddError(errors, key, $"{key} must be ISO yyyy-MM-dd.");
        return null;
    }

    private static SellingReceivablesDashboardRequest ParseReceivablesDashboardRequest(
        HttpRequest request,
        TimeProvider timeProvider)
    {
        var errors = new Dictionary<string, string[]>();
        var utcToday = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var asOfDate = ParseDate(request, "asOfDate", errors) ?? utcToday;
        if (asOfDate != utcToday)
            AddError(errors, "asOfDate", "asOfDate must be the current UTC date until historical snapshots are available.");
        var months = ParseBoundedInt(request, "months", 6, 1, 12, errors);
        var topBuyerLimit = ParseBoundedInt(request, "topBuyerLimit", 5, 1, 20, errors);

        if (errors.Count > 0)
            throw new ValidationException("Receivables dashboard request is invalid.", errors);

        return new SellingReceivablesDashboardRequest
        {
            AsOfDate = asOfDate,
            Months = months,
            TopBuyerLimit = topBuyerLimit,
        };
    }

    private static int ParseBoundedInt(
        HttpRequest request,
        string key,
        int defaultValue,
        int minimum,
        int maximum,
        Dictionary<string, string[]> errors)
    {
        var value = FirstQueryValue(request, key);
        if (string.IsNullOrWhiteSpace(value))
            return defaultValue;

        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
        {
            AddError(errors, key, $"{key} must be an integer from {minimum} to {maximum}.");
            return defaultValue;
        }

        if (parsed < minimum || parsed > maximum)
        {
            AddError(errors, key, $"{key} must be from {minimum} to {maximum}.");
            return defaultValue;
        }

        return parsed;
    }

    private static bool? ParseBool(HttpRequest request, string key, Dictionary<string, string[]> errors)
    {
        var value = FirstQueryValue(request, key);
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (bool.TryParse(value, out var result))
            return result;

        AddError(errors, key, $"{key} must be true or false.");
        return null;
    }

    private static List<Guid> ReadGuidValues(HttpRequest request, string key, Dictionary<string, string[]> errors)
    {
        var result = new List<Guid>();
        foreach (var value in ReadStringValues(request, key))
        {
            if (Guid.TryParse(value, out var guid))
                result.Add(guid);
            else
                AddError(errors, key, $"{key} value '{value}' must be a GUID.");
        }

        return result.Distinct().ToList();
    }

    private static List<string> ReadStringValues(HttpRequest request, string key)
    {
        if (!request.Query.TryGetValue(key, out var rawValues))
            return [];

        return rawValues
            .SelectMany(value => (value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static string? FirstQueryValue(HttpRequest request, string key)
    {
        return request.Query.TryGetValue(key, out var values)
            ? values.FirstOrDefault()
            : null;
    }

    private static void ValidateAllowedValue(
        string key,
        string? value,
        HashSet<string> allowedValues,
        Dictionary<string, string[]>? errors = null)
    {
        if (!string.IsNullOrWhiteSpace(value) && allowedValues.Contains(value))
            return;

        if (errors is null)
        {
            throw new ValidationException("Selling analytics filter is invalid.",
                new Dictionary<string, string[]> { [key] = [$"Invalid {key} '{value}'."] });
        }

        AddError(errors, key, $"Invalid {key} '{value}'.");
    }

    private static void AddError(Dictionary<string, string[]> errors, string key, string message)
    {
        if (!errors.TryGetValue(key, out var existing))
        {
            errors[key] = [message];
            return;
        }

        errors[key] = existing.Concat([message]).ToArray();
    }
}
