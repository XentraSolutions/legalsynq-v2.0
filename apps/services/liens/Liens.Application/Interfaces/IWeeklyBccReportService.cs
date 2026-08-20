using Liens.Application.DTOs;

namespace Liens.Application.Interfaces;

public interface IWeeklyBccReportService
{
    Task<WeeklyBccReportResult> GetAsync(
        Guid tenantId,
        DateOnly asOfDate,
        CancellationToken ct = default);

    Task<WeeklyBccReportResult> GetPageAsync(
        Guid tenantId,
        DateOnly asOfDate,
        int page,
        int pageSize,
        bool includeTotalCount = true,
        CancellationToken ct = default);

    Task<WeeklyBccExportBatch> GetExportBatchAsync(
        Guid tenantId,
        DateOnly asOfDate,
        WeeklyBccExportCursor? cursor,
        int batchSize,
        CancellationToken ct = default);
}

public sealed record WeeklyBccExportCursor(
    DateOnly PurchaseDate,
    string LienNumber,
    Guid LienId);

public sealed record WeeklyBccExportBatch(
    IReadOnlyList<WeeklyBccReportRow> Items,
    WeeklyBccExportCursor? NextCursor);
