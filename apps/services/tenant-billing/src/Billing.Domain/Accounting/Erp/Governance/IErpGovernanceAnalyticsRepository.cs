namespace Billing.Domain.Accounting.Erp.Governance;

/// <summary>
/// MS-BILL-ERP-007 — Read-only, tenant-scoped projections that
/// back the governance analytics service. Every method MUST
/// filter by tenant id at the SQL layer and use AsNoTracking.
/// NEVER mutates a Billing row.
/// </summary>
public interface IErpGovernanceAnalyticsRepository
{
    /// <summary>
    /// Per-status counts of <c>accounting_exports</c> within the
    /// given UTC window. Caller computes ratios.
    /// </summary>
    Task<IReadOnlyDictionary<string, int>> GetExportCountsByStatusAsync(
        Guid tenantId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken ct = default);

    /// <summary>
    /// Mapping-table summary used by the KPI block: total active
    /// (non-deleted) customers, total Active mappings, total non-
    /// Active (Disabled) mappings, and InvoiceFirst-mode active
    /// mapping count.
    /// </summary>
    Task<MappingTotals> GetMappingTotalsAsync(
        Guid tenantId,
        CancellationToken ct = default);

    /// <summary>
    /// Bounded list of unresolved customers (no Active mapping)
    /// ordered oldest first. Used to populate the remediation-
    /// aging table without recomputing what ERP-005 already does.
    /// </summary>
    Task<IReadOnlyList<UnresolvedCustomerAgingRow>> GetUnresolvedCustomerAgingAsync(
        Guid tenantId,
        int hardCap,
        CancellationToken ct = default);

    /// <summary>
    /// Count of Active mappings whose <c>LastExportedAtUtc</c> is
    /// either NULL or older than <paramref name="staleBefore"/>.
    /// </summary>
    Task<int> GetStaleMappingCountAsync(
        Guid tenantId,
        DateTime staleBefore,
        CancellationToken ct = default);

    /// <summary>
    /// Daily-bucket trend rows segmented by provider + export
    /// type. The repo does the date truncation and grouping so
    /// the bucket count is bounded by the window.
    /// </summary>
    Task<IReadOnlyList<TrendBucketRow>> GetExportTrendBucketsAsync(
        Guid tenantId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken ct = default);

    /// <summary>
    /// Mapping audit rows (creates + updates) inside the window.
    /// </summary>
    Task<IReadOnlyList<MappingAuditRow>> GetMappingAuditRowsAsync(
        Guid tenantId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken ct = default);

    /// <summary>
    /// Bulk-import history rows inside the window.
    /// </summary>
    Task<IReadOnlyList<BulkImportAuditRow>> GetBulkImportAuditRowsAsync(
        Guid tenantId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken ct = default);

    /// <summary>
    /// Export audit rows inside the window.
    /// </summary>
    Task<IReadOnlyList<ExportAuditRow>> GetExportAuditRowsAsync(
        Guid tenantId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken ct = default);

    /// <summary>
    /// Bounded fingerprints with ≥ 2 Failed/ProviderUnavailable
    /// rows in the window, ordered by occurrence count desc.
    /// </summary>
    Task<IReadOnlyList<FingerprintCountRow>> GetRepeatedFailureFingerprintsAsync(
        Guid tenantId,
        DateTime fromUtc,
        DateTime toUtc,
        int hardCap,
        CancellationToken ct = default);

    /// <summary>
    /// Bounded fingerprints with ≥ 2 Duplicate rows in the
    /// window, ordered by occurrence count desc.
    /// </summary>
    Task<IReadOnlyList<FingerprintCountRow>> GetReplayHeavyFingerprintsAsync(
        Guid tenantId,
        DateTime fromUtc,
        DateTime toUtc,
        int hardCap,
        CancellationToken ct = default);

    /// <summary>
    /// Most-recent Failed / ProviderUnavailable row for the
    /// tenant — surfaced on the drift indicator panel as "the
    /// last governance failure your operators saw".
    /// </summary>
    Task<(string? FailureReason, DateTime? AtUtc)> GetMostRecentFailureAsync(
        Guid tenantId,
        CancellationToken ct = default);

    /// <summary>
    /// Bulk-import counts (rows + accepted rows sum) in the
    /// window. Used by the remediation-velocity block.
    /// </summary>
    Task<(int BulkImportCount, int AcceptedRowsSum)> GetBulkImportVelocityAsync(
        Guid tenantId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken ct = default);

    /// <summary>
    /// Count of Active mappings whose <c>CreatedAtUtc</c> falls
    /// within the window — operator-driven mapping resolutions
    /// during the period.
    /// </summary>
    Task<int> GetMappingsResolvedInWindowAsync(
        Guid tenantId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken ct = default);

    /// <summary>
    /// Average age in days (vs <paramref name="nowUtc"/>) of every
    /// unresolved customer (active, non-deleted, no Active
    /// mapping). Returns 0 when there are no unresolved customers
    /// so the dashboard does not render NaN. Computed at the SQL
    /// layer so the bounded aging table does not bias the mean.
    /// </summary>
    Task<decimal> GetAverageUnresolvedAgeDaysAsync(
        Guid tenantId,
        DateTime nowUtc,
        CancellationToken ct = default);
}

public sealed record MappingTotals(
    int ActiveCustomerCount,
    int ActiveMappingCount,
    int InactiveMappingCount,
    int InvoiceFirstActiveMappingCount,
    int UnresolvedMappingCount);

public sealed record UnresolvedCustomerAgingRow(
    Guid BillingCustomerId,
    string BillingCustomerName,
    DateTime CustomerCreatedAtUtc,
    DateTime? LastInvoiceDate,
    string? ExistingMappingStatus);

public sealed record TrendBucketRow(
    DateTime BucketDateUtc,
    string Provider,
    string ExportType,
    string Status,
    int Count);

public sealed record MappingAuditRow(
    Guid MappingId,
    string CreatedBy,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    string MappingStatus);

public sealed record BulkImportAuditRow(
    Guid Id,
    string OperatorDisplayName,
    DateTime StartedAtUtc,
    DateTime CompletedAtUtc,
    int TotalRows,
    int AcceptedRows,
    int RejectedRows);

public sealed record ExportAuditRow(
    Guid ExportId,
    string RequestedBy,
    DateTime RequestedAtUtc,
    string Status,
    string Provider,
    string ExportType,
    string CorrelationId);

public sealed record FingerprintCountRow(
    string Fingerprint,
    string Provider,
    string ExportType,
    int Count,
    DateTime LastSeenAtUtc,
    string? LastFailureReason);
