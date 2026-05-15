using Billing.Domain.Entities;

namespace Billing.Domain.Services;

/// <summary>
/// Page of payments plus the total matching count, used to compute total
/// pages on the API. Returned by <see cref="IPaymentService.ListPagedAsync"/>.
/// </summary>
public sealed record PaymentPage(IReadOnlyList<Payment> Items, int TotalCount);

/// <summary>
/// Aggregate view of an invoice's money state: how much has been recorded
/// against it, and what is still outstanding. Returned by
/// <see cref="IPaymentService.GetInvoicePaymentSummaryAsync"/>. Returns
/// <c>null</c> when the invoice does not exist or belongs to a different
/// tenant (no cross-tenant existence leak).
/// </summary>
public sealed record InvoicePaymentSummary(
    Guid InvoiceId,
    string InvoiceNumber,
    string InvoiceStatus,
    decimal InvoiceTotal,
    decimal TotalPaid,
    decimal BalanceDue,
    string Currency);

/// <summary>
/// MS-BILL-WRITE-002 — result of a successful payment reversal. Carries the
/// post-reversal payment row (now in <c>"Voided"</c> status with
/// <c>ReversedAt</c> + <c>ReversalReason</c> populated) and a fresh
/// <see cref="InvoicePaymentSummary"/> reflecting the recomputed paid total
/// and (potentially demoted) invoice status. Returning both lets the API
/// respond with everything the tenant portal needs to refresh its detail
/// view in one round trip — same shape as <c>RecordPayment</c>.
/// </summary>
public sealed record ReversePaymentResult(Payment Payment, InvoicePaymentSummary Invoice);

public interface IPaymentService
{
    /// <summary>
    /// Record a payment against an Issued (or PartiallyPaid / Overdue)
    /// invoice and recompute the invoice's status from the new paid total.
    /// All money rules (positive amount, currency match with invoice,
    /// no overpayment, lifecycle gate) are enforced inside an atomic
    /// transaction with a row lock on the invoice so concurrent attempts
    /// cannot collectively overpay. Throws typed exceptions for the various
    /// failure modes (see <see cref="InvoiceNotFoundException"/>,
    /// <see cref="InvalidInvoicePaymentStateException"/>,
    /// <see cref="OverpaymentException"/>,
    /// <see cref="CurrencyMismatchException"/>,
    /// <see cref="InvalidPaymentAmountException"/>,
    /// <see cref="DuplicatePaymentReferenceException"/>) — all derive from
    /// <see cref="InvalidOperationException"/> for back-compat with existing
    /// callers.
    /// </summary>
    Task<Payment> CreateAsync(
        Guid tenantId,
        Guid invoiceId,
        decimal amount,
        string currency,
        string method,
        string status,
        string? transactionReference,
        DateTime? paidAt,
        string? notes = null,
        CancellationToken ct = default);

    Task<Payment?> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    /// <summary>
    /// Backwards-compatible unfiltered list. Returns every payment for the
    /// tenant. New callers should prefer <see cref="ListPagedAsync"/>.
    /// </summary>
    Task<IReadOnlyList<Payment>> ListAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// Tenant-scoped, filtered, paginated payment list. Page is clamped to
    /// >= 1 and pageSize to [1, 100] (default 25). Date filters bound
    /// <c>PaidAt</c>; status / method are matched case-insensitively.
    /// </summary>
    Task<PaymentPage> ListPagedAsync(
        Guid tenantId,
        Guid? invoiceId,
        string? status,
        string? method,
        DateTime? fromDate,
        DateTime? toDate,
        int page,
        int pageSize,
        CancellationToken ct = default);

    /// <summary>
    /// All payments recorded against a single invoice, ordered newest
    /// <c>PaidAt</c> first. Returns null when the invoice does not exist or
    /// belongs to a different tenant (so the controller can surface a clean
    /// 404 instead of a misleading empty 200).
    /// </summary>
    Task<IReadOnlyList<Payment>?> GetByInvoiceAsync(Guid tenantId, Guid invoiceId, CancellationToken ct = default);

    /// <summary>
    /// Aggregate money view of an invoice (total, total paid, balance due,
    /// current status). Returns null when the invoice does not exist or
    /// belongs to a different tenant.
    /// </summary>
    Task<InvoicePaymentSummary?> GetInvoicePaymentSummaryAsync(Guid tenantId, Guid invoiceId, CancellationToken ct = default);

    /// <summary>
    /// MS-BILL-WRITE-002 — reverse a previously-recorded manual payment.
    /// Flips the payment's lifecycle status from <c>"Recorded"</c> to
    /// <c>"Voided"</c> and appends the audit metadata (<c>ReversedAt</c> /
    /// <c>ReversalReason</c>) WITHOUT mutating any of the original financial
    /// fields (Amount, Currency, Method, PaidAt, TransactionReference,
    /// Notes, CreatedAt). The parent invoice's paid sum and lifecycle
    /// status are recomputed inside the same atomic transaction with a
    /// tenant-scoped row lock on the invoice.
    /// <para>
    /// Throws:
    /// <see cref="PaymentNotFoundException"/> when the id is unknown or
    /// belongs to a different tenant (no existence leak — same response
    /// in both cases);
    /// <see cref="PaymentAlreadyReversedException"/> when the payment is
    /// already <c>"Voided"</c> (reversal lifecycle is one-way);
    /// <see cref="PaymentNotReversibleException"/> when the payment is in
    /// any non-Recorded, non-Voided state (e.g. legacy <c>"Pending"</c>
    /// rows that pre-date the recorded-by-default contract);
    /// <see cref="InvalidReversalReasonException"/> when the reason is
    /// missing, blank, or exceeds the column-bounded length.
    /// </para>
    /// </summary>
    Task<ReversePaymentResult> ReverseAsync(
        Guid tenantId,
        Guid paymentId,
        string reason,
        CancellationToken ct = default);

    /// <summary>
    /// MS-BILL-WRITE-003 — update ONLY the <see cref="Payment.Notes"/> field
    /// on an existing payment. No financial field, status, lifecycle
    /// timestamp, or reversal audit field is touched — the operation is by
    /// construction safe relative to the immutable-financial-history
    /// guarantee enforced by <see cref="ReverseAsync"/>.
    /// <para>
    /// The <paramref name="notes"/> argument is normalised: <c>null</c>,
    /// the empty string, and any whitespace-only string all collapse to
    /// <c>null</c> (clear). A non-empty, non-whitespace value is trimmed
    /// before length validation. The bound mirrors
    /// <c>PaymentService.MaxNotesLength</c> (2000) and the EF column width.
    /// </para>
    /// <para>
    /// Notes are editable on BOTH <c>"Recorded"</c> and <c>"Voided"</c>
    /// payments — operators sometimes need to clarify reversal context
    /// after the fact. Cross-tenant probes surface as
    /// <see cref="PaymentNotFoundException"/> with no existence leak;
    /// length-bound violations surface as
    /// <see cref="InvalidPaymentNotesException"/>. Returns the updated
    /// payment.
    /// </para>
    /// </summary>
    Task<Payment> UpdateNotesAsync(
        Guid tenantId,
        Guid paymentId,
        string? notes,
        CancellationToken ct = default);
}
