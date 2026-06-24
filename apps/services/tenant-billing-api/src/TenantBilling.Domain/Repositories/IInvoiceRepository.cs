using TenantBilling.Domain.Entities;

namespace TenantBilling.Domain.Repositories;

public interface IInvoiceRepository
{
    Task<Invoice> AddAsync(Invoice invoice, CancellationToken ct = default);

    /// <summary>
    /// Returns the invoice with the given id only if it belongs to the
    /// specified tenant. Includes line items, payments, and refunds for
    /// callers that need the full aggregate. Cross-tenant or unknown ids
    /// return <c>null</c>.
    /// </summary>
    Task<Invoice?> GetByIdForTenantAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<Invoice>> GetAllForTenantAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// Tenant-scoped, paginated, filtered invoice list. Search matches
    /// <see cref="Invoice.InvoiceNumber"/> or <see cref="Invoice.Notes"/>;
    /// status is matched case-insensitively; date filters bound
    /// <see cref="Invoice.IssueDate"/>; sort is newest <c>CreatedAt</c>
    /// first then <c>InvoiceNumber</c>. Includes line items so the API
    /// list response can render aggregate-level data without a follow-up
    /// fetch.
    /// </summary>
    Task<IReadOnlyList<Invoice>> ListAsync(
        Guid tenantId,
        string? search,
        string? status,
        Guid? customerId,
        DateTime? fromDate,
        DateTime? toDate,
        int page,
        int pageSize,
        CancellationToken ct = default);

    /// <summary>
    /// Total count of invoices matching the same filters as
    /// <see cref="ListAsync"/>. Used to compute total pages on the API.
    /// </summary>
    Task<int> CountAsync(
        Guid tenantId,
        string? search,
        string? status,
        Guid? customerId,
        DateTime? fromDate,
        DateTime? toDate,
        CancellationToken ct = default);

    /// <summary>
    /// Returns true when the tenant already has an invoice with the given
    /// number, optionally excluding a specific invoice id (used by future
    /// update flows; today the create path passes <c>null</c>).
    /// </summary>
    Task<bool> ExistsByTenantAndNumberAsync(
        Guid tenantId,
        string invoiceNumber,
        Guid? excludingInvoiceId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the latest existing invoice number for the tenant in the
    /// given calendar year (matches the <c>INV-YYYY-</c> prefix), or
    /// <c>null</c> if there are no invoices yet for that year. Drives the
    /// auto-numbering sequence in the service layer.
    /// </summary>
    Task<string?> GetLatestInvoiceNumberAsync(Guid tenantId, int year, CancellationToken ct = default);

    /// <summary>
    /// Persist a status transition for an invoice owned by the given tenant.
    /// Returns the updated invoice (with line items + payments loaded), or
    /// null if the invoice no longer exists or belongs to a different tenant.
    /// Implementations must update <see cref="Invoice.Status"/>,
    /// <see cref="Invoice.UpdatedAt"/>, and (when supplied) the
    /// <see cref="Invoice.IssuedAt"/> timestamp.
    /// </summary>
    Task<Invoice?> UpdateStatusAsync(
        Guid tenantId,
        Guid invoiceId,
        string status,
        DateTime updatedAt,
        DateTime? issuedAt = null,
        CancellationToken ct = default);

    /// <summary>
    /// Find invoices eligible for transition into the Overdue status:
    /// status is currently Issued or PartiallyPaid, and the due date has
    /// passed at <paramref name="nowUtc"/>. When <paramref name="tenantId"/>
    /// is provided the search is scoped to that tenant; otherwise the
    /// search runs across all tenants (used by the hosted scheduler). Caps
    /// the result at <paramref name="take"/> rows ordered oldest-due first
    /// so the longest-overdue invoices are processed first when a batch
    /// runs short.
    /// </summary>
    Task<IReadOnlyList<Invoice>> GetInvoicesEligibleForOverdueAsync(
        Guid? tenantId,
        DateTime nowUtc,
        int take,
        CancellationToken ct = default);

    /// <summary>
    /// Conditional, race-safe transition of a single invoice into Overdue.
    /// The update happens only when the persisted row still satisfies the
    /// eligibility predicate at write time:
    ///   <c>Status in (Issued, PartiallyPaid)</c> AND the due date is
    ///   strictly before the date portion of <paramref name="nowUtc"/>.
    /// This closes the read-validate-write TOCTOU window in the batch
    /// sweep — if a concurrent payment moves the invoice to Paid (or
    /// another writer voids it) between the eligibility query and this
    /// call, the conditional check fails and the method returns
    /// <c>null</c> instead of overwriting the newer status.
    /// Returns the updated invoice (with line items + payments loaded)
    /// when the row was transitioned; <c>null</c> when the row no longer
    /// exists, belongs to another tenant, or no longer matches the
    /// predicate.
    /// </summary>
    Task<Invoice?> TryMarkOverdueAsync(
        Guid tenantId,
        Guid invoiceId,
        DateTime nowUtc,
        CancellationToken ct = default);

    /// <summary>
    /// INV-TPL-02: stamp the supplied template's branding snapshot
    /// onto an existing invoice and persist. Used by the issue-path
    /// "ensure stamp" flow (an invoice that was created before its
    /// tenant configured a default template, or before the caller
    /// passed an explicit id, gets snapshotted on its way to Issued).
    ///
    /// Implementations must:
    /// <list type="bullet">
    ///   <item>Load the invoice tenant-scoped (so a cross-tenant or
    ///     missing id returns <c>null</c> with no existence leak).</item>
    ///   <item>Honour the idempotency guard
    ///     <see cref="IInvoiceTemplateStampingService.EnsureStampedInvoice"/>
    ///     so an already-stamped invoice is left untouched.</item>
    ///   <item>Save and return the updated invoice via the standard
    ///     read path so callers get the consistent
    ///     <c>GetByIdForTenantAsync</c> shape.</item>
    /// </list>
    /// Returns <c>null</c> when the invoice does not exist or
    /// belongs to another tenant.
    /// </summary>
    Task<Invoice?> ApplyStampAsync(
        Guid tenantId,
        Guid invoiceId,
        InvoiceTemplate template,
        DateTime stampedAtUtc,
        CancellationToken ct = default);

    /// <summary>
    /// STAT-B01: tenant-scoped list of every invoice belonging to a
    /// single customer, regardless of status, ordered ascending by
    /// <see cref="Invoice.IssueDate"/> then <see cref="Invoice.InvoiceNumber"/>.
    /// Returns an empty list when the customer is unknown or belongs
    /// to a different tenant — no existence leak. Drives the customer
    /// statement engine, which partitions the result into pre-period,
    /// in-period, and outstanding subsets in memory. Line items are
    /// not loaded (the engine only needs aggregate totals).
    /// </summary>
    Task<IReadOnlyList<Invoice>> GetInvoicesForCustomerAsync(
        Guid tenantId,
        Guid customerId,
        CancellationToken ct = default);
}
