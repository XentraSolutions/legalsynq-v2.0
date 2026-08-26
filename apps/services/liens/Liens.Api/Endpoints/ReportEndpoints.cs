using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using BuildingBlocks.Context;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Api.Serialization;
using Liens.Domain.Enums;
using Liens.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System.Globalization;
using System.Text;
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
        var reports = app.MapGroup("/api/liens/reports")
            .RequireAuthorization(Policies.AuthenticatedUser)
            .RequireProductAccess(LiensPermissions.ProductCode);

        reports.MapPost("/weekly-bcc", GetWeeklyBccReport)
            .RequirePermission(LiensPermissions.CaseRead);

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

        legacy.MapPost("/weekly-bcc", GetWeeklyBccReport)
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

    private static async Task<IResult> GetWeeklyBccReport(
        WeeklyBccReportRequest? request,
        IWeeklyBccReportService svc,
        ICurrentRequestContext ctx,
        HttpContext httpContext,
        CancellationToken ct = default)
    {
        httpContext.Response.Headers.CacheControl = "no-store";
        if (request is null)
        {
            return Results.BadRequest(new
            {
                isSuccess = false,
                message = "Request body is required.",
            });
        }

        if (!TryParseWeeklyBccAsOfDate(request.AsOfDate, out var asOfDate))
        {
            return Results.BadRequest(new
            {
                isSuccess = false,
                message = "A valid asOfDate is required. Use MM/dd/yyyy or yyyy-MM-dd.",
            });
        }

        var tenantId = CaseEndpoints.RequireTenantId(ctx);
        var result = await svc.GetAsync(tenantId, asOfDate, ct);
        return Results.Ok(new
        {
            isSuccess = true,
            message = "Weekly BCC report generated.",
            asOfDate = result.AsOfDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            totalCount = result.Items.Count,
            summaryTotals = result.SummaryTotals,
            data = result.Items,
        });
    }

    private static bool TryParseWeeklyBccAsOfDate(string? value, out DateOnly asOfDate)
    {
        asOfDate = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        string[] formats = ["MM/dd/yyyy", "M/d/yyyy", "yyyy-MM-dd", "yyyy-M-d"];
        return DateOnly.TryParseExact(
            value.Trim(),
            formats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out asOfDate);
    }

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
        var effectiveRequest = await ResolveSavedReportRequestAsync(request, svc, tenantId, ct);
        var result = await svc.RunReportAsync(
            tenantId,
            effectiveRequest,
            includeAllItems: false,
            ct: ct);
        return Results.Ok(ToLegacyRunResponse(result, effectiveRequest));
    }

    private static async Task<IResult> ExportReport(
        DIYReportRunRequest request,
        IDIYReportService svc,
        ICurrentRequestContext ctx,
        CancellationToken ct = default)
    {
        var tenantId = CaseEndpoints.RequireTenantId(ctx);
        var effectiveRequest = await ResolveSavedReportRequestAsync(request, svc, tenantId, ct);
        var result = await svc.RunReportAsync(
            tenantId,
            effectiveRequest,
            includeAllItems: true,
            ct: ct);
        var rows = BuildLegacyReportRows(result, effectiveRequest);
        var csvBytes = BuildLegacyReportCsv(rows);
        var filename = $"diy_report_{DateTime.UtcNow:yyyyMMddHHmmss}.csv";

        return Results.Ok(new
        {
            isSuccess = true,
            message = "CSV generated successfully.",
            data = new object[]
            {
                new
                {
                    base64 = Convert.ToBase64String(csvBytes),
                    filename,
                    export_format = "csv",
                },
            },
        });
    }

    private static async Task<DIYReportRunRequest> ResolveSavedReportRequestAsync(
        DIYReportRunRequest request,
        IDIYReportService svc,
        Guid tenantId,
        CancellationToken ct)
    {
        if (!TryGetReportProperty(request, "reportId", out var reportIdValue) ||
            reportIdValue.ValueKind != JsonValueKind.String ||
            !Guid.TryParse(reportIdValue.GetString(), out var reportId))
        {
            return request;
        }

        var savedReport = await svc.GetByIdAsync(tenantId, reportId, ct);
        return new DIYReportRunRequest
        {
            Config = savedReport.Config.Clone(),
            Page = request.Page,
            Limit = request.Limit,
            SortBy = request.SortBy,
            SortDir = request.SortDir,
            ExtensionData = request.ExtensionData,
        };
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
            // The report editor uses this sequence to render its initially selected columns.
            // Keep it independent of the grouped metadata order below.
            defaultColumn = GetLegacyDefaultColumnKeys(reportType),
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

    private static object ToLegacyRunResponse(
        DIYReportResult result,
        DIYReportRunRequest request)
    {
        var rows = BuildLegacyReportRows(result, request);
        var legacyBillingAmount = string.Equals(
            result.ReportType,
            "LIENS",
            StringComparison.OrdinalIgnoreCase)
                ? result.SummaryTotals.TotalAmtToSettle
                : result.SummaryTotals.TotalBillingAmt;
        var summaryTotals = new Dictionary<string, object?>
        {
            ["totalCases"] = result.SummaryTotals.TotalCases,
            ["totalLiens"] = result.SummaryTotals.TotalLiens,
            ["totalPurchaseAmt"] = result.SummaryTotals.TotalPurchaseAmt,
            // The legacy LIENS card labels the outstanding balance as billing.
            // Preserve gross billing separately so API consumers do not lose it.
            ["totalBillingAmt"] = legacyBillingAmount,
            ["grossBillingAmt"] = result.SummaryTotals.TotalBillingAmt,
            ["totalAmtToSettle"] = result.SummaryTotals.TotalAmtToSettle,
            ["totalReturnedAmt"] = result.SummaryTotals.TotalReturnedAmt,
            ["totalGrossProfit"] = result.SummaryTotals.TotalGrossProfit,
            ["avgRoi"] = result.SummaryTotals.AvgRoi,
            ["totalOpenCases"] = result.SummaryTotals.TotalOpenCases,
            ["totalClosedCases"] = result.SummaryTotals.TotalClosedCases,
            ["totalOpenLiens"] = result.SummaryTotals.TotalOpenLiens,
            ["totalClosedLiens"] = result.SummaryTotals.TotalClosedLiens,
        };
        if (TryGetReportBoolean(request, "isBoldReady"))
            summaryTotals["_bold"] = true;

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
            summaryTotals,
            data = rows,
            page = result.Page,
            limit = result.PageSize,
            totalCount = result.TotalCount,
        };
    }

    private static List<Dictionary<string, object?>> BuildLegacyReportRows(
        DIYReportResult result,
        DIYReportRunRequest request)
    {
        var requestedColumns = GetRequestedColumns(request);
        return result.Items
            .Select(r => ProjectColumns(BuildLegacyRow(r), requestedColumns))
            .ToList();
    }

    private static byte[] BuildLegacyReportCsv(IReadOnlyList<Dictionary<string, object?>> rows)
    {
        var columns = rows
            .SelectMany(row => row.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var csv = new StringBuilder();
        csv.AppendLine(string.Join(',', columns.Select(column =>
            EscapeCsvValue(GetLegacyCsvColumnHeader(column)))));

        foreach (var row in rows)
        {
            csv.AppendLine(string.Join(',', columns.Select(column =>
                EscapeCsvValue(row.TryGetValue(column, out var value) ? value?.ToString() : null))));
        }

        return Encoding.UTF8.GetBytes(csv.ToString());
    }

    private static string EscapeCsvValue(string? value)
    {
        var rawValue = value ?? string.Empty;
        var firstSignificant = rawValue.FirstOrDefault(character =>
            !char.IsWhiteSpace(character) && !char.IsControl(character));
        var safeValue = firstSignificant is '=' or '+' or '-' or '@'
            ? $"'{rawValue}"
            : rawValue;
        var escaped = safeValue.Replace("\"", "\"\"");
        return escaped.IndexOfAny([',', '\"', '\r', '\n']) >= 0 ? $"\"{escaped}\"" : escaped;
    }

    private static string GetLegacyCsvColumnHeader(string key)
    {
        if (string.Equals(key, "last_case_tracking_note", StringComparison.OrdinalIgnoreCase))
            return "Last Activity";
        if (string.Equals(key, "last_case_tracking_date", StringComparison.OrdinalIgnoreCase))
            return "Last Activity Date";
        return key;
    }

    private static Dictionary<string, object?> BuildLegacyRow(DIYReportRow r) =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["plaintiff_first_name"] = r.PlaintiffFirstName,
            ["plaintiff_last_name"] = r.PlaintiffLastName,
            ["case_id"] = r.CaseNumber,
            ["lien_id"] = r.LienNumber,
            ["purchase_date"] = FormatLegacyDate(r.PurchaseDate),
            ["days_since_purchase"] = r.DaysSincePurchase?.ToString(CultureInfo.InvariantCulture),
            ["purchase_amt"] = FormatLegacyMoney(r.PurchaseAmount),
            ["billing_amt"] = FormatLegacyMoney(r.BillingAmount),
            ["remaining_billing_amt"] = FormatLegacyMoney(r.RemainingBillingAmount),
            ["expected_settlement_amount"] = FormatLegacyMoney(r.RemainingBillingAmount),
            ["reduction_percentage"] = FormatLegacyMoney(r.ReductionPercentage),
            ["capital_providers"] = string.Empty,
            ["first_purchase_date"] = FormatLegacyDate(r.FirstPurchaseDate),
            ["last_purchase_date"] = FormatLegacyDate(r.LastPurchaseDate),
            ["earliest_purchase_date"] = FormatLegacyDate(r.FirstPurchaseDate),
            ["latest_purchase_date"] = FormatLegacyDate(r.LastPurchaseDate),
            ["date_closed"] = FormatLegacyDate(r.DateClosed),
            ["latest_date_closed"] = FormatLegacyDate(r.DateClosed),
            ["reduction"] = FormatLegacyMoney(r.ReductionAmount),
            ["amt_to_settle"] = FormatLegacyMoney(r.ToSettleAmount),
            ["returned_amount"] = FormatLegacyMoney(r.ReturnedAmount),
            ["gross_profit"] = FormatLegacyMoney(r.GrossProfit),
            ["roi"] = FormatLegacyMoney(r.Roi),
            ["annualized_roi"] = FormatLegacyMoney(r.AnnualizedRoi),
            ["returned_amt"] = FormatLegacyMoney(r.ReturnedAmount),
            ["initial_service_date"] = FormatLegacyDate(r.InitialServiceDate),
            ["end_service_date"] = FormatLegacyDate(r.EndServiceDate),
            ["medical_provider"] = string.Empty,
            ["medical_facility_contact"] = string.Empty,
            ["medical_facility"] = r.MedicalFacility,
            ["medical_facility_address"] = string.Empty,
            ["medical_facility_city"] = string.Empty,
            ["medical_facility_state"] = string.Empty,
            ["medical_facility_zip_code"] = string.Empty,
            ["medical_codes"] = string.Empty,
            ["notes"] = r.FeedNote,
            ["notes_date"] = FormatLegacyDate(r.FeedNoteDate),
            ["lien_status"] = r.Status ?? string.Empty,
            ["attorney"] = string.Empty,
            ["attorney_phone"] = string.Empty,
            ["attorney_email"] = string.Empty,
            ["lawfirm"] = r.LawFirm,
            ["law_firm_address"] = string.Empty,
            ["law_firm_city"] = string.Empty,
            ["law_firm_state"] = string.Empty,
            ["law_firm_zip_code"] = string.Empty,
            ["law_firm_phone"] = string.Empty,
            ["case_type"] = r.CaseType,
            ["case_manager"] = r.CaseManager,
            ["case_manager_email"] = string.Empty,
            ["state_of_incident"] = string.Empty,
            ["settlement_date"] = FormatLegacyDate(r.SettlementDate),
            ["settle_date"] = FormatLegacyDate(r.SettlementDate),
            ["returned_date"] = FormatLegacyDate(r.DateClosed),
            ["reduction_date"] = FormatLegacyDate(r.ReductionDate),
            ["days_since_reduction_approval"] = r.DaysSinceReductionApproval?.ToString(CultureInfo.InvariantCulture),
            ["days_to_return"] = GetLegacyDaysBetween(r.PurchaseDate, r.DateClosed),
            ["lawfirm_email"] = string.Empty,
            ["number_of_liens"] = r.NumberOfLiens,
            ["case_status"] = FormatLegacyStatus(r.CaseStatus),
            ["medical_status"] = string.Empty,
            ["last_case_tracking_date"] = r.LastActivityAtUtc.HasValue
                ? PacificTimeHelper.FormatTimestamp(r.LastActivityAtUtc.Value)
                : string.Empty,
            ["last_case_tracking_note"] = r.LastActivity,
            ["case_tracking_follow_up_date"] = string.Empty,
            ["case_tracking_contact"] = string.Empty,
            ["case_tracking_contact_email"] = string.Empty,
            ["last_case_note"] = r.TrackingNotes,
            ["last_case_note_date"] = FormatLegacyDate(r.LastTrackingNoteDate),
            ["date_of_loss"] = FormatLegacyDate(r.DateOfLoss),
            ["plaintiff_date_of_birth"] = string.Empty,
            ["plaintiff_phone"] = string.Empty,
            ["plaintiff_email"] = string.Empty,
            ["plaintiff_address"] = string.Empty,
            ["plaintiff_city"] = string.Empty,
            ["plaintiff_state"] = string.Empty,
            ["plaintiff_zip_code"] = string.Empty,
            ["case_entered_by"] = string.Empty,
            ["lead_source"] = string.Empty,
            ["case_dropped"] = string.Empty,
            ["ucc_filed"] = r.UccFiled,
            ["minor_comp"] = string.Empty,
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
            .Select(column => column.ValueKind switch
            {
                JsonValueKind.String => column.GetString(),
                JsonValueKind.Object when column.TryGetProperty("key", out var key) && key.ValueKind == JsonValueKind.String => key.GetString(),
                JsonValueKind.Object when column.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String => name.GetString(),
                _ => null,
            })
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

    private static bool TryGetReportBoolean(DIYReportRunRequest request, string propertyName)
    {
        if (!TryGetReportProperty(request, propertyName, out var value))
            return false;

        if (value.ValueKind == JsonValueKind.True)
            return true;

        if (value.ValueKind == JsonValueKind.Number &&
            value.TryGetInt32(out var number))
        {
            return number != 0;
        }

        if (value.ValueKind != JsonValueKind.String)
            return false;

        var text = value.GetString()?.Trim();
        return bool.TryParse(text, out var parsed)
            ? parsed
            : text is not null &&
              (text.Equals("Y", StringComparison.OrdinalIgnoreCase) ||
               text.Equals("YES", StringComparison.OrdinalIgnoreCase) ||
               text == "1");
    }

    private static string? GetLegacyDaysBetween(DateOnly? from, DateTime? to)
    {
        if (!from.HasValue || !to.HasValue)
            return null;

        var days = DateOnly.FromDateTime(to.Value).DayNumber - from.Value.DayNumber;
        return Math.Max(days, 0).ToString(CultureInfo.InvariantCulture);
    }

    private static string? FormatLegacyDate(DateOnly? value) =>
        value.HasValue
            ? value.Value.ToString("MM/dd/yyyy", System.Globalization.CultureInfo.InvariantCulture)
            : null;

    private static string? FormatLegacyDate(DateTime? value) =>
        value.HasValue
            ? value.Value.ToString("MM/dd/yyyy", System.Globalization.CultureInfo.InvariantCulture)
            : null;

    private static string? FormatLegacyMoney(decimal? value) =>
        value.HasValue
            ? value.Value.ToString("#,0.00", System.Globalization.CultureInfo.InvariantCulture)
            : null;

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
            "casemanager" => await GetLegacyCaseManagerFilterOptionsAsync(
                contactService, tenantId, search, normalizedLimit, ct),
            "medicalprovider" => await GetLegacyContactFilterOptionsAsync(
                contactService, tenantId, ContactType.Provider, search, normalizedLimit, ct),
            "medicalfacility" => (await facilityService.SearchLienFacilitiesAsync(
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

    private static async Task<List<object>> GetLegacyCaseManagerFilterOptionsAsync(
        IContactService contactService,
        Guid tenantId,
        string? search,
        int limit,
        CancellationToken ct)
    {
        var standaloneCaseManagers = await contactService.SearchAsync(
            tenantId,
            search,
            ContactType.CaseManager,
            isActive: true,
            page: 1,
            pageSize: limit,
            ct: ct);
        var lawFirmCaseManagers = await contactService.SearchAsync(
            tenantId,
            search,
            ContactType.LawFirm,
            isActive: true,
            page: 1,
            pageSize: limit,
            contactSubtype: ContactSubtype.LawFirmCaseManager,
            ct: ct);

        return standaloneCaseManagers.Items
            .Concat(lawFirmCaseManagers.Items)
            .DistinctBy(item => item.Id)
            .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
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
        var defaultColumns = new HashSet<string>(
            GetLegacyDefaultColumnKeys(reportType),
            StringComparer.OrdinalIgnoreCase);

        return new List<LegacyReportColumnDefinition>
        {
            new("plaintiff_first_name", "Plaintiff First Name", "plaintiffInfo"),
            new("plaintiff_last_name", "Plaintiff Last Name", "plaintiffInfo"),
            new("case_id", "Case ID", "caseInfo"),
            new("lien_id", "Lien ID", "liensInfo"),
            new("purchase_date", "Purchase Date", "liensInfo"),
            new("days_since_purchase", "Days Since Purchase", "liensInfo"),
            new("purchase_amt", "Purchase Amt", "liensInfo"),
            new("billing_amt", "Billing Amt", "liensInfo"),
            new("expected_settlement_amount", "Expected Settlement Amount", "liensInfo"),
            new("reduction_percentage", "Reduction Percentage", "liensInfo"),
            new("capital_providers", "Capital Providers", "liensInfo"),
            new("number_of_liens", "Number Of Liens", "liensInfo"),
            new("date_closed", "Date Closed", "liensInfo"),
            new("first_purchase_date", "First Purchase Date", "liensInfo"),
            new("last_purchase_date", "Last Purchase Date", "liensInfo"),
            new("reduction", "Reduction", "settlementInfo"),
            new("amt_to_settle", "Amt To Settle", "settlementInfo"),
            new("gross_profit", "Gross Profit", "settlementInfo"),
            new("roi", "ROI", "settlementInfo"),
            new("annualized_roi", "Annualized ROI", "settlementInfo"),
            new("returned_amount", "Returned Amount", "settlementInfo"),
            new("to_settle_amt", "To Settle Amount", "settlementInfo"),
            new("settled_amt", "Settled Amount", "settlementInfo"),
            new("initial_service_date", "Initial Service Date", "procedureInfo"),
            new("end_service_date", "End Service Date", "procedureInfo"),
            new("medical_provider", "Medical Provider", "procedureInfo"),
            new("medical_facility_contact", "Medical Facility Contact", "procedureInfo"),
            new("medical_facility", "Medical Facility", "procedureInfo"),
            new("medical_facility_address", "Medical Facility Address", "procedureInfo"),
            new("medical_facility_city", "Medical Facility City", "procedureInfo"),
            new("medical_facility_state", "Medical Facility State", "procedureInfo"),
            new("medical_facility_zip_code", "Medical Facility ZIP Code", "procedureInfo"),
            new("medical_codes", "Medical Codes", "procedureInfo"),
            new("notes", "Notes", "procedureInfo"),
            new("notes_date", "Notes Date", "procedureInfo"),
            new("attorney", "Attorney", "caseInfo"),
            new("attorney_phone", "Attorney Phone", "caseInfo"),
            new("attorney_email", "Attorney Email", "caseInfo"),
            new("lawfirm", "Law Firm", "caseInfo"),
            new("law_firm_address", "Law Firm Address", "caseInfo"),
            new("law_firm_city", "Law Firm City", "caseInfo"),
            new("law_firm_state", "Law Firm State", "caseInfo"),
            new("law_firm_zip_code", "Law Firm ZIP Code", "caseInfo"),
            new("law_firm_phone", "Law Firm Phone", "caseInfo"),
            new("case_type", "Case Type", "caseInfo"),
            new("case_manager", "Case Manager", "caseInfo"),
            new("case_manager_email", "Case Manager Email", "caseInfo"),
            new("state_of_incident", "State of Incident", "caseInfo"),
            new("date_of_loss", "Date of Loss", "caseInfo"),
            new("settlement_date", "Settlement Date", "caseInfo"),
            new("reduction_date", "Reduction Date", "caseInfo"),
            new("days_since_reduction_approval", "Days Since Reduction Approval", "caseInfo"),
            new("days_to_return", "Days To Return", "caseInfo"),
            new("lawfirm_email", "Lawfirm Email", "caseInfo"),
            new("case_status", "Case Status", "caseTrackingInfo"),
            new("medical_status", "Medical Status", "caseTrackingInfo"),
            new("last_case_tracking_date", "Last Activity Date", "caseTrackingInfo"),
            new("last_case_tracking_note", "Last Activity", "caseTrackingInfo"),
            new("case_tracking_follow_up_date", "Case Tracking Follow Up Date (For later from Servicing)", "caseTrackingInfo"),
            new("case_tracking_contact", "Case Tracking Contact (case manager)", "caseTrackingInfo"),
            new("case_tracking_contact_email", "Case Tracking Contact Email (case manager note)", "caseTrackingInfo"),
            new("last_case_note", "Tracking Notes", "caseTrackingInfo"),
            new("last_case_note_date", "Last Tracking Note Date", "caseTrackingInfo"),
            new("plaintiff_date_of_birth", "Plaintiff Date of Birth", "plaintiffInfo"),
            new("plaintiff_phone", "Plaintiff Phone", "plaintiffInfo"),
            new("plaintiff_email", "Plaintiff Email", "plaintiffInfo"),
            new("plaintiff_address", "Plaintiff Address", "plaintiffInfo"),
            new("plaintiff_city", "Plaintiff City", "plaintiffInfo"),
            new("plaintiff_state", "Plaintiff State", "plaintiffInfo"),
            new("plaintiff_zip_code", "Plaintiff ZIP Code", "plaintiffInfo"),
            new("case_entered_by", "Case Entered by", "plaintiffInfo"),
            new("lead_source", "Lead Source", "plaintiffInfo"),
            new("case_dropped", "Case Dropped", "plaintiffInfo"),
            new("ucc_filed", "UCC Filed", "plaintiffInfo"),
            new("minor_comp", "Minor Comp", "plaintiffInfo"),
            new("id", "Case Guid", "caseInfo"),
        }
        .Select(column => column with { IsDefault = defaultColumns.Contains(column.Key) })
        .ToList();
    }

    private static IReadOnlyList<string> GetLegacyDefaultColumnKeys(string reportType)
        => NormalizeLegacyReportType(reportType) == "CASES"
            ?
            [
                "plaintiff_first_name",
                "plaintiff_last_name",
                "case_id",
                "case_status",
                "date_of_loss",
            ]
            :
            [
                "plaintiff_first_name",
                "plaintiff_last_name",
                "case_id",
                "lien_id",
                "purchase_date",
                "purchase_amt",
                "billing_amt",
                "date_closed",
                "returned_amount",
                "days_since_reduction_approval",
                "medical_facility",
                "lawfirm",
                "case_type",
                "case_manager",
                "case_status",
                "date_of_loss",
            ];

    private sealed record LegacyReportColumnDefinition(
        string Key,
        string Label,
        string Category,
        bool IsDefault = false);
}
