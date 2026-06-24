using Billing.Domain.Entities;

namespace Billing.Domain.Repositories;

/// <summary>
/// MS-BILL-WRITE-005 — append-only invoice adjustment repository.
///
/// Append-only by contract: the interface intentionally exposes NO
/// <c>UpdateAsync</c> or <c>DeleteAsync</c> method. A wrong
/// adjustment is corrected by appending a compensating adjustment of
/// the opposite type, NOT by mutating the existing row. Read methods
/// are tenant-scoped at the call site (the service layer always
/// passes <paramref name="tenantId"/> through from
/// <c>ITenantContext.TenantId</c>).
/// </summary>
public interface IInvoiceAdjustmentRepository
{
    /// <summary>
    /// Insert a new adjustment row. The caller is responsible for
    /// validating the row (positive amount, valid type, tenant
    /// scoping, over-credit guard) BEFORE calling this method —
    /// repository-level validation is intentionally minimal so the
    /// service layer remains the single source of truth.
    /// </summary>
    Task<InvoiceAdjustment> AddAsync(InvoiceAdjustment adjustment, CancellationToken ct = default);

    /// <summary>
    /// Tenant-scoped fetch by id. Returns null if the id does not
    /// exist OR if it belongs to a different tenant — the same null
    /// shape so a cross-tenant probe surfaces as a generic 404 with
    /// no existence leak.
    /// </summary>
    Task<InvoiceAdjustment?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    /// <summary>
    /// Tenant-scoped list for a single invoice, oldest first
    /// (chronological audit order).
    /// </summary>
    Task<IReadOnlyList<InvoiceAdjustment>> GetByInvoiceAsync(
        Guid tenantId, Guid invoiceId, CancellationToken ct = default);

    /// <summary>
    /// Tenant-scoped sum of adjustment amounts for an invoice,
    /// returned as a (creditSum, debitSum) tuple. Zero-zero when no
    /// adjustments exist.
    /// </summary>
    Task<(decimal CreditSum, decimal DebitSum)> SumByInvoiceAsync(
        Guid tenantId, Guid invoiceId, CancellationToken ct = default);
}
