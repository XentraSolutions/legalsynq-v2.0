using Billing.Domain.Accounting.Erp.BulkImport;
using Billing.Domain.Entities;

namespace Billing.Domain.Accounting.Erp.Remediation;

/// <summary>
/// MS-BILL-ERP-005 — read-only tenant-scoped repository used by the
/// remediation service. Every method filters by <c>TenantId</c> at
/// the SQL layer and uses
/// <see cref="Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AsNoTracking{T}"/>.
/// NEVER mutates a Billing row.
/// </summary>
public interface IErpRemediationRepository
{
    /// <summary>
    /// Project the unmapped-customer view for the tenant. A row is
    /// returned when the Billing customer is NOT soft-deleted AND
    /// either has no row in <c>quickbooks_customer_mappings</c> or
    /// its only row has <c>MappingStatus != Active</c>.
    ///
    /// <para>
    /// Hard-capped at <paramref name="hardCap"/> rows; ordered
    /// deterministically by <see cref="UnmappedCustomerRow.BillingCustomerName"/>
    /// ascending then <see cref="UnmappedCustomerRow.BillingCustomerId"/> ascending.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<UnmappedCustomerRow>> ListUnmappedCustomersAsync(
        Guid tenantId,
        int hardCap,
        CancellationToken ct = default);

    /// <summary>
    /// Tenant-scoped lookup for a single Billing customer (used by the
    /// validation endpoint to confirm existence + ownership).
    /// Returns NULL when the row does not exist or is soft-deleted.
    /// </summary>
    Task<Customer?> GetCustomerAsync(
        Guid tenantId,
        Guid billingCustomerId,
        CancellationToken ct = default);

    /// <summary>
    /// MS-BILL-ERP-006 — tenant-scoped CSV-export projection. Joins
    /// every <c>quickbooks_customer_mappings</c> row for the tenant
    /// with its owning Billing customer's display name and returns
    /// a deterministically-ordered, hard-capped list. Soft-deleted
    /// customers are excluded; Disabled mappings are INCLUDED so an
    /// operator can recover the audit row from the export.
    /// </summary>
    Task<IReadOnlyList<BulkMappingExportRow>> ListMappingExportAsync(
        Guid tenantId,
        int hardCap,
        CancellationToken ct = default);
}
