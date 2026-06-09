using TenantBilling.Domain.Entities;

namespace TenantBilling.Domain.Services;

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
}
