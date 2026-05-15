namespace Billing.Domain.Accounting.Erp.Governance;

/// <summary>
/// MS-BILL-ERP-007 — Application-level composer for the five
/// tenant-admin governance dashboards. Pure read composition over
/// <see cref="IErpGovernanceAnalyticsRepository"/>; no mutation,
/// no provider call, no scheduler.
/// </summary>
public interface IErpGovernanceAnalyticsService
{
    Task<ErpGovernanceSummary> GetSummaryAsync(
        Guid tenantId,
        int? windowDays,
        CancellationToken ct = default);

    Task<ErpExportTrendResult> GetExportTrendsAsync(
        Guid tenantId,
        int? windowDays,
        CancellationToken ct = default);

    Task<RemediationAgingResult> GetRemediationAgingAsync(
        Guid tenantId,
        int? windowDays,
        CancellationToken ct = default);

    Task<GovernanceAuditResult> GetAuditTrailAsync(
        Guid tenantId,
        int? windowDays,
        int? page,
        int? pageSize,
        CancellationToken ct = default);

    Task<DriftIndicatorResult> GetDriftIndicatorsAsync(
        Guid tenantId,
        int? windowDays,
        CancellationToken ct = default);
}
