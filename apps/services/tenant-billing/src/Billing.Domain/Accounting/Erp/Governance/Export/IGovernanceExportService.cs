using System.Threading;
using System.Threading.Tasks;

namespace Billing.Domain.Accounting.Erp.Governance.Export;

/// <summary>
/// MS-BILL-ERP-008 — Read-only governance-export composer. Each
/// method calls the existing ERP-007
/// <see cref="IErpGovernanceAnalyticsService"/> for the source
/// projection and then serialises it into the requested
/// <see cref="GovernanceExportFormat"/>.
///
/// Implementations MUST NOT:
///   - mutate any persisted state,
///   - re-run a fingerprint,
///   - call a QBO endpoint,
///   - publish to a queue / event bus,
///   - cache, schedule, or background the work.
///
/// Tenant id is supplied by the caller (the controller resolves
/// it from <see cref="Billing.Api.Tenancy.ITenantContext"/>).
/// </summary>
public interface IGovernanceExportService
{
    Task<GovernanceExportPayload> ExportSummaryAsync(
        System.Guid tenantId,
        int? windowDays,
        GovernanceExportFormat format,
        CancellationToken ct = default);

    Task<GovernanceExportPayload> ExportTrendsAsync(
        System.Guid tenantId,
        int? windowDays,
        GovernanceExportFormat format,
        CancellationToken ct = default);

    Task<GovernanceExportPayload> ExportRemediationAgingAsync(
        System.Guid tenantId,
        int? windowDays,
        GovernanceExportFormat format,
        CancellationToken ct = default);

    Task<GovernanceExportPayload> ExportAuditTrailAsync(
        System.Guid tenantId,
        int? windowDays,
        int? page,
        int? pageSize,
        GovernanceExportFormat format,
        CancellationToken ct = default);

    Task<GovernanceExportPayload> ExportDriftIndicatorsAsync(
        System.Guid tenantId,
        int? windowDays,
        GovernanceExportFormat format,
        CancellationToken ct = default);
}
