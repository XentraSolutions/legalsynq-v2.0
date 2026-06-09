using Microsoft.EntityFrameworkCore;
using Billing.Domain.Accounting.Erp;
using Billing.Domain.Entities;
using Billing.Infrastructure.Data;

namespace Billing.Infrastructure.Accounting.Erp;

/// <summary>
/// MS-BILL-ERP-001 — concrete tenant-scoped repository for
/// <see cref="AccountingExport"/> lifecycle rows AND the read-only
/// projection-window loaders consumed by
/// <c>AccountingExportProjectionBuilder</c>.
///
/// <para>
/// All read methods apply <c>Where(x => x.TenantId == tenantId)</c>
/// at the SQL level and use <see cref="EntityFrameworkQueryableExtensions.AsNoTracking{T}"/>.
/// Write methods INSERT / UPDATE only the <c>accounting_exports</c>
/// row — never an Invoice, Payment, or InvoiceAdjustment.
/// </para>
/// </summary>
public sealed class AccountingExportRepository : IAccountingExportRepository
{
    private readonly BillingDbContext _db;

    public AccountingExportRepository(BillingDbContext db)
    {
        _db = db;
    }

    public async Task<AccountingExport?> TryReserveSlotAsync(
        Guid tenantId,
        string fingerprint,
        AccountingExport newPending,
        CancellationToken ct = default)
    {
        // Serializable transaction: the SELECT below acquires
        // next-key locks on the (TenantId, Fingerprint) index range
        // under MySQL InnoDB. A concurrent caller for the same
        // fingerprint either (a) sees our committed Pending row
        // here and short-circuits to Duplicate, or (b) blocks on
        // the gap lock until we commit, then sees the row on
        // re-entry. Either way, only ONE Pending row per
        // tenant+fingerprint exists at a time.
        await using var tx = await _db.Database
            .BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct)
            .ConfigureAwait(false);

        var existing = await _db.AccountingExports
            .Where(x =>
                x.TenantId == tenantId
                && x.Fingerprint == fingerprint
                && (x.Status == AccountingExportStatus.Pending
                    || x.Status == AccountingExportStatus.Exported
                    || x.Status == AccountingExportStatus.Duplicate))
            .OrderByDescending(x => x.RequestedAtUtc)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            await tx.CommitAsync(ct).ConfigureAwait(false);
            // Detach so the caller cannot accidentally save changes
            // against the existing row when handling the duplicate.
            _db.Entry(existing).State = EntityState.Detached;
            return existing;
        }

        _db.AccountingExports.Add(newPending);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
        return null;
    }

    public async Task UpdateTerminalAsync(
        AccountingExport export,
        CancellationToken ct = default)
    {
        // The caller hands us a live tracked entity (or one we
        // re-attach via Update). EF computes the diff and emits a
        // single UPDATE. We never re-INSERT.
        _db.AccountingExports.Update(export);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public Task<AccountingExport?> GetByIdAsync(
        Guid tenantId,
        Guid exportId,
        CancellationToken ct = default)
        => _db.AccountingExports
            .Where(x => x.TenantId == tenantId && x.Id == exportId)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<AccountingExport>> ListAsync(
        Guid tenantId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var skip = Math.Max(0, (page - 1) * pageSize);
        var rows = await _db.AccountingExports
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.RequestedAtUtc)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        return rows;
    }

    // ------------- Projection-window loaders -----------------------

    public async Task<IReadOnlyList<Invoice>> LoadInvoicesForWindowAsync(
        Guid tenantId,
        DateTime fromUtc,
        DateTime toUtc,
        int hardCap,
        CancellationToken ct = default)
    {
        var rows = await _db.Invoices
            .AsNoTracking()
            .Where(x =>
                x.TenantId == tenantId
                && x.IssueDate >= fromUtc
                && x.IssueDate < toUtc)
            .OrderBy(x => x.IssueDate).ThenBy(x => x.InvoiceNumber)
            .Take(hardCap)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        return rows;
    }

    public async Task<IReadOnlyList<Payment>> LoadPaymentsForWindowAsync(
        Guid tenantId,
        DateTime fromUtc,
        DateTime toUtc,
        int hardCap,
        CancellationToken ct = default)
    {
        var rows = await _db.Payments
            .AsNoTracking()
            .Where(x =>
                x.TenantId == tenantId
                && x.PaidAt >= fromUtc
                && x.PaidAt < toUtc)
            .OrderBy(x => x.PaidAt)
            .Take(hardCap)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        return rows;
    }

    public async Task<IReadOnlyList<InvoiceAdjustment>> LoadAdjustmentsForWindowAsync(
        Guid tenantId,
        DateTime fromUtc,
        DateTime toUtc,
        int hardCap,
        CancellationToken ct = default)
    {
        var rows = await _db.InvoiceAdjustments
            .AsNoTracking()
            .Where(x =>
                x.TenantId == tenantId
                && x.CreatedAt >= fromUtc
                && x.CreatedAt < toUtc)
            .OrderBy(x => x.CreatedAt)
            .Take(hardCap)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        return rows;
    }

    public async Task<IReadOnlyDictionary<Guid, string>> LoadCustomerNamesAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> customerIds,
        CancellationToken ct = default)
    {
        if (customerIds is null || customerIds.Count == 0)
            return new Dictionary<Guid, string>();

        // Materialise the id set up-front so EF translates this to
        // a single `WHERE Id IN (...)` query; bound it by tenant
        // first to keep the index seek tenant-local.
        var ids = customerIds.Distinct().ToArray();
        var rows = await _db.Customers
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && ids.Contains(c.Id))
            .Select(c => new { c.Id, c.Name })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var dict = new Dictionary<Guid, string>(rows.Count);
        foreach (var r in rows)
            dict[r.Id] = r.Name ?? string.Empty;
        return dict;
    }
}
