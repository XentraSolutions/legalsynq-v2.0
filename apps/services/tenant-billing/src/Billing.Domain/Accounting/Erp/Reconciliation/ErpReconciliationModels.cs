namespace Billing.Domain.Accounting.Erp.Reconciliation;

/// <summary>
/// MS-BILL-ERP-004 — Read-only summary of the tenant's ERP export
/// posture. Computed from already-persisted <c>accounting_exports</c>
/// rows + the curated <c>quickbooks_customer_mappings</c> table.
/// Carries NO recipient PII, NO provider secret, NO token.
/// </summary>
public sealed record ErpReconciliationSummary(
    int TotalExports,
    int ExportedCount,
    int FailedCount,
    int DuplicateCount,
    int ProviderUnavailableCount,
    int SkippedCount,
    int PendingCount,
    ErpExportDiagnostic? LatestSuccessfulExport,
    ErpExportDiagnostic? LatestFailedExport,
    int UnmappedActiveCustomerCount,
    int StaleMappingCount,
    int StaleWindowDays,
    DateTime ObservedAtUtc);

/// <summary>
/// MS-BILL-ERP-004 — Lightweight projection of a single
/// <see cref="AccountingExport"/> row used by the reconciliation
/// list / detail / summary APIs. NEVER includes <c>PayloadJson</c>
/// (that surface is the existing ERP-001 controller).
/// </summary>
public sealed record ErpExportDiagnostic(
    Guid ExportId,
    string Provider,
    string ExportType,
    string Status,
    DateTime WindowFromUtc,
    DateTime WindowToUtc,
    string CorrelationId,
    string? ExternalReferenceId,
    string? FailureReason,
    int RecordCount,
    int InvoiceCount,
    int PaymentCount,
    int AdjustmentCount,
    int JournalEntryCount,
    string RequestedBy,
    DateTime RequestedAtUtc,
    DateTime? CompletedAtUtc,
    string FingerprintShort,
    bool IsDuplicate);

/// <summary>
/// MS-BILL-ERP-004 — Detail projection. Augments
/// <see cref="ErpExportDiagnostic"/> with the operator-supplied
/// reason and a <see cref="SiblingsByFingerprint"/> count so the
/// drill-down can render "this duplicate corresponds to N prior
/// attempts" without exposing the canonical payload.
/// </summary>
public sealed record ErpExportDiagnosticDetail(
    ErpExportDiagnostic Diagnostic,
    string? Reason,
    int SiblingsByFingerprint);

/// <summary>
/// MS-BILL-ERP-004 — Operator-curated mapping health snapshot.
/// Diagnostics only — no auto-fix, no fuzzy match, no QBO Customer
/// creation surface.
/// </summary>
public sealed record ErpMappingHealthSnapshot(
    int TotalMappings,
    int ActiveMappings,
    int InactiveMappings,
    int UnmappedActiveCustomerCount,
    int StaleMappingCount,
    int StaleWindowDays,
    ErpMappingHealthRow? LatestExportedMapping,
    DateTime ObservedAtUtc);

/// <summary>
/// MS-BILL-ERP-004 — Sample mapping row surfaced by the mapping-
/// health snapshot. Carries identifiers only — never a token, never
/// a provider secret.
/// </summary>
public sealed record ErpMappingHealthRow(
    Guid Id,
    Guid BillingCustomerId,
    string QuickBooksCustomerId,
    string? QuickBooksDisplayName,
    string MappingStatus,
    DateTime? LastExportedAtUtc);

/// <summary>
/// MS-BILL-ERP-004 — Per-provider rolling-window health derived
/// purely from append-only <c>accounting_exports</c> rows. No new
/// state, no scheduler, no provider call. The set is the providers
/// that have at least one historical export row for the tenant.
/// </summary>
public sealed record ErpProviderHealthSnapshot(
    int WindowSeconds,
    DateTime ObservedAtUtc,
    IReadOnlyList<ErpProviderHealthRow> Providers);

/// <summary>
/// MS-BILL-ERP-004 — Per-provider classification.
///
/// <para>
/// <see cref="State"/> is one of <see cref="ErpProviderHealthState"/>:
/// <c>Healthy</c>, <c>Degraded</c>, <c>Unavailable</c>, or
/// <c>Unknown</c>. Mirrors the OPS-002 / INT-003 provider-health
/// vocabulary so the operator sees a single conceptual badge across
/// dashboards.
/// </para>
/// </summary>
public sealed record ErpProviderHealthRow(
    string Provider,
    string State,
    int RecentSuccesses,
    int RecentFailures,
    int RecentProviderUnavailable,
    int ConsecutiveFailures,
    DateTime? LatestSuccessAtUtc,
    DateTime? LatestFailureAtUtc,
    string? LatestFailureReason);

/// <summary>
/// MS-BILL-ERP-004 — Stable, allow-listed provider-health states.
/// Persisted nowhere; derived deterministically from the recent
/// export window. Mirrors <c>BillingProviderHealthState</c> on the
/// browser-side so the same badge component renders both.
/// </summary>
public static class ErpProviderHealthState
{
    public const string Healthy = "Healthy";
    public const string Degraded = "Degraded";
    public const string Unavailable = "Unavailable";
    public const string Unknown = "Unknown";
}

/// <summary>
/// MS-BILL-ERP-004 — Filter / paging shape for the reconciliation
/// list endpoint. Tenant id is resolved server-side via
/// <c>ITenantContext</c>; never accepted from the wire.
/// </summary>
public sealed record ErpReconciliationListQuery(
    int Page,
    int PageSize,
    string? Status,
    string? Provider);
