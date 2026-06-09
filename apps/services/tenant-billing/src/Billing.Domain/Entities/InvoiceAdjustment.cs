namespace Billing.Domain.Entities;

/// <summary>
/// MS-BILL-WRITE-005 — append-only invoice adjustment ledger entry.
///
/// Represents a Credit or Debit memo applied to an invoice AFTER its
/// original totals were finalised. Adjustments are NEVER updated or
/// deleted: a mistake is corrected by appending a compensating
/// adjustment of the opposite type. The effective balance of the
/// invoice is computed on demand from
/// <c>Invoice.TotalAmount + sum(Debit) - sum(Credit) - sum(Payments)</c> —
/// the original <see cref="Invoice.TotalAmount"/>, the original
/// <see cref="Invoice.LineItems"/>, and the original
/// <see cref="Invoice.Payments"/> are never mutated by the adjustment
/// flow.
///
/// Two types are accepted (case-insensitive on input, normalised to
/// PascalCase on insert):
///   - <c>"Credit"</c>  — reduces the customer's obligation
///                        (e.g. billing correction, goodwill, mis-billing).
///   - <c>"Debit"</c>   — increases the customer's obligation
///                        (e.g. late fee, surcharge, post-issue line item).
///
/// Tenant scoping: every adjustment carries the owning tenant id and
/// the customer id (denormalised from the parent invoice at insert
/// time so tenant-scoped queries do not have to join through the
/// invoice). Cross-tenant reads are guarded at the repository layer.
/// </summary>
public class InvoiceAdjustment
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid InvoiceId { get; set; }
    public Guid CustomerId { get; set; }

    /// <summary>
    /// Adjustment kind — see class summary. Stored as PascalCase
    /// string for forward-compatibility with future kinds (e.g.
    /// <c>"Writeoff"</c>) without an enum migration.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Adjustment amount in the parent invoice's currency. Always
    /// positive — the sign is implied by <see cref="Type"/>.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Snapshot of the parent invoice's currency at the moment the
    /// adjustment was applied. Allows the adjustment to remain valid
    /// even if the parent invoice's currency is somehow renamed in
    /// the future (it cannot be today, but the snapshot future-proofs
    /// the ledger).
    /// </summary>
    public string Currency { get; set; } = "USD";

    /// <summary>
    /// Mandatory operator-supplied free-text reason (1–1000 chars
    /// after trim). Captured in the BFF audit log; only the length
    /// is captured in the Billing.Api log line.
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Optional operator-supplied reference (e.g. internal credit
    /// memo number, ticket id). Bounded to 64 chars after trim.
    /// </summary>
    public string? ReferenceNumber { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Optional operator identifier. Populated when the calling
    /// session resolves to a known user (the BFF forwards the
    /// resolved user id; if not available the column stays null —
    /// the audit log still has the actor via the BFF httpLogger).
    /// </summary>
    public string? CreatedBy { get; set; }

    public Invoice? Invoice { get; set; }
}
