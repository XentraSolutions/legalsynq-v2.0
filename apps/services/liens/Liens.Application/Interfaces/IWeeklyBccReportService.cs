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
}
