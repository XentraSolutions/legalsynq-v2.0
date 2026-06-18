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
        var statuses = new List<string>();

        if (request.Config.ValueKind == JsonValueKind.Object)
        {
            if (request.Config.TryGetProperty("status", out var s) && s.ValueKind == JsonValueKind.String)
            {
                var status = s.GetString();
                if (!string.IsNullOrWhiteSpace(status))
                    statuses.Add(status.Trim());
            }
            if (request.Config.TryGetProperty("lienStatusIds", out var statusIds) && statusIds.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in statusIds.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
                        statuses.Add(item.GetString()!.Trim());
                }
            }
            if (request.Config.TryGetProperty("search", out var k) && k.ValueKind == JsonValueKind.String)
                search = k.GetString();
        }

        var page = request.Page < 1 ? 1 : request.Page;
        var limit = request.Limit < 1 ? 50 : request.Limit;
        if (limit > 500) limit = 500;

        var reportData = await _lienRepo.SearchReportAsync(
            tenantId,
            search,
            statuses.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            page,
            limit,
            ct);

        var caseIds = reportData.PageItems
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

        var rows = reportData.PageItems.Select(l =>
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
                InitialServiceDate = l.IncidentDate,
                BillingAmount = l.OriginalAmount,
                PurchaseAmount = l.PurchasePrice,
                ReturnedAmount = l.PayoffAmount,
                LienTotal = l.CurrentBalance,
                Extra = new Dictionary<string, object?>(),
            };
        }).ToList();

        var summary = await BuildSummaryAsync(tenantId, reportData.AllItems, ct);

        return new DIYReportResult
        {
            Items      = rows,
            TotalCount = reportData.TotalCount,
            Page       = page,
            PageSize   = limit,
            SummaryTotals = summary,
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
                string.Equals(caseEntity.Status, CaseStatus.Closed, StringComparison.OrdinalIgnoreCase))
            {
                closedCases++;
            }
        }

        return new DIYReportSummaryTotals
        {
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
