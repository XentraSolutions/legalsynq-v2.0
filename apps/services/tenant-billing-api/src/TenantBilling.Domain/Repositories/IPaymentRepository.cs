using TenantBilling.Domain.Entities;

namespace TenantBilling.Domain.Repositories;

public interface IPaymentRepository
{
    Task<Payment> AddAsync(Payment payment, CancellationToken ct = default);

    /// <summary>
    /// Persist mutations to an existing payment row (e.g. status changes
    /// when a payment is voided in a future block). The supplied entity must
    /// already belong to the calling tenant — callers are responsible for
    /// loading it via a tenant-scoped read first.
    /// </summary>
    Task<Payment> UpdateAsync(Payment payment, CancellationToken ct = default);

    /// <summary>
    /// Returns the payment with the given id only if it belongs to the
    /// specified tenant. Cross-tenant or unknown ids return <c>null</c>.
    /// </summary>
    Task<Payment?> GetByIdForTenantAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    /// <summary>
    /// Tenant-scoped, unfiltered list of all payments. Retained for
    /// backwards compatibility; new callers should use
    /// <see cref="ListAsync"/>.
    /// </summary>
    Task<IReadOnlyList<Payment>> GetAllForTenantAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// Returns true if a payment with the given (TenantId, TransactionReference)
    /// already exists. Used to short-circuit duplicate webhook deliveries
    /// before they reach the unique-index enforcement at the database.
    /// Callers should only invoke this when transactionReference is non-null
    /// and non-whitespace.
    /// </summary>
    Task<bool> ExistsByTenantAndReferenceAsync(Guid tenantId, string transactionReference, CancellationToken ct = default);

    /// <summary>
    /// Tenant-scoped list of all payments recorded against a single invoice,
    /// ordered newest <c>PaidAt</c> first. Cross-tenant or unknown invoice
    /// ids return an empty list (no existence leak).
    /// </summary>
    Task<IReadOnlyList<Payment>> GetByInvoiceIdAsync(Guid tenantId, Guid invoiceId, CancellationToken ct = default);

    /// <summary>
    /// Tenant-scoped, paginated, filtered payment list. Sort is newest
    /// <c>PaidAt</c> first, then <c>CreatedAt</c>. Date filters bound
    /// <c>PaidAt</c>. Status / method comparisons are case-insensitive.
    /// </summary>
    Task<IReadOnlyList<Payment>> ListAsync(
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
    /// Total count of payments matching the same filters as
    /// <see cref="ListAsync"/>. Used to compute total pages on the API.
    /// </summary>
    Task<int> CountAsync(
        Guid tenantId,
        Guid? invoiceId,
        string? status,
        string? method,
        DateTime? fromDate,
        DateTime? toDate,
        CancellationToken ct = default);

    /// <summary>
    /// Sum of recorded (non-voided) payment amounts for an invoice owned by
    /// the calling tenant. Returns zero for unknown / cross-tenant invoice
    /// ids. This is the authoritative paid-total used by the balance and
    /// status-transition logic.
    /// </summary>
    Task<decimal> SumRecordedPaymentsForInvoiceAsync(Guid tenantId, Guid invoiceId, CancellationToken ct = default);

    /// <summary>
    /// STAT-B01: tenant-scoped list of every recorded (non-voided)
    /// payment whose parent invoice belongs to the given customer,
    /// ordered ascending by <see cref="Payment.PaidAt"/> then
    /// <see cref="Payment.Id"/>. Tenant scope is enforced both on the
    /// payment row AND through the join to the parent invoice's
    /// <c>CustomerId</c> — a payment whose parent invoice belongs to
    /// a different tenant cannot leak in. Returns an empty list when
    /// the customer is unknown or cross-tenant.
    /// </summary>
    Task<IReadOnlyList<Payment>> GetRecordedPaymentsForCustomerAsync(
        Guid tenantId,
        Guid customerId,
        CancellationToken ct = default);
}
