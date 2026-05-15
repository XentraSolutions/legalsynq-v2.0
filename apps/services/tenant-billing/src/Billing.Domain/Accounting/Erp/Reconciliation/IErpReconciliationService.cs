namespace Billing.Domain.Accounting.Erp.Reconciliation;

/// <summary>
/// MS-BILL-ERP-004 — Read-only orchestration seam for the tenant-
/// admin reconciliation dashboard. Pure diagnostics: every public
/// method is a query, returns deterministically, and never mutates
/// a Billing row.
/// </summary>
public interface IErpReconciliationService
{
    Task<ErpReconciliationSummary> GetSummaryAsync(
        Guid tenantId,
        CancellationToken ct = default);

    Task<IReadOnlyList<ErpExportDiagnostic>> ListExportsAsync(
        Guid tenantId,
        ErpReconciliationListQuery query,
        CancellationToken ct = default);

    Task<ErpExportDiagnosticDetail?> GetExportDetailAsync(
        Guid tenantId,
        Guid exportId,
        CancellationToken ct = default);

    Task<ErpMappingHealthSnapshot> GetMappingHealthAsync(
        Guid tenantId,
        CancellationToken ct = default);

    Task<ErpProviderHealthSnapshot> GetProviderHealthAsync(
        Guid tenantId,
        CancellationToken ct = default);
}
