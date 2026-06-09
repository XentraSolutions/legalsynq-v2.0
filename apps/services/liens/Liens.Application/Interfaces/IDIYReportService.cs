using Liens.Application.DTOs;

namespace Liens.Application.Interfaces;

public interface IDIYReportService
{
    Task<List<DIYReportConfigResponse>> GetSavedReportsAsync(Guid tenantId, Guid userId, CancellationToken ct = default);
    Task<DIYReportConfigResponse>       GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<DIYReportConfigResponse>       SaveReportAsync(Guid tenantId, Guid userId, SaveDIYReportRequest request, CancellationToken ct = default);
    Task DeleteReportAsync(Guid tenantId, Guid id, Guid userId, CancellationToken ct = default);

    /// <summary>Execute a DIY report filter query and return paginated case rows.</summary>
    Task<PaginatedResult<DIYReportRow>> RunReportAsync(Guid tenantId, DIYReportRunRequest request, CancellationToken ct = default);
}

public sealed class DIYReportRow
{
    public Guid    CaseId       { get; init; }
    public string  CaseNumber   { get; init; } = string.Empty;
    public string  ClientName   { get; init; } = string.Empty;
    public string? Status       { get; init; }
    public decimal? LienTotal   { get; init; }
    public Dictionary<string, object?> Extra { get; init; } = new();
}
