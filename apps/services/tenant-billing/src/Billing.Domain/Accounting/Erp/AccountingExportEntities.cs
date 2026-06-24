namespace Billing.Domain.Accounting.Erp;

/// <summary>
/// MS-BILL-ERP-001 — Persisted lifecycle row for one export attempt.
///
/// <para>
/// Append-safe by contract: a row is INSERTed when the run starts
/// (Status=Pending) and UPDATEd exactly once when the provider
/// returns. There is NEVER a second mutation of the same row;
/// retries create a fresh row (and may surface as Status=Duplicate
/// against an existing successful row — see
/// <see cref="Fingerprint"/>).
/// </para>
///
/// <para>
/// Carries no recipient PII and no provider secret.
/// <see cref="PayloadJson"/> is the server-built immutable
/// projection bundle (invoices / payments / adjustments / journal
/// entries) and is itself derived from append-only Billing rows.
/// </para>
/// </summary>
public sealed class AccountingExport
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    /// <summary>
    /// Lower-case provider name (matches
    /// <see cref="IAccountingExportProvider.ProviderName"/>).
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// One of <see cref="AccountingExportType"/>.
    /// </summary>
    public string ExportType { get; set; } = string.Empty;

    public DateTime WindowFromUtc { get; set; }
    public DateTime WindowToUtc { get; set; }

    /// <summary>
    /// One of <see cref="AccountingExportStatus"/>.
    /// </summary>
    public string Status { get; set; } = AccountingExportStatus.Pending;

    /// <summary>
    /// Server-generated correlation id (one per attempt).
    /// </summary>
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>
    /// Provider-supplied external reference; nullable until the
    /// provider returns one.
    /// </summary>
    public string? ExternalReferenceId { get; set; }

    /// <summary>
    /// IDM session-derived display name of the operator who
    /// triggered the run. NEVER an email — the BFF strips PII
    /// before forwarding.
    /// </summary>
    public string RequestedBy { get; set; } = string.Empty;

    public DateTime RequestedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }

    public string? FailureReason { get; set; }

    /// <summary>
    /// Caller-supplied <c>Idempotency-Key</c> header. Persisted on
    /// the row so a retry of the same key surfaces a deterministic
    /// duplicate response (see also <see cref="Fingerprint"/>).
    /// </summary>
    public string IdempotencyKey { get; set; } = string.Empty;

    /// <summary>
    /// Deterministic fingerprint of
    /// <c>tenantId | provider | exportType | windowFromUtc | windowToUtc</c>
    /// (sha256, hex). The application-level dedupe key.
    /// </summary>
    public string Fingerprint { get; set; } = string.Empty;

    public int InvoiceCount { get; set; }
    public int PaymentCount { get; set; }
    public int AdjustmentCount { get; set; }
    public int JournalEntryCount { get; set; }

    /// <summary>
    /// Operator-supplied free-text reason for the export run.
    /// Capped at 1000 chars by the controller; rendered back in
    /// the history table for audit visibility.
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Server-built immutable payload as JSON (LONGTEXT on MySQL).
    /// Populated for every successful run regardless of provider —
    /// the JSON is the operator's evidence of what was sent and
    /// the source of truth for any future replay.
    /// </summary>
    public string? PayloadJson { get; set; }
}
