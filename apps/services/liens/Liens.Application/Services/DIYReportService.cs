using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Application.Repositories;
using Liens.Domain.Entities;
using Liens.Domain.Enums;

namespace Liens.Application.Services;

public class DIYReportService : IDIYReportService
{
    private readonly IDIYReportConfigRepository _repo;
    private readonly ILienRepository            _lienRepo;
    private readonly ICaseRepository            _caseRepo;

    public DIYReportService(
        IDIYReportConfigRepository repo,
        ILienRepository lienRepo,
        ICaseRepository caseRepo)
    {
        _repo        = repo;
        _lienRepo    = lienRepo;
        _caseRepo    = caseRepo;
    }

    public async Task<List<DIYReportConfigResponse>> GetSavedReportsAsync(
        Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        var items = await _repo.GetByUserAsync(tenantId, userId, ct);
        return items.Select(Map).ToList();
    }

    public async Task<DIYReportConfigResponse> GetByIdAsync(
        Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdAsync(tenantId, id, ct)
            ?? throw new KeyNotFoundException($"Report config {id} not found.");
        return Map(entity);
    }

    public async Task<DIYReportConfigResponse> SaveReportAsync(
        Guid tenantId, Guid userId, SaveDIYReportRequest request, CancellationToken ct = default)
    {
        var configJson = request.Config.GetRawText();
        var entity = DIYReportConfig.Create(tenantId, userId, request.Name, configJson, userId);
        await _repo.AddAsync(entity, ct);
        return Map(entity);
    }

    public async Task DeleteReportAsync(
        Guid tenantId, Guid id, Guid userId, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdAsync(tenantId, id, ct)
            ?? throw new KeyNotFoundException($"Report config {id} not found.");
        entity.SoftDelete(userId);
        await _repo.UpdateAsync(entity, ct);
    }

    /// <summary>
    /// Executes the DIY report by reading the "status" filter from the config
    /// JSON and delegating to the case search.  Additional column/filter support
    /// can be added here as the product evolves.
    /// </summary>
    public async Task<DIYReportResult> RunReportAsync(
        Guid tenantId, DIYReportRunRequest request, CancellationToken ct = default)
    {
        // Extract known filters from the config payload
        string? search = null;
        var lienStatuses = new List<string>();
        var caseStatuses = new List<string>();
        var reportType = GetReportString(request, "reportType")?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(reportType))
            reportType = "LIENS";
        var isLiensReport = string.Equals(reportType, "LIENS", StringComparison.OrdinalIgnoreCase);
        var isCombinedReport = string.Equals(reportType, "COMBINED", StringComparison.OrdinalIgnoreCase);
        var isCasesReport = !isLiensReport && !isCombinedReport;

        if (HasReportProperty(request, "status", out var s) && s.ValueKind == JsonValueKind.String)
        {
            var status = s.GetString();
            if (!string.IsNullOrWhiteSpace(status))
                lienStatuses.Add(status.Trim());
        }
        if (HasReportProperty(request, "statusView", out var statusView) && statusView.ValueKind == JsonValueKind.String)
        {
            var status = statusView.GetString();
            if (!string.IsNullOrWhiteSpace(status))
            {
                ApplyStatusView(status.Trim(), isLiensReport || isCombinedReport, lienStatuses, caseStatuses);
            }
        }
        if (HasReportProperty(request, "lienStatusIds", out var statusIds) && statusIds.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in statusIds.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
                    lienStatuses.Add(item.GetString()!.Trim());
            }
        }
        if (HasReportProperty(request, "search", out var k) && k.ValueKind == JsonValueKind.String)
            search = k.GetString();
        var purchaseDateFrom = GetReportDateOnly(request, "purchaseDateFrom");
        var purchaseDateTo = GetReportDateOnly(request, "purchaseDateTo");
        var closedDateFrom = GetReportDateTime(request, "closedDateFrom", endOfDay: false);
        var closedDateTo = GetReportDateTime(request, "closedDateTo", endOfDay: true);
        var isBulk = GetReportString(request, "isBulk");
        var filterCaseIds = GetReportGuidList(request, "plaintiffCaseIds");

        var page = GetReportInt(request, "page", request.Page);
        if (page < 1) page = 1;
        var limit = GetReportInt(request, "limit", request.Limit);
        if (limit < 1) limit = 50;
        if (limit > 500) limit = 500;

        var reportData = await _lienRepo.SearchReportAsync(
            tenantId,
            search,
            lienStatuses.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            caseStatuses.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            purchaseDateFrom,
            purchaseDateTo,
            closedDateFrom,
            closedDateTo,
            NormalizeLegacyBooleanFilter(isBulk),
            filterCaseIds,
            page,
            limit,
            ct);

        var rowSource = isCasesReport ? reportData.AllItems : reportData.PageItems;
        var caseIds = rowSource
            .Select(l => l.CaseId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
        var casesById = new Dictionary<Guid, Case>();
        foreach (var caseId in caseIds)
        {
            var caseEntity = await _caseRepo.GetByIdAsync(tenantId, caseId, ct);
            if (caseEntity is not null)
                casesById[caseId] = caseEntity;
        }

        var rows = isCasesReport
            ? BuildCaseRows(reportData.AllItems, casesById, page, limit)
            : reportData.PageItems.Select(l => BuildLienRow(l, casesById)).ToList();

        var totalCount = isCasesReport
            ? reportData.AllItems.Select(l => l.CaseId).Where(id => id.HasValue).Distinct().Count()
            : reportData.TotalCount;

        var summary = await BuildSummaryAsync(tenantId, reportData.AllItems, ct);

        return new DIYReportResult
        {
            ReportType = reportType,
            Items      = rows,
            TotalCount = totalCount,
            Page       = page,
            PageSize   = limit,
            SummaryTotals = summary,
        };
    }

    private static void ApplyStatusView(
        string statusView,
        bool isLienLevel,
        List<string> lienStatuses,
        List<string> caseStatuses)
    {
        switch (statusView.ToUpperInvariant())
        {
            case "ALL":
                return;
            case "OPEN":
                if (isLienLevel)
                    lienStatuses.AddRange(LienStatus.Open);
                else
                    caseStatuses.AddRange(CaseStatus.All.Where(s => !IsClosedCaseStatus(s)));
                return;
            case "CLOSED":
                if (isLienLevel)
                    lienStatuses.AddRange(LienStatus.Terminal);
                else
                    caseStatuses.AddRange(CaseStatus.All.Where(IsClosedCaseStatus));
                return;
            case "REJECTED":
                if (isLienLevel)
                    lienStatuses.Add(LienStatus.Cancelled);
                return;
            default:
                if (isLienLevel && LienStatus.All.Contains(statusView))
                    lienStatuses.Add(statusView);
                else if (CaseStatus.All.Contains(statusView))
                    caseStatuses.Add(statusView);
                return;
        }
    }

    private static DIYReportRow BuildLienRow(Lien l, IReadOnlyDictionary<Guid, Case> casesById)
    {
        Case? caseEntity = null;
        if (l.CaseId.HasValue)
            casesById.TryGetValue(l.CaseId.Value, out caseEntity);

        return new DIYReportRow
        {
            CaseId = l.CaseId,
            LienId = l.Id,
            CaseNumber = caseEntity?.CaseNumber ?? string.Empty,
            LienNumber = l.LienNumber,
            PlaintiffFirstName = caseEntity?.ClientFirstName ?? l.SubjectFirstName ?? string.Empty,
            PlaintiffLastName = caseEntity?.ClientLastName ?? l.SubjectLastName ?? string.Empty,
            ClientName = caseEntity is null
                ? string.Join(" ", new[] { l.SubjectFirstName, l.SubjectLastName }.Where(v => !string.IsNullOrWhiteSpace(v)))
                : $"{caseEntity.ClientFirstName} {caseEntity.ClientLastName}".Trim(),
            Status = l.Status,
            CaseStatus = caseEntity?.Status,
            DateOfLoss = caseEntity?.DateOfIncident ?? l.IncidentDate,
            DateClosed = l.ClosedAtUtc ?? caseEntity?.ClosedAtUtc,
            InitialServiceDate = l.InitialServiceDate,
            BillingAmount = l.OriginalAmount,
            PurchaseAmount = l.PurchasePrice,
            ReturnedAmount = l.PayoffAmount,
            LienTotal = l.CurrentBalance,
            NumberOfLiens = 1,
            ToSettleAmount = l.CurrentBalance,
            SettledAmount = l.PayoffAmount,
            Extra = new Dictionary<string, object?>(),
        };
    }

    private static bool IsClosedCaseStatus(string? status) =>
        string.Equals(status, CaseStatus.Closed, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, CaseStatus.CaseSettled, StringComparison.OrdinalIgnoreCase);

    private static List<DIYReportRow> BuildCaseRows(
        IReadOnlyCollection<Lien> liens,
        IReadOnlyDictionary<Guid, Case> casesById,
        int page,
        int limit)
    {
        return liens
            .Where(l => l.CaseId.HasValue)
            .GroupBy(l => l.CaseId!.Value)
            .OrderByDescending(g => g.Key)
            .Skip((page - 1) * limit)
            .Take(limit)
            .Select(g =>
            {
                casesById.TryGetValue(g.Key, out var caseEntity);
                var first = g.First();

                return new DIYReportRow
                {
                    CaseId = g.Key,
                    LienId = null,
                    CaseNumber = caseEntity?.CaseNumber ?? string.Empty,
                    LienNumber = string.Empty,
                    PlaintiffFirstName = caseEntity?.ClientFirstName ?? first.SubjectFirstName ?? string.Empty,
                    PlaintiffLastName = caseEntity?.ClientLastName ?? first.SubjectLastName ?? string.Empty,
                    ClientName = caseEntity is null
                        ? string.Join(" ", new[] { first.SubjectFirstName, first.SubjectLastName }.Where(v => !string.IsNullOrWhiteSpace(v)))
                        : $"{caseEntity.ClientFirstName} {caseEntity.ClientLastName}".Trim(),
                    Status = null,
                    CaseStatus = caseEntity?.Status,
                    DateOfLoss = caseEntity?.DateOfIncident ?? first.IncidentDate,
                    DateClosed = caseEntity?.ClosedAtUtc ?? g.Max(l => l.ClosedAtUtc),
                    InitialServiceDate = g.Min(l => l.InitialServiceDate),
                    BillingAmount = g.Sum(l => l.OriginalAmount),
                    PurchaseAmount = g.Sum(l => l.PurchasePrice ?? 0m),
                    ReturnedAmount = g.Sum(l => l.PayoffAmount ?? 0m),
                    LienTotal = g.Sum(l => l.CurrentBalance ?? 0m),
                    NumberOfLiens = g.Count(),
                    ToSettleAmount = g.Sum(l => l.CurrentBalance ?? 0m),
                    SettledAmount = g.Sum(l => l.PayoffAmount ?? 0m),
                    Extra = new Dictionary<string, object?>(),
                };
            })
            .ToList();
    }

    private static bool HasReportProperty(DIYReportRunRequest request, string propertyName, out JsonElement value)
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

    private static int GetReportInt(DIYReportRunRequest request, string propertyName, int fallback)
    {
        if (!HasReportProperty(request, propertyName, out var value))
            return fallback;

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt32(out var number) => number,
            JsonValueKind.String when int.TryParse(value.GetString(), out var number) => number,
            _ => fallback,
        };
    }

    private static string? GetReportString(DIYReportRunRequest request, string propertyName)
    {
        if (!HasReportProperty(request, propertyName, out var value) ||
            value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.String)
            return value.GetString();

        if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in value.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
                    return item.GetString();
            }
        }

        return null;
    }

    private static DateOnly? GetReportDateOnly(DIYReportRunRequest request, string propertyName)
    {
        var value = GetReportString(request, propertyName);
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;
    }

    private static DateTime? GetReportDateTime(DIYReportRunRequest request, string propertyName, bool endOfDay)
    {
        var value = GetReportString(request, propertyName);
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (!DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return null;

        return endOfDay ? date.Date.AddDays(1).AddTicks(-1) : date.Date;
    }

    private static IReadOnlyCollection<Guid> GetReportGuidList(DIYReportRunRequest request, string propertyName)
    {
        if (!HasReportProperty(request, propertyName, out var value) ||
            value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return value.EnumerateArray()
            .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : null)
            .Where(v => Guid.TryParse(v, out _))
            .Select(v => Guid.Parse(v!))
            .ToList();
    }

    private static string? NormalizeLegacyBooleanFilter(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value.Trim().ToUpperInvariant() switch
        {
            "Y" or "YES" or "TRUE" or "1" => "Yes",
            "N" or "NO" or "FALSE" or "0" => "No",
            _ => value.Trim(),
        };
    }

    private async Task<DIYReportSummaryTotals> BuildSummaryAsync(
        Guid tenantId,
        IReadOnlyCollection<Lien> liens,
        CancellationToken ct)
    {
        var totalPurchase = liens.Sum(l => l.PurchasePrice ?? 0m);
        var totalBilling = liens.Sum(l => l.OriginalAmount);
        var totalReturned = liens.Sum(l => l.PayoffAmount ?? 0m);
        var totalAmtToSettle = liens.Sum(l => l.CurrentBalance ?? 0m);
        var totalGrossProfit = totalReturned - totalPurchase;
        var avgRoi = totalPurchase == 0m ? 0m : totalReturned / totalPurchase;
        var closedLiens = liens.Count(l => LienStatus.Terminal.Contains(l.Status));
        var uniqueCaseIds = liens
            .Select(l => l.CaseId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
        var closedCases = 0;

        foreach (var caseId in uniqueCaseIds)
        {
            var caseEntity = await _caseRepo.GetByIdAsync(tenantId, caseId, ct);
            if (caseEntity is not null &&
                IsClosedCaseStatus(caseEntity.Status))
            {
                closedCases++;
            }
        }

        return new DIYReportSummaryTotals
        {
            TotalCases = uniqueCaseIds.Count,
            TotalLiens = liens.Count,
            TotalPurchaseAmt = totalPurchase,
            TotalBillingAmt = totalBilling,
            TotalAmtToSettle = totalAmtToSettle,
            TotalReturnedAmt = totalReturned,
            TotalGrossProfit = totalGrossProfit,
            AvgRoi = avgRoi,
            TotalOpenCases = Math.Max(uniqueCaseIds.Count - closedCases, 0),
            TotalClosedCases = closedCases,
            TotalOpenLiens = liens.Count - closedLiens,
            TotalClosedLiens = closedLiens,
        };
    }

    private static DIYReportConfigResponse Map(DIYReportConfig r)
    {
        JsonElement config;
        try
        {
            config = JsonDocument.Parse(r.ConfigJson).RootElement;
        }
        catch
        {
            config = JsonDocument.Parse("{}").RootElement;
        }

        var reportType = GetString(config, "reportType") ?? "LIENS";
        var reportConfig = BuildReportConfig(config);
        var columnCount = CountColumns(config);

        return new DIYReportConfigResponse
        {
            Id           = r.Id,
            TenantId     = r.TenantId,
            UserId       = r.UserId,
            Name         = r.Name,
            Config       = config,
            CreatedAtUtc = r.CreatedAtUtc,
            UpdatedAtUtc = r.UpdatedAtUtc,
            ReportId     = r.Id.ToString(),
            ReportName   = r.Name,
            ReportDescription = null,
            ReportType   = reportType,
            CreatedAt    = FormatLegacyDate(r.CreatedAtUtc),
            CreatedBy    = string.Empty,
            UpdatedAt    = FormatLegacyDate(r.UpdatedAtUtc),
            ReportConfig = reportConfig,
            ColumnCount  = columnCount,
        };
    }

    private static JsonElement BuildReportConfig(JsonElement config)
    {
        var node = JsonNode.Parse(config.GetRawText()) as JsonObject ?? new JsonObject();
        var columns = new JsonArray();

        if (config.ValueKind == JsonValueKind.Object &&
            config.TryGetProperty("columns", out var configColumns) &&
            configColumns.ValueKind == JsonValueKind.Array)
        {
            foreach (var column in configColumns.EnumerateArray())
            {
                if (column.ValueKind == JsonValueKind.String)
                {
                    columns.Add(column.GetString());
                }
                else if (column.ValueKind == JsonValueKind.Object &&
                         column.TryGetProperty("key", out var key) &&
                         key.ValueKind == JsonValueKind.String)
                {
                    columns.Add(key.GetString());
                }
            }
        }

        node["columns"] = columns;
        return JsonDocument.Parse(node.ToJsonString()).RootElement;
    }

    private static int CountColumns(JsonElement config)
    {
        if (config.ValueKind != JsonValueKind.Object ||
            !config.TryGetProperty("columns", out var columns) ||
            columns.ValueKind != JsonValueKind.Array)
        {
            return 0;
        }

        return columns.GetArrayLength();
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(propertyName, out var property) &&
            property.ValueKind == JsonValueKind.String)
        {
            return property.GetString();
        }

        return null;
    }

    private static string FormatLegacyDate(DateTime value) =>
        value.ToString("MM/dd/yyyy", System.Globalization.CultureInfo.InvariantCulture);
}
