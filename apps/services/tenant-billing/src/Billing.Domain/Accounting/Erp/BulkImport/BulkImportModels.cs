namespace Billing.Domain.Accounting.Erp.BulkImport;

/// <summary>
/// MS-BILL-ERP-006 — closed enum of per-row outcome buckets. The
/// preview UI renders one badge per row keyed off this value; the
/// commit step ONLY persists rows whose post-recheck classification
/// is <see cref="Valid"/> or <see cref="Warning"/>.
/// </summary>
public enum BulkImportRowClassification
{
    Valid = 0,
    Warning = 1,
    Rejected = 2,
}

/// <summary>
/// MS-BILL-ERP-006 — closed enum of per-row issue codes surfaced by
/// the validation preview and re-checked at commit time. Reuses the
/// ERP-005 single-row codes verbatim so the operator UX is
/// consistent between the per-customer remediation page and the
/// bulk import page; adds three bulk-only codes.
/// </summary>
public static class BulkImportRowIssueCode
{
    // ----- Bulk-only -----
    public const string MalformedCsvRow = nameof(MalformedCsvRow);
    public const string DuplicateBillingCustomerInUpload = nameof(DuplicateBillingCustomerInUpload);
    public const string DuplicateQuickBooksCustomerInUpload = nameof(DuplicateQuickBooksCustomerInUpload);
    public const string InvalidExportMode = nameof(InvalidExportMode);
    public const string MissingRequiredField = nameof(MissingRequiredField);

    // ----- Mirrors ERP-005 single-row codes -----
    public const string InvalidQuickBooksCustomerId = nameof(InvalidQuickBooksCustomerId);
    public const string BillingCustomerNotFound = nameof(BillingCustomerNotFound);
    public const string BillingCustomerAlreadyMapped = nameof(BillingCustomerAlreadyMapped);
    public const string QuickBooksCustomerAlreadyMapped = nameof(QuickBooksCustomerAlreadyMapped);
    public const string ExistingDisabledMapping = nameof(ExistingDisabledMapping);
}

/// <summary>
/// MS-BILL-ERP-006 — single per-row issue. The pair (code, message)
/// is part of the public wire contract; the message is operator-
/// facing English derived deterministically from <see cref="Code"/>
/// — never includes raw exception text.
/// </summary>
public sealed record BulkImportRowIssue(string Code, string Message);

/// <summary>
/// Result of the <see cref="IBulkMappingImportParser"/>. The parser
/// itself NEVER persists; it returns a flat sequence of parsed rows
/// keyed by their 1-indexed source line number plus any structural
/// errors that prevented further processing (missing header,
/// truncation due to row cap, etc.).
/// </summary>
public sealed record ParsedCsvDocument(
    IReadOnlyList<CsvParsedRow> Rows,
    IReadOnlyList<BulkImportRowIssue> DocumentIssues);

/// <summary>
/// One CSV row, parsed but NOT yet validated. Field-level shape
/// validation happens in the service layer so the parser stays a
/// pure transport-concern adapter.
/// </summary>
public sealed record CsvParsedRow(
    int LineNumber,
    string? BillingCustomerIdRaw,
    string? BillingCustomerName,
    string? QuickBooksCustomerIdRaw,
    string? QuickBooksDisplayName,
    string? ExportModeRaw,
    string? Notes,
    bool IsMalformed);

/// <summary>
/// One row that has passed the parser AND been classified by the
/// validator. <see cref="Issues"/> is empty for a clean
/// <see cref="BulkImportRowClassification.Valid"/> row.
/// </summary>
public sealed record ValidatedBulkImportRow(
    int LineNumber,
    Guid? BillingCustomerId,
    string? BillingCustomerName,
    string? QuickBooksCustomerId,
    string? QuickBooksDisplayName,
    string? ExportMode,
    string? Notes,
    BulkImportRowClassification Classification,
    IReadOnlyList<BulkImportRowIssue> Issues);

/// <summary>
/// Output of <see cref="IBulkMappingImportService.ValidateAsync"/>.
/// The <see cref="PreviewToken"/> is purely a correlation id for
/// the audit history; the commit step ALWAYS re-validates the
/// caller-supplied rows server-side (defence in depth against
/// TOCTOU between preview and commit).
/// </summary>
public sealed record BulkImportPreviewResult(
    Guid PreviewToken,
    int TotalRows,
    int ValidCount,
    int WarningCount,
    int RejectedCount,
    IReadOnlyList<ValidatedBulkImportRow> Rows,
    IReadOnlyList<BulkImportRowIssue> DocumentIssues);

/// <summary>
/// One row the operator has explicitly approved for commit. The
/// commit endpoint NEVER promotes preview rows automatically — the
/// client must echo the chosen rows back so the audit trail records
/// exactly what the operator confirmed.
/// </summary>
public sealed record BulkImportCommitRowCommand(
    int LineNumber,
    Guid BillingCustomerId,
    string QuickBooksCustomerId,
    string? QuickBooksDisplayName,
    string? ExportMode,
    string? Notes);

public sealed record BulkImportCommitCommand(
    Guid? PreviewToken,
    IReadOnlyList<BulkImportCommitRowCommand> Rows);

/// <summary>
/// Closed enum of per-row commit outcomes.
/// </summary>
public static class BulkImportCommitOutcome
{
    public const string Persisted = nameof(Persisted);
    public const string Conflict = nameof(Conflict);
    public const string Rejected = nameof(Rejected);
    public const string Failed = nameof(Failed);
}

public sealed record BulkImportCommitRowResult(
    int LineNumber,
    Guid BillingCustomerId,
    string QuickBooksCustomerId,
    string Outcome,
    Guid? MappingId,
    string? Error);

public sealed record BulkImportCommitResult(
    Guid HistoryId,
    int TotalRequested,
    int Persisted,
    int Conflicted,
    int Rejected,
    int Failed,
    IReadOnlyList<BulkImportCommitRowResult> Rows);

/// <summary>
/// Read projection for the import-history list view. Mirrors the
/// columns persisted on <see cref="BulkMappingImportHistory"/> so
/// the UI never sees the raw <c>SummaryJson</c> blob.
/// </summary>
public sealed record BulkImportHistorySnapshot(
    Guid Id,
    DateTime StartedAtUtc,
    DateTime CompletedAtUtc,
    string OperatorDisplayName,
    int TotalRows,
    int AcceptedRows,
    int WarningRows,
    int RejectedRows);

/// <summary>
/// Read projection for the CSV export endpoint. Built deterministically
/// from <see cref="QuickBooks.QuickBooksCustomerMapping"/> + the owning
/// Billing customer's display name.
/// </summary>
public sealed record BulkMappingExportRow(
    Guid BillingCustomerId,
    string BillingCustomerName,
    string QuickBooksCustomerId,
    string? QuickBooksDisplayName,
    string MappingStatus,
    string? ExportMode,
    string CreatedBy,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime? LastExportedAtUtc);
