using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using BuildingBlocks.Context;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Domain.Enums;
using Liens.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace Liens.Api.Endpoints;

public static class ReportEndpoints
{
    private sealed class LegacyDIYFilterMetaRequest
    {
        public string? filterField { get; init; }
        public string? keyword { get; init; }
        public string? reportType { get; init; }
        public int limit { get; init; } = 50;
    }

    public static void MapReportEndpoints(this WebApplication app)
    {
        // ── v2 routes ─────────────────────────────────────────────────────────
        var v2 = app.MapGroup("/api/liens/reports/diy")
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(LiensPermissions.ProductCode);

        v2.MapGet("/saved", GetSavedReports)
            .RequirePermission(LiensPermissions.CaseRead);

        v2.MapGet("/saved/{id:guid}", GetSavedReportById)
            .RequirePermission(LiensPermissions.CaseRead);

        v2.MapPost("/save", SaveReport)
            .RequirePermission(LiensPermissions.CaseRead);

        v2.MapDelete("/saved/{id:guid}", DeleteReport)
            .RequirePermission(LiensPermissions.CaseUpdate);

        v2.MapPost("/run", RunReport)
            .RequirePermission(LiensPermissions.CaseRead);

        v2.MapPost("/export", ExportReport)
            .RequirePermission(LiensPermissions.CaseRead);

        // ── Legacy routes ─────────────────────────────────────────────────────
        var legacy = app.MapGroup("/report")
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(LiensPermissions.ProductCode);

        // GET /report/diy  — list saved reports
        legacy.MapGet("/diy", GetSavedReports)
            .RequirePermission(LiensPermissions.CaseRead);

        // POST /report/diy  — run / preview a report
        legacy.MapPost("/diy", RunReport)
            .RequirePermission(LiensPermissions.CaseRead);

        // POST /report/diy/export  — export as CSV
        legacy.MapPost("/diy/export", ExportReport)
            .RequirePermission(LiensPermissions.CaseRead);

        // POST /report/diy/save  — save config
        legacy.MapPost("/diy/save", SaveReport)
            .RequirePermission(LiensPermissions.CaseRead);

        // DELETE /report/diy/{id}
        legacy.MapDelete("/diy/{id:guid}", DeleteReport)
            .RequirePermission(LiensPermissions.CaseUpdate);

        // GET /report/diy/saved
        legacy.MapGet("/diy/saved", GetSavedReportsLegacy)
            .RequirePermission(LiensPermissions.CaseRead);

        // DELETE /report/diy/delete/{id}
        legacy.MapDelete("/diy/delete/{id:guid}", DeleteReport)
            .RequirePermission(LiensPermissions.CaseUpdate);

        // GET /report/diy/columns
        legacy.MapGet("/diy/columns", GetLegacyColumns)
            .RequirePermission(LiensPermissions.CaseRead);

        // POST /report/diy/filter-options
        legacy.MapPost("/diy/filter-options", GetLegacyFilterOptions)
            .RequirePermission(LiensPermissions.CaseRead);

        // GET /report/diy/all-filters
        legacy.MapGet("/diy/all-filters", GetLegacyAllFilterOptions)
            .RequirePermission(LiensPermissions.CaseRead);
    }

    // ── Handlers ──────────────────────────────────────────────────────────────

    private static async Task<IResult> GetSavedReports(
        IDIYReportService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = CaseEndpoints.RequireTenantId(ctx);
        var userId   = CaseEndpoints.RequireUserId(ctx);
        var result = await svc.GetSavedReportsAsync(tenantId, userId, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetSavedReportsLegacy(
        IDIYReportService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = CaseEndpoints.RequireTenantId(ctx);
        var userId = CaseEndpoints.RequireUserId(ctx);
        var result = await svc.GetSavedReportsAsync(tenantId, userId, ct);
        return Results.Ok(new
        {
            isSuccess = true,
            message = "Saved reports retrieved.",
            data = result,
        });
    }

    private static async Task<IResult> GetSavedReportById(
        Guid id,
        IDIYReportService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = CaseEndpoints.RequireTenantId(ctx);
        var result = await svc.GetByIdAsync(tenantId, id, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> SaveReport(
        SaveDIYReportRequest request,
        IDIYReportService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = CaseEndpoints.RequireTenantId(ctx);
        var userId   = CaseEndpoints.RequireUserId(ctx);
        var result = await svc.SaveReportAsync(tenantId, userId, request, ct);
        return Results.Created($"/api/liens/reports/diy/saved/{result.Id}", result);
    }

    private static async Task<IResult> DeleteReport(
        Guid id,
        IDIYReportService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = CaseEndpoints.RequireTenantId(ctx);
        var userId   = CaseEndpoints.RequireUserId(ctx);
        await svc.DeleteReportAsync(tenantId, id, userId, ct);
        return Results.Ok(new { isSuccess = true, message = "Successfully Deleted." });
    }

    private static async Task<IResult> RunReport(
        DIYReportRunRequest request,
        IDIYReportService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = CaseEndpoints.RequireTenantId(ctx);
        var result = await svc.RunReportAsync(tenantId, request, ct);
        return Results.Ok(ToLegacyRunResponse(result, request));
    }

    private static async Task<IResult> ExportReport(
        DIYReportRunRequest request,
        IDIYReportService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = CaseEndpoints.RequireTenantId(ctx);
        var result = await svc.RunReportAsync(tenantId,
            new DIYReportRunRequest
            {
                Config = request.Config,
                Page = request.Page,
                Limit = 10_000,
                SortBy = request.SortBy,
                SortDir = request.SortDir,
                ExtensionData = request.ExtensionData,
            },
            ct);

        var csv = BuildCsv(result.Items);
        var bytes = System.Text.Encoding.UTF8.GetBytes(csv);
        var base64 = Convert.ToBase64String(bytes);
        return Results.Ok(new { data = base64 });
    }

    private static IResult GetLegacyColumns(string reportType = "LIENS")
    {
        var columns = GetLegacyReportColumns(reportType);
        var grouped = columns
            .GroupBy(column => column.Category, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Select(column => new
                {
                    key = column.Key,
                    label = column.Label,
                    isDefault = column.IsDefault,
                }).ToList(),
                StringComparer.OrdinalIgnoreCase);

        grouped.TryGetValue("liensInfo", out var liensInfo);
        grouped.TryGetValue("settlementInfo", out var settlementInfo);
        grouped.TryGetValue("procedureInfo", out var procedureInfo);
        grouped.TryGetValue("caseInfo", out var caseInfo);
        grouped.TryGetValue("caseTrackingInfo", out var caseTrackingInfo);
        grouped.TryGetValue("plaintiffInfo", out var plaintiffInfo);

        return Results.Ok(new
        {
            isSuccess = true,
            message = "Columns retrieved.",
            reportType = NormalizeLegacyReportType(reportType),
            defaultColumn = columns.Where(column => column.IsDefault).Select(column => column.Key).ToList(),
            liensInfo = liensInfo ?? [],
            settlementInfo = settlementInfo ?? [],
            procedureInfo = procedureInfo ?? [],
            caseInfo = caseInfo ?? [],
            caseTrackingInfo = caseTrackingInfo ?? [],
            plaintiffInfo = plaintiffInfo ?? [],
            data = columns.Select(column => new
            {
                key = column.Key,
                label = column.Label,
                isDefault = column.IsDefault,
            }).ToList(),
        });
    }

    private static async Task<IResult> GetLegacyFilterOptions(
        LegacyDIYFilterMetaRequest request,
        ICaseService caseService,
        IContactService contactService,
        IFacilityService facilityService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = CaseEndpoints.RequireTenantId(ctx);
        var data = await GetLegacyFilterOptionsInternalAsync(
            tenantId,
            request.filterField,
            request.keyword,
            request.limit,
            caseService,
            contactService,
            facilityService,
            ct);

        return Results.Ok(new
        {
            isSuccess = true,
            message = "Filter options retrieved.",
            data,
        });
    }

    private static async Task<IResult> GetLegacyAllFilterOptions(
        string reportType,
        int limit,
        ICaseService caseService,
        IContactService contactService,
        IFacilityService facilityService,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = CaseEndpoints.RequireTenantId(ctx);
        var normalizedLimit = NormalizeLegacyLimit(limit);
        var fields = new[]
        {
            "plaintiff",
            "lawfirm",
            "attorney",
            "fundingcompany",
            "medicalfacility",
            "casemanager",
            "medicalprovider",
        };

        var data = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in fields)
        {
            data[field] = await GetLegacyFilterOptionsInternalAsync(
                tenantId,
                field,
                keyword: null,
                normalizedLimit,
                caseService,
                contactService,
                facilityService,
                ct);
        }

        return Results.Ok(new
        {
            isSuccess = true,
            message = "All filter options retrieved.",
            reportType = NormalizeLegacyReportType(reportType),
            data,
        });
    }

    private static string BuildCsv(List<DIYReportRow> rows)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("CaseId,CaseNumber,ClientName,Status,LienTotal");
        foreach (var r in rows)
            sb.AppendLine($"{r.CaseId},{CsvEscape(r.CaseNumber)},{CsvEscape(r.ClientName)},{CsvEscape(r.Status)},{r.LienTotal}");
        return sb.ToString();
    }

    private static object ToLegacyRunResponse(DIYReportResult result, DIYReportRunRequest request)
    {
        var requestedColumns = GetRequestedColumns(request);
        var rows = result.Items
            .Select(r => ProjectColumns(BuildLegacyRow(r), requestedColumns))
            .ToList();

        var message = result.ReportType.ToUpperInvariant() switch
        {
            "CASES" => "Cases report generated.",
            "COMBINED" => "Combined report generated.",
            _ => "Liens report generated.",
        };

        return new
        {
            isSuccess = true,
            message,
            summaryTotals = new
            {
                totalCases = result.SummaryTotals.TotalCases,
                totalLiens = result.SummaryTotals.TotalLiens,
                totalPurchaseAmt = result.SummaryTotals.TotalPurchaseAmt,
                totalBillingAmt = result.SummaryTotals.TotalBillingAmt,
                totalAmtToSettle = result.SummaryTotals.TotalAmtToSettle,
                totalReturnedAmt = result.SummaryTotals.TotalReturnedAmt,
                totalGrossProfit = result.SummaryTotals.TotalGrossProfit,
                avgRoi = result.SummaryTotals.AvgRoi,
                totalOpenCases = result.SummaryTotals.TotalOpenCases,
                totalClosedCases = result.SummaryTotals.TotalClosedCases,
                totalOpenLiens = result.SummaryTotals.TotalOpenLiens,
                totalClosedLiens = result.SummaryTotals.TotalClosedLiens,
            },
            data = rows,
            page = result.Page,
            limit = result.PageSize,
            totalCount = result.TotalCount,
        };
    }

    private static Dictionary<string, object?> BuildLegacyRow(DIYReportRow r) =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["plaintiff_first_name"] = r.PlaintiffFirstName,
            ["plaintiff_last_name"] = r.PlaintiffLastName,
            ["case_id"] = r.CaseNumber,
            ["lien_id"] = r.LienNumber,
            ["purchase_date"] = string.Empty,
            ["purchase_amt"] = FormatLegacyMoney(r.PurchaseAmount),
            ["billing_amt"] = FormatLegacyMoney(r.BillingAmount),
            ["date_closed"] = FormatLegacyDate(r.DateClosed),
            ["returned_amt"] = FormatLegacyMoney(r.ReturnedAmount),
            ["initial_service_date"] = FormatLegacyDate(r.InitialServiceDate),
            ["medical_facility"] = string.Empty,
            ["lawfirm"] = string.Empty,
            ["case_type"] = string.Empty,
            ["case_manager"] = " ",
            ["number_of_liens"] = r.NumberOfLiens,
            ["case_status"] = FormatLegacyStatus(r.CaseStatus),
            ["date_of_loss"] = FormatLegacyDate(r.DateOfLoss),
            ["id"] = r.CaseId?.ToString() ?? string.Empty,
            ["l_id"] = r.LienId?.ToString() ?? string.Empty,
            ["to_settle_amt"] = FormatLegacyMoney(r.ToSettleAmount),
            ["settled_amt"] = FormatLegacyMoney(r.SettledAmount),
        };

    private static Dictionary<string, object?> ProjectColumns(
        Dictionary<string, object?> row,
        IReadOnlyCollection<string> requestedColumns)
    {
        if (requestedColumns.Count == 0)
            return row;

        var projected = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var column in requestedColumns)
        {
            if (row.TryGetValue(column, out var value))
                projected[column] = value;
        }

        return projected;
    }

    private static IReadOnlyCollection<string> GetRequestedColumns(DIYReportRunRequest request)
    {
        if (!TryGetReportProperty(request, "columns", out var columns) ||
            columns.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return columns.EnumerateArray()
            .Select(column => column.ValueKind == JsonValueKind.String ? column.GetString() : null)
            .Where(column => !string.IsNullOrWhiteSpace(column))
            .Select(column => column!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool TryGetReportProperty(DIYReportRunRequest request, string propertyName, out JsonElement value)
    {
        if (request.Config.ValueKind == JsonValueKind.Object &&
            request.Config.TryGetProperty(propertyName, out value))
        {
            return true;
        }

        if (request.ExtensionData is not null &&
            request.ExtensionData.TryGetValue(propertyName, out value))
        {
            return true;
        }

        value = default;
        return false;
    }

    private static string FormatLegacyDate(DateOnly? value) =>
        value.HasValue
            ? value.Value.ToString("MM/dd/yyyy", System.Globalization.CultureInfo.InvariantCulture)
            : string.Empty;

    private static string FormatLegacyDate(DateTime? value) =>
        value.HasValue
            ? value.Value.ToString("MM/dd/yyyy", System.Globalization.CultureInfo.InvariantCulture)
            : string.Empty;

    private static string FormatLegacyMoney(decimal? value) =>
        value.HasValue
            ? value.Value.ToString("#,0.00", System.Globalization.CultureInfo.InvariantCulture)
            : "0.00";

    private static string FormatLegacyStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value switch
        {
            "PreDemand" => "Pre-demand",
            "DemandSent" => "Demand Sent",
            "InNegotiation" => "In Negotiation",
            "CaseSettled" => "Case Settled",
            _ => value,
        };
    }

    private static string CsvEscape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }

    private static async Task<List<object>> GetLegacyFilterOptionsInternalAsync(
        Guid tenantId,
        string? filterField,
        string? keyword,
        int limit,
        ICaseService caseService,
        IContactService contactService,
        IFacilityService facilityService,
        CancellationToken ct)
    {
        var normalizedField = (filterField ?? string.Empty).Trim().ToLowerInvariant();
        var normalizedLimit = NormalizeLegacyLimit(limit);
        var search = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim();

        return normalizedField switch
        {
            "plaintiff" => (await caseService.SearchAsync(
                    tenantId,
                    search,
                    status: null,
                    page: 1,
                    pageSize: normalizedLimit,
                    orgId: null,
                    ct))
                .Items
                .Select(item => (object)new
                {
                    id = item.Id.ToString(),
                    name = item.ClientDisplayName,
                    caseNumber = item.CaseNumber,
                })
                .ToList(),

            "lawfirm" => await GetLegacyContactFilterOptionsAsync(
                contactService, tenantId, ContactType.LawFirm, search, normalizedLimit, ct),
            "attorney" => await GetLegacyContactFilterOptionsAsync(
                contactService, tenantId, ContactType.LawFirm, search, normalizedLimit, ct),
            "fundingcompany" => await GetLegacyContactFilterOptionsAsync(
                contactService, tenantId, ContactType.FundingCompany, search, normalizedLimit, ct),
            "casemanager" => await GetLegacyContactFilterOptionsAsync(
                contactService, tenantId, ContactType.CaseManager, search, normalizedLimit, ct),
            "medicalprovider" => await GetLegacyContactFilterOptionsAsync(
                contactService, tenantId, ContactType.Provider, search, normalizedLimit, ct),
            "medicalfacility" => (await facilityService.SearchAsync(
                    tenantId,
                    search,
                    isActive: true,
                    page: 1,
                    pageSize: normalizedLimit,
                    ct))
                .Items
                .Select(item => (object)new
                {
                    id = item.Id.ToString(),
                    name = item.Name,
                })
                .ToList(),
            _ => [],
        };
    }

    private static async Task<List<object>> GetLegacyContactFilterOptionsAsync(
        IContactService contactService,
        Guid tenantId,
        string contactType,
        string? search,
        int limit,
        CancellationToken ct)
    {
        var result = await contactService.SearchAsync(
            tenantId,
            search,
            contactType,
            isActive: true,
            page: 1,
            pageSize: limit,
            ct: ct);

        return result.Items
            .Select(item => (object)new
            {
                id = item.Id.ToString(),
                name = string.IsNullOrWhiteSpace(item.DisplayName)
                    ? item.Organization ?? $"{item.FirstName} {item.LastName}".Trim()
                    : item.DisplayName,
            })
            .ToList();
    }

    private static int NormalizeLegacyLimit(int limit)
    {
        if (limit < 1)
            return 50;
        return Math.Min(limit, 200);
    }

    private static string NormalizeLegacyReportType(string? reportType)
        => string.IsNullOrWhiteSpace(reportType)
            ? "LIENS"
            : reportType.Trim().ToUpperInvariant();

    private static List<LegacyReportColumnDefinition> GetLegacyReportColumns(string reportType)
    {
        var normalizedReportType = NormalizeLegacyReportType(reportType);
        var defaultColumns = normalizedReportType == "CASES"
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "plaintiff_first_name",
                "plaintiff_last_name",
                "case_id",
                "case_status",
                "date_of_loss",
            }
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "plaintiff_first_name",
                "plaintiff_last_name",
                "case_id",
                "lien_id",
                "purchase_amt",
                "billing_amt",
                "case_status",
            };

        return new List<LegacyReportColumnDefinition>
        {
            new("plaintiff_first_name", "Plaintiff First Name", "plaintiffInfo"),
            new("plaintiff_last_name", "Plaintiff Last Name", "plaintiffInfo"),
            new("case_id", "Case ID", "caseInfo"),
            new("lien_id", "Lien ID", "liensInfo"),
            new("purchase_date", "Purchase Date", "liensInfo"),
            new("purchase_amt", "Purchase Amount", "liensInfo"),
            new("billing_amt", "Billing Amount", "liensInfo"),
            new("date_closed", "Date Closed", "caseTrackingInfo"),
            new("returned_amt", "Returned Amount", "settlementInfo"),
            new("initial_service_date", "Initial Service Date", "procedureInfo"),
            new("medical_facility", "Medical Facility", "liensInfo"),
            new("lawfirm", "Law Firm", "caseInfo"),
            new("case_type", "Case Type", "caseInfo"),
            new("case_manager", "Case Manager", "caseInfo"),
            new("number_of_liens", "Number Of Liens", "liensInfo"),
            new("case_status", "Case Status", "caseTrackingInfo"),
            new("date_of_loss", "Date Of Loss", "caseTrackingInfo"),
            new("id", "Case Guid", "caseInfo"),
            new("l_id", "Lien Guid", "liensInfo"),
            new("to_settle_amt", "To Settle Amount", "settlementInfo"),
            new("settled_amt", "Settled Amount", "settlementInfo"),
        }
        .Select(column => column with { IsDefault = defaultColumns.Contains(column.Key) })
        .ToList();
    }

    private sealed record LegacyReportColumnDefinition(
        string Key,
        string Label,
        string Category,
        bool IsDefault = false);
}
