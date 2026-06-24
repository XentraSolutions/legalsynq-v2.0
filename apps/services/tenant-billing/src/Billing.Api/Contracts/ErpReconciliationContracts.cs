using Billing.Domain.Accounting.Erp.Reconciliation;

namespace Billing.Api.Contracts;

/// <summary>
/// MS-BILL-ERP-004 — Wire shapes for the read-only reconciliation
/// dashboard. Mirror the domain DTOs 1:1 (no projection, no hidden
/// secret) so the operator UI can render them without an additional
/// translation layer. Browser-supplied <c>X-Tenant-Id</c> is never
/// trusted; tenant id flows from <c>ITenantContext</c> only.
/// </summary>
public sealed record ErpReconciliationSummaryResponse(
    int TotalExports,
    int ExportedCount,
    int FailedCount,
    int DuplicateCount,
    int ProviderUnavailableCount,
    int SkippedCount,
    int PendingCount,
    ErpExportDiagnosticResponse? LatestSuccessfulExport,
    ErpExportDiagnosticResponse? LatestFailedExport,
    int UnmappedActiveCustomerCount,
    int StaleMappingCount,
    int StaleWindowDays,
    DateTime ObservedAtUtc)
{
    public static ErpReconciliationSummaryResponse FromDomain(ErpReconciliationSummary s)
        => new(
            TotalExports: s.TotalExports,
            ExportedCount: s.ExportedCount,
            FailedCount: s.FailedCount,
            DuplicateCount: s.DuplicateCount,
            ProviderUnavailableCount: s.ProviderUnavailableCount,
            SkippedCount: s.SkippedCount,
            PendingCount: s.PendingCount,
            LatestSuccessfulExport: s.LatestSuccessfulExport is null
                ? null : ErpExportDiagnosticResponse.FromDomain(s.LatestSuccessfulExport),
            LatestFailedExport: s.LatestFailedExport is null
                ? null : ErpExportDiagnosticResponse.FromDomain(s.LatestFailedExport),
            UnmappedActiveCustomerCount: s.UnmappedActiveCustomerCount,
            StaleMappingCount: s.StaleMappingCount,
            StaleWindowDays: s.StaleWindowDays,
            ObservedAtUtc: s.ObservedAtUtc);
}

public sealed record ErpExportDiagnosticResponse(
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
    bool IsDuplicate)
{
    public static ErpExportDiagnosticResponse FromDomain(ErpExportDiagnostic d)
        => new(
            ExportId: d.ExportId,
            Provider: d.Provider,
            ExportType: d.ExportType,
            Status: d.Status,
            WindowFromUtc: d.WindowFromUtc,
            WindowToUtc: d.WindowToUtc,
            CorrelationId: d.CorrelationId,
            ExternalReferenceId: d.ExternalReferenceId,
            FailureReason: d.FailureReason,
            RecordCount: d.RecordCount,
            InvoiceCount: d.InvoiceCount,
            PaymentCount: d.PaymentCount,
            AdjustmentCount: d.AdjustmentCount,
            JournalEntryCount: d.JournalEntryCount,
            RequestedBy: d.RequestedBy,
            RequestedAtUtc: d.RequestedAtUtc,
            CompletedAtUtc: d.CompletedAtUtc,
            FingerprintShort: d.FingerprintShort,
            IsDuplicate: d.IsDuplicate);
}

public sealed record ErpExportDiagnosticListResponse(
    int Page,
    int PageSize,
    int Count,
    IReadOnlyList<ErpExportDiagnosticResponse> Items);

public sealed record ErpExportDiagnosticDetailResponse(
    ErpExportDiagnosticResponse Diagnostic,
    string? Reason,
    int SiblingsByFingerprint)
{
    public static ErpExportDiagnosticDetailResponse FromDomain(ErpExportDiagnosticDetail d)
        => new(
            Diagnostic: ErpExportDiagnosticResponse.FromDomain(d.Diagnostic),
            Reason: d.Reason,
            SiblingsByFingerprint: d.SiblingsByFingerprint);
}

public sealed record ErpMappingHealthResponse(
    int TotalMappings,
    int ActiveMappings,
    int InactiveMappings,
    int UnmappedActiveCustomerCount,
    int StaleMappingCount,
    int StaleWindowDays,
    ErpMappingHealthRowResponse? LatestExportedMapping,
    DateTime ObservedAtUtc)
{
    public static ErpMappingHealthResponse FromDomain(ErpMappingHealthSnapshot s)
        => new(
            TotalMappings: s.TotalMappings,
            ActiveMappings: s.ActiveMappings,
            InactiveMappings: s.InactiveMappings,
            UnmappedActiveCustomerCount: s.UnmappedActiveCustomerCount,
            StaleMappingCount: s.StaleMappingCount,
            StaleWindowDays: s.StaleWindowDays,
            LatestExportedMapping: s.LatestExportedMapping is null
                ? null : ErpMappingHealthRowResponse.FromDomain(s.LatestExportedMapping),
            ObservedAtUtc: s.ObservedAtUtc);
}

public sealed record ErpMappingHealthRowResponse(
    Guid Id,
    Guid BillingCustomerId,
    string QuickBooksCustomerId,
    string? QuickBooksDisplayName,
    string MappingStatus,
    DateTime? LastExportedAtUtc)
{
    public static ErpMappingHealthRowResponse FromDomain(ErpMappingHealthRow r)
        => new(
            Id: r.Id,
            BillingCustomerId: r.BillingCustomerId,
            QuickBooksCustomerId: r.QuickBooksCustomerId,
            QuickBooksDisplayName: r.QuickBooksDisplayName,
            MappingStatus: r.MappingStatus,
            LastExportedAtUtc: r.LastExportedAtUtc);
}

public sealed record ErpProviderHealthResponse(
    int WindowSeconds,
    DateTime ObservedAtUtc,
    IReadOnlyList<ErpProviderHealthRowResponse> Providers)
{
    public static ErpProviderHealthResponse FromDomain(ErpProviderHealthSnapshot s)
        => new(
            WindowSeconds: s.WindowSeconds,
            ObservedAtUtc: s.ObservedAtUtc,
            Providers: s.Providers.Select(ErpProviderHealthRowResponse.FromDomain).ToList());
}

public sealed record ErpProviderHealthRowResponse(
    string Provider,
    string State,
    int RecentSuccesses,
    int RecentFailures,
    int RecentProviderUnavailable,
    int ConsecutiveFailures,
    DateTime? LatestSuccessAtUtc,
    DateTime? LatestFailureAtUtc,
    string? LatestFailureReason)
{
    public static ErpProviderHealthRowResponse FromDomain(ErpProviderHealthRow r)
        => new(
            Provider: r.Provider,
            State: r.State,
            RecentSuccesses: r.RecentSuccesses,
            RecentFailures: r.RecentFailures,
            RecentProviderUnavailable: r.RecentProviderUnavailable,
            ConsecutiveFailures: r.ConsecutiveFailures,
            LatestSuccessAtUtc: r.LatestSuccessAtUtc,
            LatestFailureAtUtc: r.LatestFailureAtUtc,
            LatestFailureReason: r.LatestFailureReason);
}
