namespace Billing.Domain.Accounting.Erp;

/// <summary>
/// MS-BILL-ERP-001 — Stable, allow-listed lifecycle states for an
/// accounting-export attempt. Persisted verbatim on
/// <c>accounting_exports.Status</c>; the BFF / UI render badges
/// off these literal strings.
/// </summary>
public static class AccountingExportStatus
{
    public const string Pending = "Pending";
    public const string Exported = "Exported";
    public const string Failed = "Failed";
    public const string ProviderUnavailable = "ProviderUnavailable";
    public const string Skipped = "Skipped";
    public const string Duplicate = "Duplicate";
}

/// <summary>
/// MS-BILL-ERP-001 — Allow-listed export "kinds". Today only the
/// monolithic <c>AccountingBatch</c> is implemented (invoices +
/// payments + adjustments + derived journal entries inside a
/// single window). Future kinds (e.g. <c>InvoicesOnly</c>,
/// <c>PaymentsOnly</c>) plug in by extending this allowlist and
/// the projection builder.
/// </summary>
public static class AccountingExportType
{
    public const string AccountingBatch = "AccountingBatch";
}

/// <summary>
/// MS-BILL-ERP-001 — Provider-result shape. Deterministic by
/// contract: the orchestrator persists the result verbatim on the
/// <c>accounting_exports</c> row, and the BFF / UI render the
/// status / failure reason directly.
/// </summary>
/// <param name="Success">
/// True iff <see cref="Status"/> is
/// <see cref="AccountingExportStatus.Exported"/> (other terminal
/// statuses set this to false).
/// </param>
/// <param name="Provider">
/// Lower-case provider name; MUST equal the
/// <see cref="IAccountingExportProvider.ProviderName"/> of the
/// provider that produced the result.
/// </param>
/// <param name="Status">
/// One of <see cref="AccountingExportStatus"/>.
/// </param>
/// <param name="ExternalReferenceId">
/// Provider-supplied external reference (e.g. QuickBooks
/// "JournalEntry/123"). Optional.
/// </param>
/// <param name="CorrelationId">
/// Server-generated correlation id, echoed back. Used to tie the
/// row in <c>accounting_exports</c> to provider-side traces.
/// </param>
/// <param name="FailureReason">
/// Human-readable, NON-PII failure reason. Surfaced verbatim in
/// the operator UI on Failed / ProviderUnavailable.
/// </param>
public sealed record AccountingExportProviderResult(
    bool Success,
    string Provider,
    string Status,
    string? ExternalReferenceId,
    string? CorrelationId,
    string? FailureReason)
{
    public static AccountingExportProviderResult Exported(
        string provider,
        string correlationId,
        string? externalReferenceId)
        => new(
            Success: true,
            Provider: provider,
            Status: AccountingExportStatus.Exported,
            ExternalReferenceId: externalReferenceId,
            CorrelationId: correlationId,
            FailureReason: null);

    public static AccountingExportProviderResult Failed(
        string provider,
        string correlationId,
        string failureReason)
        => new(
            Success: false,
            Provider: provider,
            Status: AccountingExportStatus.Failed,
            ExternalReferenceId: null,
            CorrelationId: correlationId,
            FailureReason: failureReason);

    public static AccountingExportProviderResult ProviderUnavailable(
        string provider,
        string correlationId,
        string failureReason)
        => new(
            Success: false,
            Provider: provider,
            Status: AccountingExportStatus.ProviderUnavailable,
            ExternalReferenceId: null,
            CorrelationId: correlationId,
            FailureReason: failureReason);

    public static AccountingExportProviderResult Skipped(
        string provider,
        string correlationId,
        string reason)
        => new(
            Success: false,
            Provider: provider,
            Status: AccountingExportStatus.Skipped,
            ExternalReferenceId: null,
            CorrelationId: correlationId,
            FailureReason: reason);
}

/// <summary>
/// MS-BILL-ERP-001 — Canonical, immutable, server-built payload
/// handed to a provider. Built by
/// <c>AccountingExportProjectionBuilder</c> from already-immutable
/// Billing rows (invoices, payments, adjustments) — there is NO new
/// accounting math here.
///
/// <para>
/// Carries no recipient PII, no rendered HTML, no provider secret.
/// CustomerId is a GUID; CustomerName is the same display name
/// surfaced everywhere else in Billing.
/// </para>
/// </summary>
public sealed record AccountingExportPayload(
    Guid TenantId,
    string ExportType,
    DateTime WindowFromUtc,
    DateTime WindowToUtc,
    string Currency,
    string CorrelationId,
    IReadOnlyList<AccountingExportInvoice> Invoices,
    IReadOnlyList<AccountingExportPayment> Payments,
    IReadOnlyList<AccountingExportAdjustment> Adjustments,
    IReadOnlyList<AccountingJournalEntry> JournalEntries);

/// <summary>
/// Invoice projection row (server-authoritative). Mirrors the
/// shape of <see cref="Billing.Domain.Reporting.AccountingSummaryRow"/>
/// where the fields overlap.
/// </summary>
public sealed record AccountingExportInvoice(
    Guid InvoiceId,
    string InvoiceNumber,
    Guid CustomerId,
    string CustomerName,
    string Status,
    DateTime IssueDate,
    DateTime DueDate,
    decimal Subtotal,
    decimal TaxAmount,
    decimal DiscountAmount,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal AdjustmentCreditSum,
    decimal AdjustmentDebitSum,
    decimal EffectiveTotal,
    decimal EffectiveOutstanding,
    string Currency,
    string? ExternalReference);

/// <summary>
/// Payment projection row. Mirrors the WRITE-007 payment report row.
/// </summary>
public sealed record AccountingExportPayment(
    Guid PaymentId,
    Guid InvoiceId,
    string InvoiceNumber,
    Guid CustomerId,
    decimal Amount,
    string Currency,
    string Method,
    string Status,
    string? TransactionReference,
    DateTime PaidAt,
    bool Reversed,
    DateTime? ReversedAt);

/// <summary>
/// Adjustment / credit-memo projection row.
/// </summary>
public sealed record AccountingExportAdjustment(
    Guid AdjustmentId,
    Guid InvoiceId,
    string InvoiceNumber,
    Guid CustomerId,
    string Type,
    decimal Amount,
    string Currency,
    string Reason,
    string? ReferenceNumber,
    DateTime CreatedAt);

/// <summary>
/// Derived double-entry journal projection. Built from invoices /
/// payments / adjustments by
/// <c>AccountingExportProjectionBuilder</c>. Account names are
/// canonical Billing labels — providers map them onto their own
/// chart of accounts.
/// </summary>
public sealed record AccountingJournalEntry(
    string EntryType,
    Guid SourceId,
    DateTime EntryDate,
    string DebitAccount,
    string CreditAccount,
    decimal Amount,
    string Currency,
    string Memo);

/// <summary>
/// MS-BILL-ERP-001 — Caller-supplied request shape (validated +
/// normalised at the controller). Carried into the orchestrator
/// for projection-builder + provider dispatch.
/// </summary>
// TB-MERGE-01 import fix: removed `sealed` so the existing
// TenantScopedAccountingExportRunRequest in AccountingExportService.cs can
// inherit (the archive shipped the derived type but kept the base sealed,
// which is a CS0509 build-blocker). No behavioural change.
public record AccountingExportRunRequest(
    string Provider,
    string ExportType,
    DateTime WindowFromUtc,
    DateTime WindowToUtc,
    string IdempotencyKey,
    string RequestedBy,
    string? Reason);

/// <summary>
/// MS-BILL-ERP-001 — Outcome bundle returned to the controller
/// (and surfaced verbatim to the operator UI). Combines the
/// persisted batch row with the provider result so the caller has
/// everything needed to render a status badge + failure reason
/// without a follow-up GET.
/// </summary>
public sealed record AccountingExportRunResult(
    Guid ExportId,
    string Provider,
    string ExportType,
    string Status,
    string? ExternalReferenceId,
    string CorrelationId,
    string? FailureReason,
    DateTime RequestedAtUtc,
    DateTime? CompletedAtUtc,
    int InvoiceCount,
    int PaymentCount,
    int AdjustmentCount,
    int JournalEntryCount,
    bool WasDuplicate);
