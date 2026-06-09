using Microsoft.EntityFrameworkCore;
using Billing.Domain.Accounting.Erp;
using Billing.Domain.Accounting.Erp.Governance;
using Billing.Domain.Accounting.Erp.QuickBooks;
using Billing.Infrastructure.Data;

namespace Billing.Infrastructure.Accounting.Erp.Governance;

/// <summary>
/// MS-BILL-ERP-007 — concrete read-only tenant-scoped repository
/// for the governance analytics service. Every query filters by
/// <c>TenantId</c> at the SQL layer and uses
/// <see cref="EntityFrameworkQueryableExtensions.AsNoTracking{T}"/>.
///
/// <para>
/// NEVER mutates a Billing row. NEVER joins across tenants.
/// Result sets are bounded either by the natural shape of the
/// query (status enum, daily bucket cap) or by an explicit
/// <c>Take(hardCap)</c> for unbounded sources (audit-trail rows,
/// fingerprint group-by).
/// </para>
/// </summary>
public sealed class ErpGovernanceAnalyticsRepository : IErpGovernanceAnalyticsRepository
{
    /// <summary>
    /// Hard cap on every audit-row source list (mappings, bulk
    /// imports, exports). The composing service unions these and
    /// pages, so this cap bounds the worst-case worker memory
    /// without bias the operator notices.
    /// </summary>
    public const int AuditRowsPerSourceHardCap = 500;

    private readonly BillingDbContext _db;

    public ErpGovernanceAnalyticsRepository(BillingDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyDictionary<string, int>> GetExportCountsByStatusAsync(
        Guid tenantId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken ct = default)
    {
        var rows = await _db.AccountingExports
            .AsNoTracking()
            .Where(x =>
                x.TenantId == tenantId
                && x.RequestedAtUtc >= fromUtc
                && x.RequestedAtUtc <= toUtc)
            .GroupBy(x => x.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct)
            .ConfigureAwait(false);
        return rows.ToDictionary(r => r.Status, r => r.Count);
    }

    public async Task<MappingTotals> GetMappingTotalsAsync(
        Guid tenantId,
        CancellationToken ct = default)
    {
        var activeCustomerCount = await _db.Customers
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && !c.IsDeleted)
            .CountAsync(ct)
            .ConfigureAwait(false);

        var mappingsByStatus = await _db.QuickBooksCustomerMappings
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId)
            .GroupBy(m => m.MappingStatus)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var activeMappings = mappingsByStatus
            .Where(x => x.Status == QuickBooksCustomerMappingStatus.Active)
            .Sum(x => x.Count);
        var inactiveMappings = mappingsByStatus
            .Where(x => x.Status != QuickBooksCustomerMappingStatus.Active)
            .Sum(x => x.Count);

        var invoiceFirstActive = await _db.QuickBooksCustomerMappings
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId
                && m.MappingStatus == QuickBooksCustomerMappingStatus.Active
                && m.ExportMode == QuickBooksCustomerMappingExportMode.InvoiceFirst)
            .CountAsync(ct)
            .ConfigureAwait(false);

        // Unresolved = active customers with no Active mapping
        // (LEFT-JOIN-IS-NULL, mirrored from ERP-005 repo).
        var unresolvedQuery =
            from c in _db.Customers.AsNoTracking()
            where c.TenantId == tenantId && !c.IsDeleted
            join m in _db.QuickBooksCustomerMappings.AsNoTracking()
                .Where(x =>
                    x.TenantId == tenantId
                    && x.MappingStatus == QuickBooksCustomerMappingStatus.Active)
                on c.Id equals m.BillingCustomerId into ms
            from m in ms.DefaultIfEmpty()
            where m == null
            select c.Id;

        var unresolved = await unresolvedQuery
            .CountAsync(ct)
            .ConfigureAwait(false);

        return new MappingTotals(
            ActiveCustomerCount: activeCustomerCount,
            ActiveMappingCount: activeMappings,
            InactiveMappingCount: inactiveMappings,
            InvoiceFirstActiveMappingCount: invoiceFirstActive,
            UnresolvedMappingCount: unresolved);
    }

    public async Task<IReadOnlyList<UnresolvedCustomerAgingRow>> GetUnresolvedCustomerAgingAsync(
        Guid tenantId,
        int hardCap,
        CancellationToken ct = default)
    {
        if (hardCap <= 0) hardCap = 50;

        var baseQuery =
            from c in _db.Customers.AsNoTracking()
            where c.TenantId == tenantId && !c.IsDeleted
            join m in _db.QuickBooksCustomerMappings.AsNoTracking()
                .Where(x => x.TenantId == tenantId)
                on c.Id equals m.BillingCustomerId into ms
            from m in ms.DefaultIfEmpty()
            where m == null
                || m.MappingStatus != QuickBooksCustomerMappingStatus.Active
            // Oldest customers (by creation date) first — that is
            // the dimension this dashboard ranks on.
            orderby c.CreatedAt ascending, c.Id ascending
            select new
            {
                c.Id,
                c.Name,
                c.CreatedAt,
                ExistingMappingStatus = m == null ? null : m.MappingStatus,
            };

        var rows = await baseQuery.Take(hardCap).ToListAsync(ct).ConfigureAwait(false);
        if (rows.Count == 0) return Array.Empty<UnresolvedCustomerAgingRow>();

        var customerIds = rows.Select(r => r.Id).ToList();

        var lastInvoiceMap = await _db.Invoices
            .AsNoTracking()
            .Where(i => i.TenantId == tenantId && customerIds.Contains(i.CustomerId))
            .GroupBy(i => i.CustomerId)
            .Select(g => new { CustomerId = g.Key, Last = g.Max(i => i.IssueDate) })
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var lastInvoiceLookup = lastInvoiceMap.ToDictionary(x => x.CustomerId, x => x.Last);

        var result = new List<UnresolvedCustomerAgingRow>(rows.Count);
        foreach (var r in rows)
        {
            lastInvoiceLookup.TryGetValue(r.Id, out var lastInvoice);
            result.Add(new UnresolvedCustomerAgingRow(
                BillingCustomerId: r.Id,
                BillingCustomerName: r.Name,
                CustomerCreatedAtUtc: r.CreatedAt,
                LastInvoiceDate: lastInvoice == default ? null : lastInvoice,
                ExistingMappingStatus: r.ExistingMappingStatus));
        }
        return result;
    }

    public async Task<int> GetStaleMappingCountAsync(
        Guid tenantId,
        DateTime staleBefore,
        CancellationToken ct = default)
    {
        return await _db.QuickBooksCustomerMappings
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId
                && m.MappingStatus == QuickBooksCustomerMappingStatus.Active
                && (m.LastExportedAtUtc == null
                    || m.LastExportedAtUtc < staleBefore))
            .CountAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<TrendBucketRow>> GetExportTrendBucketsAsync(
        Guid tenantId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken ct = default)
    {
        // Day-truncate the timestamp at the SQL layer so the
        // bucket count is bounded by the window. EF Core 8 maps
        // `EF.Functions.DateDiffDay` on MySQL via CAST AS DATE;
        // we use the more portable `new DateTime(...)` projection
        // which EF translates to DATE() on MySQL.
        var rows = await _db.AccountingExports
            .AsNoTracking()
            .Where(x =>
                x.TenantId == tenantId
                && x.RequestedAtUtc >= fromUtc
                && x.RequestedAtUtc <= toUtc)
            .GroupBy(x => new
            {
                BucketDate = new DateTime(
                    x.RequestedAtUtc.Year,
                    x.RequestedAtUtc.Month,
                    x.RequestedAtUtc.Day),
                x.Provider,
                x.ExportType,
                x.Status,
            })
            .Select(g => new TrendBucketRow(
                g.Key.BucketDate,
                g.Key.Provider,
                g.Key.ExportType,
                g.Key.Status,
                g.Count()))
            .ToListAsync(ct)
            .ConfigureAwait(false);
        return rows;
    }

    public async Task<IReadOnlyList<MappingAuditRow>> GetMappingAuditRowsAsync(
        Guid tenantId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken ct = default)
    {
        return await _db.QuickBooksCustomerMappings
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId
                && (m.CreatedAtUtc >= fromUtc && m.CreatedAtUtc <= toUtc
                    || m.UpdatedAtUtc >= fromUtc && m.UpdatedAtUtc <= toUtc))
            .OrderByDescending(m => m.UpdatedAtUtc)
            .ThenBy(m => m.Id)
            .Take(AuditRowsPerSourceHardCap)
            .Select(m => new MappingAuditRow(
                m.Id,
                m.CreatedBy,
                m.CreatedAtUtc,
                m.UpdatedAtUtc,
                m.MappingStatus))
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<BulkImportAuditRow>> GetBulkImportAuditRowsAsync(
        Guid tenantId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken ct = default)
    {
        return await _db.BulkMappingImportHistory
            .AsNoTracking()
            .Where(b => b.TenantId == tenantId
                && b.StartedAtUtc >= fromUtc
                && b.StartedAtUtc <= toUtc)
            .OrderByDescending(b => b.StartedAtUtc)
            .ThenBy(b => b.Id)
            .Take(AuditRowsPerSourceHardCap)
            .Select(b => new BulkImportAuditRow(
                b.Id,
                b.OperatorDisplayName,
                b.StartedAtUtc,
                b.CompletedAtUtc,
                b.TotalRows,
                b.AcceptedRows,
                b.RejectedRows))
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ExportAuditRow>> GetExportAuditRowsAsync(
        Guid tenantId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken ct = default)
    {
        return await _db.AccountingExports
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId
                && x.RequestedAtUtc >= fromUtc
                && x.RequestedAtUtc <= toUtc)
            .OrderByDescending(x => x.RequestedAtUtc)
            .ThenBy(x => x.Id)
            .Take(AuditRowsPerSourceHardCap)
            .Select(x => new ExportAuditRow(
                x.Id,
                x.RequestedBy,
                x.RequestedAtUtc,
                x.Status,
                x.Provider,
                x.ExportType,
                x.CorrelationId))
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<FingerprintCountRow>> GetRepeatedFailureFingerprintsAsync(
        Guid tenantId,
        DateTime fromUtc,
        DateTime toUtc,
        int hardCap,
        CancellationToken ct = default)
    {
        if (hardCap <= 0) hardCap = 50;
        return await GetFingerprintGroupsAsync(
            tenantId,
            fromUtc,
            toUtc,
            statuses: new[]
            {
                AccountingExportStatus.Failed,
                AccountingExportStatus.ProviderUnavailable,
            },
            minOccurrences: 2,
            hardCap: hardCap,
            ct: ct)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<FingerprintCountRow>> GetReplayHeavyFingerprintsAsync(
        Guid tenantId,
        DateTime fromUtc,
        DateTime toUtc,
        int hardCap,
        CancellationToken ct = default)
    {
        if (hardCap <= 0) hardCap = 50;
        return await GetFingerprintGroupsAsync(
            tenantId,
            fromUtc,
            toUtc,
            statuses: new[] { AccountingExportStatus.Duplicate },
            minOccurrences: 2,
            hardCap: hardCap,
            ct: ct)
            .ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<FingerprintCountRow>> GetFingerprintGroupsAsync(
        Guid tenantId,
        DateTime fromUtc,
        DateTime toUtc,
        string[] statuses,
        int minOccurrences,
        int hardCap,
        CancellationToken ct)
    {
        var rows = await _db.AccountingExports
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId
                && x.RequestedAtUtc >= fromUtc
                && x.RequestedAtUtc <= toUtc
                && statuses.Contains(x.Status)
                && x.Fingerprint != string.Empty)
            .GroupBy(x => new { x.Fingerprint, x.Provider, x.ExportType })
            .Select(g => new
            {
                g.Key.Fingerprint,
                g.Key.Provider,
                g.Key.ExportType,
                Count = g.Count(),
                LastSeenAtUtc = g.Max(x => x.RequestedAtUtc),
            })
            .Where(r => r.Count >= minOccurrences)
            .OrderByDescending(r => r.Count)
            .ThenByDescending(r => r.LastSeenAtUtc)
            .Take(hardCap)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (rows.Count == 0) return Array.Empty<FingerprintCountRow>();

        var fingerprints = rows.Select(r => r.Fingerprint).Distinct().ToList();
        var lastReasons = await _db.AccountingExports
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId
                && fingerprints.Contains(x.Fingerprint)
                && x.FailureReason != null)
            .GroupBy(x => x.Fingerprint)
            .Select(g => new
            {
                Fingerprint = g.Key,
                Reason = g
                    .OrderByDescending(x => x.RequestedAtUtc)
                    .Select(x => x.FailureReason)
                    .FirstOrDefault(),
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var reasonLookup = lastReasons.ToDictionary(x => x.Fingerprint, x => x.Reason);

        return rows
            .Select(r => new FingerprintCountRow(
                Fingerprint: r.Fingerprint,
                Provider: r.Provider,
                ExportType: r.ExportType,
                Count: r.Count,
                LastSeenAtUtc: r.LastSeenAtUtc,
                LastFailureReason: reasonLookup.TryGetValue(r.Fingerprint, out var rr) ? rr : null))
            .ToList();
    }

    public async Task<(string? FailureReason, DateTime? AtUtc)> GetMostRecentFailureAsync(
        Guid tenantId,
        CancellationToken ct = default)
    {
        var row = await _db.AccountingExports
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId
                && (x.Status == AccountingExportStatus.Failed
                    || x.Status == AccountingExportStatus.ProviderUnavailable))
            .OrderByDescending(x => x.RequestedAtUtc)
            .Select(x => new { x.FailureReason, x.RequestedAtUtc })
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
        return row is null ? (null, null) : (row.FailureReason, row.RequestedAtUtc);
    }

    public async Task<(int BulkImportCount, int AcceptedRowsSum)> GetBulkImportVelocityAsync(
        Guid tenantId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken ct = default)
    {
        var totals = await _db.BulkMappingImportHistory
            .AsNoTracking()
            .Where(b => b.TenantId == tenantId
                && b.StartedAtUtc >= fromUtc
                && b.StartedAtUtc <= toUtc)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Count = g.Count(),
                AcceptedSum = g.Sum(x => (int?)x.AcceptedRows) ?? 0,
            })
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
        return totals is null
            ? (0, 0)
            : (totals.Count, totals.AcceptedSum);
    }

    public async Task<int> GetMappingsResolvedInWindowAsync(
        Guid tenantId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken ct = default)
    {
        return await _db.QuickBooksCustomerMappings
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId
                && m.MappingStatus == QuickBooksCustomerMappingStatus.Active
                && m.CreatedAtUtc >= fromUtc
                && m.CreatedAtUtc <= toUtc)
            .CountAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task<decimal> GetAverageUnresolvedAgeDaysAsync(
        Guid tenantId,
        DateTime nowUtc,
        CancellationToken ct = default)
    {
        // SQL-side AVG on EF.Functions.DateDiffDay would be cleaner
        // but the MySQL provider does not consistently translate it
        // across versions. Pull only the CreatedAt column for
        // unresolved customers and average in-process — the row
        // count is the unresolved customer count, which is exactly
        // the same set the count metric in MappingTotals reports
        // (both are rendered side by side, so the operator gets a
        // consistent denominator).
        var dates = await (
            from c in _db.Customers.AsNoTracking()
            where c.TenantId == tenantId && !c.IsDeleted
            join m in _db.QuickBooksCustomerMappings.AsNoTracking()
                .Where(x =>
                    x.TenantId == tenantId
                    && x.MappingStatus == QuickBooksCustomerMappingStatus.Active)
                on c.Id equals m.BillingCustomerId into ms
            from m in ms.DefaultIfEmpty()
            where m == null
            select c.CreatedAt)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        if (dates.Count == 0) return 0m;
        var totalDays = 0d;
        foreach (var d in dates)
        {
            var span = nowUtc - d;
            totalDays += span.TotalDays > 0 ? span.TotalDays : 0;
        }
        var avg = (decimal)(totalDays / dates.Count);
        return Math.Round(avg, 2, MidpointRounding.AwayFromZero);
    }
}
