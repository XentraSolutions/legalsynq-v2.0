using System.Text.Json;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Application.Repositories;
using Liens.Domain.Entities;

namespace Liens.Application.Services;

public class DIYReportService : IDIYReportService
{
    private readonly IDIYReportConfigRepository _repo;
    private readonly ICaseService               _caseService;

    public DIYReportService(IDIYReportConfigRepository repo, ICaseService caseService)
    {
        _repo        = repo;
        _caseService = caseService;
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
    public async Task<PaginatedResult<DIYReportRow>> RunReportAsync(
        Guid tenantId, DIYReportRunRequest request, CancellationToken ct = default)
    {
        // Extract known filters from the config payload
        string? status = null;
        string? search = null;

        if (request.Config.ValueKind == JsonValueKind.Object)
        {
            if (request.Config.TryGetProperty("status", out var s) && s.ValueKind == JsonValueKind.String)
                status = s.GetString();
            if (request.Config.TryGetProperty("search", out var k) && k.ValueKind == JsonValueKind.String)
                search = k.GetString();
        }

        var cases = await _caseService.SearchAsync(
            tenantId, search, status, request.Page, request.Limit, null, ct);

        var rows = cases.Items.Select(c => new DIYReportRow
        {
            CaseId      = c.Id,
            CaseNumber  = c.CaseNumber,
            ClientName  = c.ClientDisplayName,
            Status      = c.Status,
            LienTotal   = null, // lien totals can be aggregated in a future enhancement
            Extra       = new Dictionary<string, object?>(),
        }).ToList();

        return new PaginatedResult<DIYReportRow>
        {
            Items      = rows,
            TotalCount = cases.TotalCount,
            Page       = cases.Page,
            PageSize   = cases.PageSize,
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
        return new DIYReportConfigResponse
        {
            Id           = r.Id,
            TenantId     = r.TenantId,
            UserId       = r.UserId,
            Name         = r.Name,
            Config       = config,
            CreatedAtUtc = r.CreatedAtUtc,
            UpdatedAtUtc = r.UpdatedAtUtc,
        };
    }
}
