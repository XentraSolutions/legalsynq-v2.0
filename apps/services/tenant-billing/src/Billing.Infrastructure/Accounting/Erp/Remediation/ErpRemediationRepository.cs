using Microsoft.EntityFrameworkCore;
using Billing.Domain.Accounting.Erp;
using Billing.Domain.Accounting.Erp.BulkImport;
using Billing.Domain.Accounting.Erp.QuickBooks;
using Billing.Domain.Accounting.Erp.Remediation;
using Billing.Domain.Entities;
using Billing.Infrastructure.Data;

namespace Billing.Infrastructure.Accounting.Erp.Remediation;

/// <summary>
/// MS-BILL-ERP-005 — concrete read-only tenant-scoped repository
/// for the remediation projection. Every query filters by
/// <c>TenantId</c> at the SQL layer and uses
/// <see cref="EntityFrameworkQueryableExtensions.AsNoTracking{T}"/>.
/// NEVER mutates a Billing row.
/// </summary>
public sealed class ErpRemediationRepository : IErpRemediationRepository
{
    private readonly BillingDbContext _db;

    public ErpRemediationRepository(BillingDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<UnmappedCustomerRow>> ListUnmappedCustomersAsync(
        Guid tenantId,
        int hardCap,
        CancellationToken ct = default)
    {
        if (hardCap <= 0) hardCap = 100;

        // ---- 1. Customers without an Active mapping ----
        // LEFT-JOIN-IS-NULL pattern to surface customers that have
        // either no mapping row at all OR a non-Active mapping
        // (Disabled). Bounded by hardCap so the worst case is a
        // single index seek + a bounded scan.
        var baseQuery =
            from c in _db.Customers.AsNoTracking()
            where c.TenantId == tenantId && !c.IsDeleted
            join m in _db.QuickBooksCustomerMappings.AsNoTracking()
                .Where(x => x.TenantId == tenantId)
                on c.Id equals m.BillingCustomerId into ms
            from m in ms.DefaultIfEmpty()
            where m == null
                || m.MappingStatus != QuickBooksCustomerMappingStatus.Active
            orderby c.Name ascending, c.Id ascending
            select new
            {
                c.Id,
                c.Name,
                ExistingMappingStatus = m == null ? null : m.MappingStatus,
            };

        var rows = await baseQuery.Take(hardCap).ToListAsync(ct).ConfigureAwait(false);
        if (rows.Count == 0) return Array.Empty<UnmappedCustomerRow>();

        var customerIds = rows.Select(r => r.Id).ToList();

        // ---- 2. Last invoice date per customer (single query) ----
        // Group at the SQL layer so we don't issue N+1 queries; the
        // result set is bounded by hardCap.
        var lastInvoiceMap = await _db.Invoices
            .AsNoTracking()
            .Where(i => i.TenantId == tenantId && customerIds.Contains(i.CustomerId))
            .GroupBy(i => i.CustomerId)
            .Select(g => new { CustomerId = g.Key, Last = g.Max(i => i.IssueDate) })
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var lastInvoiceLookup = lastInvoiceMap.ToDictionary(x => x.CustomerId, x => x.Last);

        // ---- 3. Most-recent failed/blocked export per tenant ----
        // We don't have a direct customer column on accounting_exports
        // (it is a batch-level row), so we surface the tenant-wide
        // most-recent Failed/ProviderUnavailable reason as a single
        // value applied to every row — this is what the operator
        // sees in the reconciliation page already, and it is the
        // highest-signal context for "why did the last export not
        // include this customer". We use the SAME recipe as
        // ErpReconciliationRepository.GetMostRecentByStatusAsync.
        var lastFailure = await _db.AccountingExports
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId
                && (x.Status == AccountingExportStatus.Failed
                    || x.Status == AccountingExportStatus.ProviderUnavailable))
            .OrderByDescending(x => x.RequestedAtUtc)
            .Select(x => new { x.FailureReason, x.RequestedAtUtc, x.Status })
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        // ---- 4. Compose the result rows ----
        var result = new List<UnmappedCustomerRow>(rows.Count);
        foreach (var r in rows)
        {
            lastInvoiceLookup.TryGetValue(r.Id, out var lastInvoice);
            var blockedReason = r.ExistingMappingStatus is null
                ? "No QuickBooks customer mapping is configured for this Billing customer."
                : $"Existing mapping is {r.ExistingMappingStatus}; the resolver treats it as unmapped.";

            result.Add(new UnmappedCustomerRow(
                BillingCustomerId: r.Id,
                BillingCustomerName: r.Name,
                LastInvoiceDate: lastInvoice == default ? null : lastInvoice,
                LastExportFailureReason: lastFailure?.FailureReason,
                LastExportFailureAtUtc: lastFailure?.RequestedAtUtc,
                ExportBlockedReason: blockedReason,
                ExistingMappingStatus: r.ExistingMappingStatus));
        }
        return result;
    }

    public Task<Customer?> GetCustomerAsync(
        Guid tenantId,
        Guid billingCustomerId,
        CancellationToken ct = default)
        => _db.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(
                c => c.TenantId == tenantId
                  && c.Id == billingCustomerId
                  && !c.IsDeleted,
                ct);

    public async Task<IReadOnlyList<BulkMappingExportRow>> ListMappingExportAsync(
        Guid tenantId,
        int hardCap,
        CancellationToken ct = default)
    {
        if (hardCap <= 0) hardCap = 1000;

        // Inner-join mappings to their owning Billing customer so a
        // mapping that points at a soft-deleted customer is omitted
        // from the export (operationally, those rows are stale and
        // re-importing them would silently revive them).
        var query =
            from m in _db.QuickBooksCustomerMappings.AsNoTracking()
            where m.TenantId == tenantId
            join c in _db.Customers.AsNoTracking()
                .Where(x => x.TenantId == tenantId && !x.IsDeleted)
                on m.BillingCustomerId equals c.Id
            orderby c.Name ascending, m.BillingCustomerId ascending
            select new BulkMappingExportRow(
                m.BillingCustomerId,
                c.Name,
                m.QuickBooksCustomerId,
                m.QuickBooksDisplayName,
                m.MappingStatus,
                m.ExportMode,
                m.CreatedBy,
                m.CreatedAtUtc,
                m.UpdatedAtUtc,
                m.LastExportedAtUtc);

        return await query.Take(hardCap).ToListAsync(ct).ConfigureAwait(false);
    }
}
