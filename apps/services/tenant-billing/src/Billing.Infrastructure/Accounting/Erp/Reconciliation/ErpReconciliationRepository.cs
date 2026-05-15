using Microsoft.EntityFrameworkCore;
using Billing.Domain.Accounting.Erp;
using Billing.Domain.Accounting.Erp.QuickBooks;
using Billing.Domain.Accounting.Erp.Reconciliation;
using Billing.Infrastructure.Data;

namespace Billing.Infrastructure.Accounting.Erp.Reconciliation;

/// <summary>
/// MS-BILL-ERP-004 — concrete tenant-scoped read-only repository
/// for the reconciliation diagnostics layer. Every query filters by
/// <c>TenantId</c> at the SQL layer and uses
/// <see cref="EntityFrameworkQueryableExtensions.AsNoTracking{T}"/>.
/// NEVER mutates a Billing row.
/// </summary>
public sealed class ErpReconciliationRepository : IErpReconciliationRepository
{
    private readonly BillingDbContext _db;

    public ErpReconciliationRepository(BillingDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyDictionary<string, int>> CountByStatusAsync(
        Guid tenantId,
        CancellationToken ct = default)
    {
        var rows = await _db.AccountingExports
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .GroupBy(x => x.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var dict = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var r in rows) dict[r.Status ?? string.Empty] = r.Count;
        return dict;
    }

    public Task<AccountingExport?> GetMostRecentByStatusAsync(
        Guid tenantId,
        string status,
        CancellationToken ct = default)
        => _db.AccountingExports
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.Status == status)
            .OrderByDescending(x => x.RequestedAtUtc)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<AccountingExport>> ListAsync(
        Guid tenantId,
        int page,
        int pageSize,
        string? status,
        string? provider,
        CancellationToken ct = default)
    {
        var skip = Math.Max(0, (page - 1) * pageSize);
        var q = _db.AccountingExports
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId);
        if (!string.IsNullOrEmpty(status))
            q = q.Where(x => x.Status == status);
        if (!string.IsNullOrEmpty(provider))
            q = q.Where(x => x.Provider == provider);

        return await q
            .OrderByDescending(x => x.RequestedAtUtc)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    public Task<AccountingExport?> GetByIdAsync(
        Guid tenantId,
        Guid exportId,
        CancellationToken ct = default)
        => _db.AccountingExports
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.Id == exportId)
            .FirstOrDefaultAsync(ct);

    public Task<int> CountSiblingsByFingerprintAsync(
        Guid tenantId,
        string fingerprint,
        Guid exportId,
        CancellationToken ct = default)
        => _db.AccountingExports
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId
                     && x.Fingerprint == fingerprint
                     && x.Id != exportId)
            .CountAsync(ct);

    public async Task<IReadOnlyList<AccountingExport>> ListRecentForProviderHealthAsync(
        Guid tenantId,
        DateTime sinceUtc,
        int hardCap,
        CancellationToken ct = default)
        => await _db.AccountingExports
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.RequestedAtUtc >= sinceUtc)
            .OrderByDescending(x => x.RequestedAtUtc)
            .Take(hardCap)
            .ToListAsync(ct)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<AccountingExport>> ListLatestPerProviderPerStatusAsync(
        Guid tenantId,
        CancellationToken ct = default)
    {
        // Distinct (provider, status) buckets — for each take the
        // most recent row. We materialise the (provider, status)
        // tuples first to keep the per-bucket lookup index-friendly
        // (TenantId, RequestedAtUtc DESC scan with composite filter).
        var buckets = await _db.AccountingExports
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .GroupBy(x => new { x.Provider, x.Status })
            .Select(g => new { g.Key.Provider, g.Key.Status })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var rows = new List<AccountingExport>(buckets.Count);
        foreach (var b in buckets)
        {
            var row = await _db.AccountingExports
                .AsNoTracking()
                .Where(x => x.TenantId == tenantId
                         && x.Provider == b.Provider
                         && x.Status == b.Status)
                .OrderByDescending(x => x.RequestedAtUtc)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);
            if (row is not null) rows.Add(row);
        }
        return rows;
    }

    // ---- Mapping-health ---------------------------------------

    public Task<int> CountMappingsByStatusAsync(
        Guid tenantId,
        string mappingStatus,
        CancellationToken ct = default)
        => _db.QuickBooksCustomerMappings
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.MappingStatus == mappingStatus)
            .CountAsync(ct);

    public Task<int> CountAllMappingsAsync(
        Guid tenantId,
        CancellationToken ct = default)
        => _db.QuickBooksCustomerMappings
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .CountAsync(ct);

    public async Task<int> CountUnmappedActiveCustomersAsync(
        Guid tenantId,
        int hardCap,
        CancellationToken ct = default)
    {
        // EF translates the LEFT-JOIN-IS-NULL pattern to a single
        // server-side query. Bound by hardCap so the worst case is
        // a single index seek + a bounded scan, not an unlimited
        // table walk.
        var query =
            from c in _db.Customers.AsNoTracking()
            where c.TenantId == tenantId && !c.IsDeleted
            join m in _db.QuickBooksCustomerMappings.AsNoTracking()
                .Where(x => x.TenantId == tenantId)
                on c.Id equals m.BillingCustomerId into ms
            from m in ms.DefaultIfEmpty()
            where m == null
            select c.Id;

        return await query.Take(hardCap).CountAsync(ct).ConfigureAwait(false);
    }

    public Task<int> CountStaleMappingsAsync(
        Guid tenantId,
        DateTime staleBeforeUtc,
        CancellationToken ct = default)
        => _db.QuickBooksCustomerMappings
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId
                     && x.MappingStatus == QuickBooksCustomerMappingStatus.Active
                     && (x.LastExportedAtUtc == null
                        || x.LastExportedAtUtc < staleBeforeUtc))
            .CountAsync(ct);

    public Task<QuickBooksCustomerMapping?> GetMostRecentlyExportedMappingAsync(
        Guid tenantId,
        CancellationToken ct = default)
        => _db.QuickBooksCustomerMappings
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.LastExportedAtUtc != null)
            .OrderByDescending(x => x.LastExportedAtUtc)
            .FirstOrDefaultAsync(ct);
}
