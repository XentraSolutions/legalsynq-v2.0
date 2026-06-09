using Billing.Domain.Projections;

namespace Billing.Domain.Services;

/// <summary>
/// MS-BILL-WRITE-006 — read-only accounting-summary projection
/// service. Single authoritative source for the effective-balance
/// math used by the tenant-admin invoice detail UI and (in future
/// prompts) by exports / reconciliation / statement renderers.
///
/// The service has NO write surface — it is a pure projection over
/// the existing immutable ledgers (invoice totals, append-only
/// adjustments, non-voided payments). Tenant scoping is enforced
/// at the repository call sites; cross-tenant or unknown invoice
/// ids return <c>null</c> (which the API surfaces as 404 with no
/// existence leak — same shape as <see cref="IInvoiceService.GetAsync"/>).
/// </summary>
public interface IInvoiceAccountingSummaryService
{
    /// <summary>
    /// Compute the accounting summary for a single invoice owned by
    /// the calling tenant. Returns <c>null</c> when the invoice does
    /// not exist or belongs to a different tenant.
    /// </summary>
    Task<InvoiceAccountingSummary?> GetAsync(
        Guid tenantId, Guid invoiceId, CancellationToken ct = default);
}
