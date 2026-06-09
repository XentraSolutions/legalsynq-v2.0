using Billing.Domain.Accounting.Erp.Governance;

namespace Billing.Api.Contracts;

/// <summary>
/// MS-BILL-ERP-007 — Wire-format contracts for the tenant-admin
/// governance analytics surface. None of these types echo a
/// tenant id, raw fingerprint, QBO token, refresh token, client
/// secret, realm id, or recipient PII to the browser. Every
/// shape mirrors the C# domain record 1:1 so the BFF / UI can
/// render badges directly off the literal status strings.
/// </summary>
public sealed record ErpGovernanceSummaryResponse(
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
    DateTime ObservedAtUtc)
{
    public static ErpGovernanceSummaryResponse From(ErpGovernanceSummary s)
        => new(
            s.WindowDays,
            s.WindowFromUtc,
            s.WindowToUtc,
            s.TotalExports,
            s.ExportedCount,
            s.FailedCount,
            s.ProviderUnavailableCount,
            s.DuplicateCount,
            s.SkippedCount,
            s.PendingCount,
            s.ExportSuccessRatePercent,
            s.FailedExportRatePercent,
            s.ReplayRatePercent,
            s.ActiveCustomerCount,
            s.ActiveMappingCount,
            s.InactiveMappingCount,
            s.UnresolvedMappingCount,
            s.MappingCoveragePercent,
            s.AverageRemediationAgeDays,
            s.InvoiceFirstMappingCount,
            s.InvoiceFirstAdoptionPercent,
            s.RecentGovernanceFailureCount,
            s.ObservedAtUtc);
}

public sealed record ErpExportTrendBucketResponse(
    DateTime BucketDateUtc,
    string Provider,
    string ExportType,
    int TotalCount,
    int ExportedCount,
    int FailedCount,
    int ProviderUnavailableCount,
    int DuplicateCount)
{
    public static ErpExportTrendBucketResponse From(ErpExportTrendBucket b)
        => new(
            b.BucketDateUtc,
            b.Provider,
            b.ExportType,
            b.TotalCount,
            b.ExportedCount,
            b.FailedCount,
            b.ProviderUnavailableCount,
            b.DuplicateCount);
}

public sealed record ErpExportTrendResponse(
    int WindowDays,
    DateTime WindowFromUtc,
    DateTime WindowToUtc,
    IReadOnlyList<ErpExportTrendBucketResponse> Buckets)
{
    public static ErpExportTrendResponse From(ErpExportTrendResult r)
        => new(
            r.WindowDays,
            r.WindowFromUtc,
            r.WindowToUtc,
            r.Buckets.Select(ErpExportTrendBucketResponse.From).ToList());
}

public sealed record RemediationAgingRowResponse(
    Guid BillingCustomerId,
    string BillingCustomerName,
    DateTime CustomerCreatedAtUtc,
    int AgeDays,
    DateTime? LastInvoiceDate,
    string? ExistingMappingStatus,
    string ExportBlockedReason)
{
    public static RemediationAgingRowResponse From(RemediationAgingRow r)
        => new(
            r.BillingCustomerId,
            r.BillingCustomerName,
            r.CustomerCreatedAtUtc,
            r.AgeDays,
            r.LastInvoiceDate,
            r.ExistingMappingStatus,
            r.ExportBlockedReason);
}

public sealed record RemediationVelocityResponse(
    int WindowDays,
    int MappingsResolvedInWindow,
    int BulkImportsInWindow,
    int BulkImportAcceptedRowsInWindow)
{
    public static RemediationVelocityResponse From(RemediationVelocity v)
        => new(
            v.WindowDays,
            v.MappingsResolvedInWindow,
            v.BulkImportsInWindow,
            v.BulkImportAcceptedRowsInWindow);
}

public sealed record RemediationAgingResponse(
    int UnresolvedCount,
    int OldestAgeDays,
    decimal AverageAgeDays,
    int StaleMappingCount,
    int StaleWindowDays,
    RemediationVelocityResponse Velocity,
    IReadOnlyList<RemediationAgingRowResponse> Oldest,
    DateTime ObservedAtUtc)
{
    public static RemediationAgingResponse From(RemediationAgingResult r)
        => new(
            r.UnresolvedCount,
            r.OldestAgeDays,
            r.AverageAgeDays,
            r.StaleMappingCount,
            r.StaleWindowDays,
            RemediationVelocityResponse.From(r.Velocity),
            r.Oldest.Select(RemediationAgingRowResponse.From).ToList(),
            r.ObservedAtUtc);
}

public sealed record GovernanceAuditEntryResponse(
    DateTime TimestampUtc,
    string ActionType,
    string Operator,
    string TargetEntityType,
    string TargetEntityId,
    string Result,
    string? CorrelationId,
    string? Detail)
{
    public static GovernanceAuditEntryResponse From(GovernanceAuditEntry e)
        => new(
            e.TimestampUtc,
            e.ActionType,
            e.Operator,
            e.TargetEntityType,
            e.TargetEntityId,
            e.Result,
            e.CorrelationId,
            e.Detail);
}

public sealed record GovernanceAuditResponse(
    int Page,
    int PageSize,
    int TotalCount,
    int WindowDays,
    DateTime WindowFromUtc,
    DateTime WindowToUtc,
    IReadOnlyList<GovernanceAuditEntryResponse> Entries)
{
    public static GovernanceAuditResponse From(GovernanceAuditResult r)
        => new(
            r.Page,
            r.PageSize,
            r.TotalCount,
            r.WindowDays,
            r.WindowFromUtc,
            r.WindowToUtc,
            r.Entries.Select(GovernanceAuditEntryResponse.From).ToList());
}

public sealed record DriftFingerprintRowResponse(
    string FingerprintShort,
    string Provider,
    string ExportType,
    int Occurrences,
    DateTime LastSeenAtUtc,
    string? LastFailureReason)
{
    public static DriftFingerprintRowResponse From(DriftFingerprintRow r)
        => new(
            r.FingerprintShort,
            r.Provider,
            r.ExportType,
            r.Occurrences,
            r.LastSeenAtUtc,
            r.LastFailureReason);
}

public sealed record DriftIndicatorResponse(
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
    IReadOnlyList<DriftFingerprintRowResponse> RepeatedFailures,
    IReadOnlyList<DriftFingerprintRowResponse> ReplayHeavy,
    DateTime ObservedAtUtc)
{
    public static DriftIndicatorResponse From(DriftIndicatorResult r)
        => new(
            r.WindowDays,
            r.WindowFromUtc,
            r.WindowToUtc,
            r.RepeatedFailureCount,
            r.StaleMappingCount,
            r.StaleWindowDays,
            r.ReplayHeavyCount,
            r.UnresolvedMappingCount,
            r.LastGovernanceFailureReason,
            r.LastGovernanceFailureAtUtc,
            r.RepeatedFailures.Select(DriftFingerprintRowResponse.From).ToList(),
            r.ReplayHeavy.Select(DriftFingerprintRowResponse.From).ToList(),
            r.ObservedAtUtc);
}
