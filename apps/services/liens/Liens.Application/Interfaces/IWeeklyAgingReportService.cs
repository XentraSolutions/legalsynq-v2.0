using Liens.Application.DTOs;

namespace Liens.Application.Interfaces;

public interface IWeeklyAgingReportService
{
    Task<WeeklyAgingReportResult> GetAsync(
        Guid tenantId,
        Guid sellerOrgId,
        DateOnly asOfDate,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<MonthlyAgingReportResult> GetMonthlyAsync(
        Guid tenantId,
        Guid sellerOrgId,
        DateOnly asOfDate,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<WeeklyAgingDetailReportResult> GetDetailAsync(
        Guid tenantId,
        Guid sellerOrgId,
        DateOnly asOfDate,
        int page,
        int pageSize,
        CancellationToken ct = default);
}
