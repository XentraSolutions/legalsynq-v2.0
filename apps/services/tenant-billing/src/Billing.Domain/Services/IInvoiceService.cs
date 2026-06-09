using Billing.Domain.Entities;

namespace Billing.Domain.Services;

public record NewInvoiceLine(string Description, int Quantity, decimal UnitPrice);

/// <summary>
/// Page of invoices plus the total matching count, used to compute total
/// pages on the API. Returned by <see cref="IInvoiceService.ListPagedAsync"/>.
/// </summary>
public sealed record InvoicePage(IReadOnlyList<Invoice> Items, int TotalCount);

public interface IInvoiceService
{
    /// <summary>
    /// Create a new <c>Draft</c> invoice for a tenant. Validates the
    /// customer is active and tenant-owned, the line items are well formed,
    /// the date and money invariants hold, and the invoice number is unique
    /// within the tenant. When <paramref name="invoiceNumber"/> is null or
    /// blank, a tenant-scoped <c>INV-YYYY-000001</c> sequence number is
    /// auto-generated. Money fields are rounded to 2dp away-from-zero.
    /// </summary>
    Task<Invoice> CreateAsync(
        Guid tenantId,
        Guid customerId,
        string? invoiceNumber,
        DateTime issueDate,
        DateTime dueDate,
        string currency,
        string? notes,
        IReadOnlyList<NewInvoiceLine> lines,
        decimal taxAmount,
        decimal discountAmount = 0m,
        // INV-TPL-02: optional pre-resolved template. The controller
        // resolves it once via IInvoiceTemplateSelectionService and
        // passes it through so the service stamps the snapshot in the
        // same insert that creates the invoice. Null = no template
        // ⇒ no snapshot ⇒ unstamped invoice (also valid).
        InvoiceTemplate? template = null,
        CancellationToken ct = default);

    Task<Invoice?> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    /// <summary>
    /// Backwards-compatible unfiltered list. Returns every invoice for the
    /// tenant. New callers should prefer <see cref="ListPagedAsync"/>.
    /// </summary>
    Task<IReadOnlyList<Invoice>> ListAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// Tenant-scoped, filtered, paginated list. Page is clamped to >= 1 and
    /// pageSize to [1, 100] (default 25). Search matches invoice number or
    /// notes; status is matched case-insensitively; date filters bound
    /// IssueDate.
    /// </summary>
    Task<InvoicePage> ListPagedAsync(
        Guid tenantId,
        string? search,
        string? status,
        Guid? customerId,
        DateTime? fromDate,
        DateTime? toDate,
        int page,
        int pageSize,
        CancellationToken ct = default);

    /// <summary>Transition Draft → Issued for an invoice owned by the tenant.</summary>
    Task<Invoice?> IssueAsync(Guid tenantId, Guid invoiceId, CancellationToken ct = default);

    /// <summary>
    /// Transition Draft/Issued/Overdue → Voided. Rejects voiding when any
    /// payments have been recorded against the invoice (irreversible side
    /// effects must be reconciled before void). Scoped to the calling tenant.
    /// </summary>
    Task<Invoice?> VoidAsync(Guid tenantId, Guid invoiceId, CancellationToken ct = default);

    /// <summary>
    /// Recompute an invoice's status from its payments and DueDate. No-op for
    /// Draft, Voided, and Refunded. Scoped to the calling tenant.
    /// </summary>
    Task<Invoice?> ReevaluateAsync(Guid tenantId, Guid invoiceId, CancellationToken ct = default);

    /// <summary>
    /// Transition Issued or PartiallyPaid → Overdue for a single invoice
    /// owned by the tenant. Returns null when the invoice does not exist or
    /// belongs to a different tenant. Throws
    /// <see cref="InvalidInvoiceTransitionException"/> when the invoice is
    /// in any other status, and
    /// <see cref="InvalidInvoiceStateException"/> when the due date has not
    /// passed yet.
    /// </summary>
    Task<Invoice?> MarkOverdueAsync(Guid tenantId, Guid invoiceId, CancellationToken ct = default);

    /// <summary>
    /// Find invoices whose status is Issued or PartiallyPaid and whose due
    /// date has passed at <paramref name="nowUtc"/>, then transition each
    /// into Overdue. When <paramref name="tenantId"/> is supplied the sweep
    /// is scoped to that tenant (operator-triggered batch); when null it
    /// runs across every tenant (hosted scheduler). Per-invoice failures
    /// are isolated (one bad row does not abort the batch) and surfaced in
    /// the returned summary. <paramref name="take"/> caps the batch size so
    /// a single run cannot dominate the database.
    /// </summary>
    Task<OverdueBatchResult> MarkEligibleOverdueAsync(
        Guid? tenantId,
        DateTime nowUtc,
        int take,
        CancellationToken ct = default);

    /// <summary>
    /// MS-BILL-WRITE-004 — Unified, audit-stamped lifecycle transition for
    /// the tenant-admin "Change status" workflow. Validates the requested
    /// (current → target) edge against the centralised
    /// <see cref="InvoiceLifecycleService"/> graph, then dispatches to the
    /// existing per-action method that owns the operational guards
    /// (<see cref="IssueAsync"/>, <see cref="VoidAsync"/>,
    /// <see cref="MarkOverdueAsync"/>, <see cref="ReevaluateAsync"/>) so
    /// the legal-transition matrix has exactly one source of truth.
    ///
    /// Accounting safety
    /// -----------------
    /// When <paramref name="targetStatus"/> is <c>Paid</c>, this method
    /// pre-checks that <c>sum(payments) &gt;= invoice.TotalAmount</c>; if
    /// the balance is non-zero the call throws
    /// <see cref="InvalidInvoiceStateException"/> WITHOUT mutating the
    /// invoice. Only when the balance is fully covered do we delegate to
    /// <see cref="ReevaluateAsync"/> (which lands the row on Paid via the
    /// canonical <c>InvoiceStatus.ComputeStatus</c> path). This keeps Paid
    /// from ever being reached through a manual "force flip" — the only
    /// way the status flips to Paid is when payments actually cover it.
    ///
    /// <paramref name="reason"/> is mandatory (1–1000 chars after trim) and
    /// is captured in the structured audit log on both success and failure.
    /// Returns null when the invoice does not exist or belongs to a
    /// different tenant — same shape as <see cref="GetAsync"/> so a
    /// cross-tenant probe surfaces as a generic 404 with no existence leak.
    /// </summary>
    Task<InvoiceTransitionResult?> TransitionAsync(
        Guid tenantId,
        Guid invoiceId,
        string targetStatus,
        string reason,
        CancellationToken ct = default);

    /// <summary>
    /// Record a refund against an invoice and transition its status. Only
    /// invoices currently in Paid or PartiallyRefunded may be refunded; the
    /// cumulative refunded amount may not exceed what was paid; currency
    /// must match. Returns null if the invoice does not exist or belongs to
    /// a different tenant (no cross-tenant existence leak).
    ///
    /// On success the invoice transitions to PartiallyRefunded (when not yet
    /// fully refunded) or Refunded (when the cumulative refunds equal the
    /// paid total).
    /// </summary>
    Task<RefundResult?> RefundAsync(
        Guid tenantId,
        Guid invoiceId,
        decimal amount,
        string? currency,
        string? reason,
        DateTime? refundedAt,
        CancellationToken ct = default);
}

/// <summary>
/// The newly-recorded refund and the invoice in its post-refund state.
/// </summary>
public sealed record RefundResult(Refund Refund, Invoice Invoice);

/// <summary>
/// MS-BILL-WRITE-004 — return shape for
/// <see cref="IInvoiceService.TransitionAsync"/>. Carries the
/// previous status snapshot alongside the post-transition invoice
/// so the API can build an <c>InvoiceLifecycleResponse</c> in a
/// single tenant-scoped read+write round-trip (no second GetAsync
/// to recover the prior status).
/// </summary>
public sealed record InvoiceTransitionResult(
    string PreviousStatus,
    Invoice Invoice);

/// <summary>
/// Per-invoice failure entry for the overdue batch. Captures enough context
/// for an operator to retry or escalate without leaking exception types
/// across the API boundary.
/// </summary>
public sealed record OverdueBatchFailure(Guid TenantId, Guid InvoiceId, string Reason);

/// <summary>
/// Summary of an <see cref="IInvoiceService.MarkEligibleOverdueAsync"/> run.
/// <c>UpdatedCount + SkippedCount + FailedCount</c> equals the number of
/// candidates considered (not the eligible total in the database, which
/// may be larger than <c>take</c>). <c>SkippedCount</c> tracks invoices
/// that were eligible at list time but no longer matched the eligibility
/// predicate at write time — typically because a concurrent payment moved
/// them to Paid or another writer voided them. These are not failures;
/// the conditional update simply found a newer state and left it alone.
/// </summary>
public sealed record OverdueBatchResult(
    int UpdatedCount,
    int FailedCount,
    IReadOnlyList<OverdueBatchFailure> Failures,
    int SkippedCount = 0);
