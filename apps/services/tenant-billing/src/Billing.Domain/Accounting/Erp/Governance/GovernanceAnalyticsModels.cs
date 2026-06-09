namespace Billing.Domain.Accounting.Erp.Governance;

/// <summary>
/// MS-BILL-ERP-007 — Top-level KPI rollup for the tenant-admin
/// governance dashboard. Computed deterministically at request
/// time over a clamped recent window (default 7d, hard cap 90d).
/// Carries NO recipient PII, NO provider secret, NO token. Every
/// counter is derived from append-only ERP-001/003/005/006 rows.
/// </summary>
public sealed record ErpGovernanceSummary(
    int WindowDays,
    DateTime WindowFromUtc,
    DateTime WindowToUtc,
    int TotalExports,
    int ExportedCount,
    int FailedCount,
    int ProviderUnavailableCount,
    int DuplicateCount,
    int SkippedCount,
    int PendingCount,
    decimal ExportSuccessRatePercent,
    decimal FailedExportRatePercent,
    decimal ReplayRatePercent,
    int ActiveCustomerCount,
    int ActiveMappingCount,
    int InactiveMappingCount,
    int UnresolvedMappingCount,
    decimal MappingCoveragePercent,
    decimal AverageRemediationAgeDays,
    int InvoiceFirstMappingCount,
    decimal InvoiceFirstAdoptionPercent,
    int RecentGovernanceFailureCount,
    DateTime ObservedAtUtc);

/// <summary>
/// MS-BILL-ERP-007 — One row of the export-trend chart. Bucketed
/// at the UTC date layer so the row count is bounded by
/// <see cref="ErpGovernanceSummary.WindowDays"/>.
/// </summary>
public sealed record ErpExportTrendBucket(
    DateTime BucketDateUtc,
    string Provider,
    string ExportType,
    int TotalCount,
    int ExportedCount,
    int FailedCount,
    int ProviderUnavailableCount,
    int DuplicateCount);

/// <summary>
/// MS-BILL-ERP-007 — Aggregated trend response envelope.
/// </summary>
public sealed record ErpExportTrendResult(
    int WindowDays,
    DateTime WindowFromUtc,
    DateTime WindowToUtc,
    IReadOnlyList<ErpExportTrendBucket> Buckets);

/// <summary>
/// MS-BILL-ERP-007 — Single unresolved-customer row used by the
/// remediation-aging table. Mirrors the ERP-005 surface area but
/// adds <see cref="AgeDays"/> so the dashboard can colour-code
/// staleness without the browser doing date math against a
/// possibly-skewed clock.
/// </summary>
public sealed record RemediationAgingRow(
    Guid BillingCustomerId,
    string BillingCustomerName,
    DateTime CustomerCreatedAtUtc,
    int AgeDays,
    DateTime? LastInvoiceDate,
    string? ExistingMappingStatus,
    string ExportBlockedReason);

/// <summary>
/// MS-BILL-ERP-007 — Velocity counters for completed remediations.
/// Computed deterministically from the mapping table:
///   `MappingsResolvedInWindow` = count of Active mappings whose
///       <c>CreatedAtUtc</c> falls within the window.
///   `BulkImportsInWindow`      = count of bulk-import history
///       rows in the window (operator-driven remediation events).
/// </summary>
public sealed record RemediationVelocity(
    int WindowDays,
    int MappingsResolvedInWindow,
    int BulkImportsInWindow,
    int BulkImportAcceptedRowsInWindow);

/// <summary>
/// MS-BILL-ERP-007 — Remediation-aging response envelope.
/// </summary>
public sealed record RemediationAgingResult(
    int UnresolvedCount,
    int OldestAgeDays,
    decimal AverageAgeDays,
    int StaleMappingCount,
    int StaleWindowDays,
    RemediationVelocity Velocity,
    IReadOnlyList<RemediationAgingRow> Oldest,
    DateTime ObservedAtUtc);

/// <summary>
/// MS-BILL-ERP-007 — Allow-listed governance audit-trail action
/// types. Stable strings so the BFF / UI can render badges off
/// the literal value. NEW values must be appended (never reused
/// or renamed) to preserve audit-replay determinism.
/// </summary>
public static class GovernanceAuditActionType
{
    /// <summary>Mapping row inserted via ERP-003 single-mapping POST.</summary>
    public const string MappingCreated = "MappingCreated";

    /// <summary>Mapping row updated via ERP-003 single-mapping PUT (UpdatedAtUtc differs from CreatedAtUtc).</summary>
    public const string MappingUpdated = "MappingUpdated";

    /// <summary>ERP-006 bulk import audit row.</summary>
    public const string BulkImportCommitted = "BulkImportCommitted";

    /// <summary>ERP-001 export attempt (any terminal status).</summary>
    public const string ExportAttempt = "ExportAttempt";
}

/// <summary>
/// MS-BILL-ERP-007 — Single audit-trail row. Composed at the
/// application layer from three immutable sources (mapping table,
/// bulk-import history, accounting-exports). Carries no PII
/// beyond the operator display name that the upstream rows
/// already persisted.
/// </summary>
public sealed record GovernanceAuditEntry(
    DateTime TimestampUtc,
    string ActionType,
    string Operator,
    string TargetEntityType,
    string TargetEntityId,
    string Result,
    string? CorrelationId,
    string? Detail);

/// <summary>
/// MS-BILL-ERP-007 — Audit-trail response envelope. Pagination is
/// clamped server-side: <see cref="PageSize"/> ≤ 100,
/// <see cref="Page"/> ≥ 1, and <see cref="TotalCount"/> is
/// computed deterministically from the union of source rows in
/// the same window so the operator can size the navigator.
/// </summary>
public sealed record GovernanceAuditResult(
    int Page,
    int PageSize,
    int TotalCount,
    int WindowDays,
    DateTime WindowFromUtc,
    DateTime WindowToUtc,
    IReadOnlyList<GovernanceAuditEntry> Entries);

/// <summary>
/// MS-BILL-ERP-007 — Per-fingerprint drift indicator row. Used by
/// both the "repeated failures" and "replay-heavy" tables. The
/// fingerprint is deliberately truncated (first 12 hex chars) so
/// the operator can correlate to the ERP-004 reconciliation page
/// without ever seeing the raw sha256.
/// </summary>
public sealed record DriftFingerprintRow(
    string FingerprintShort,
    string Provider,
    string ExportType,
    int Occurrences,
    DateTime LastSeenAtUtc,
    string? LastFailureReason);

/// <summary>
/// MS-BILL-ERP-007 — Drift-indicator response envelope.
/// Diagnostics-only by contract.
/// </summary>
public sealed record DriftIndicatorResult(
    int WindowDays,
    DateTime WindowFromUtc,
    DateTime WindowToUtc,
    int RepeatedFailureCount,
    int StaleMappingCount,
    int StaleWindowDays,
    int ReplayHeavyCount,
    int UnresolvedMappingCount,
    string? LastGovernanceFailureReason,
    DateTime? LastGovernanceFailureAtUtc,
    IReadOnlyList<DriftFingerprintRow> RepeatedFailures,
    IReadOnlyList<DriftFingerprintRow> ReplayHeavy,
    DateTime ObservedAtUtc);
