using Liens.Application.DTOs;

namespace Liens.Application.Interfaces;

public interface IWeeklyBccReportService
{
    Task<WeeklyBccReportResult> GetAsync(
        Guid tenantId,
        DateOnly asOfDate,
        CancellationToken ct = default);
}
