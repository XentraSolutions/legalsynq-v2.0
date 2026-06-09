using Microsoft.EntityFrameworkCore;
using Billing.Domain.Accounting.Erp.QuickBooks;
using Billing.Infrastructure.Data;

namespace Billing.Infrastructure.Accounting.Erp.Providers.QuickBooks;

/// <summary>
/// MS-BILL-ERP-003 — concrete tenant-scoped repository for
/// <see cref="QuickBooksCustomerMapping"/>.
///
/// <para>
/// Every read filters by <c>TenantId</c> at the SQL level and uses
/// <see cref="EntityFrameworkQueryableExtensions.AsNoTracking{T}"/>
/// so cross-tenant probes never hand back another tenant's row.
/// Duplicate-key violations on either of the two unique indexes
/// (Billing↔QBO direction and QBO↔Billing direction) are translated
/// into <see cref="QuickBooksCustomerMappingConflictException"/>
/// so the controller can surface a 409 Conflict deterministically
/// instead of a generic 500.
/// </para>
/// </summary>
public sealed class QuickBooksCustomerMappingRepository : IQuickBooksCustomerMappingRepository
{
    private const string BillingUniqueIndexName = "UX_quickbooks_customer_mappings_TenantId_BillingCustomerId";
    private const string QboUniqueIndexName = "UX_quickbooks_customer_mappings_TenantId_QuickBooksCustomerId";

    private readonly BillingDbContext _db;

    public QuickBooksCustomerMappingRepository(BillingDbContext db)
    {
        _db = db;
    }

    public async Task<QuickBooksCustomerMapping> AddAsync(
        QuickBooksCustomerMapping mapping,
        CancellationToken ct = default)
    {
        await _db.QuickBooksCustomerMappings.AddAsync(mapping, ct).ConfigureAwait(false);
        try
        {
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (TryMapUniqueViolation(ex, out var message))
        {
            throw new QuickBooksCustomerMappingConflictException(message);
        }
        return mapping;
    }

    public async Task UpdateAsync(
        QuickBooksCustomerMapping mapping,
        CancellationToken ct = default)
    {
        _db.QuickBooksCustomerMappings.Update(mapping);
        try
        {
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (TryMapUniqueViolation(ex, out var message))
        {
            throw new QuickBooksCustomerMappingConflictException(message);
        }
    }

    public async Task<bool> DeleteAsync(
        Guid tenantId,
        Guid id,
        CancellationToken ct = default)
    {
        var existing = await _db.QuickBooksCustomerMappings
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, ct)
            .ConfigureAwait(false);
        if (existing is null) return false;

        _db.QuickBooksCustomerMappings.Remove(existing);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return true;
    }

    public Task<QuickBooksCustomerMapping?> GetByIdAsync(
        Guid tenantId,
        Guid id,
        CancellationToken ct = default)
        => _db.QuickBooksCustomerMappings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, ct);

    public Task<QuickBooksCustomerMapping?> GetByBillingCustomerAsync(
        Guid tenantId,
        Guid billingCustomerId,
        CancellationToken ct = default)
        => _db.QuickBooksCustomerMappings
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.TenantId == tenantId && x.BillingCustomerId == billingCustomerId,
                ct);

    public Task<QuickBooksCustomerMapping?> GetByQuickBooksCustomerIdAsync(
        Guid tenantId,
        string quickBooksCustomerId,
        CancellationToken ct = default)
    {
        // Hit the tenant-scoped unique index on
        // (TenantId, QuickBooksCustomerId); cross-tenant probes
        // resolve to null instead of leaking another tenant's row.
        if (string.IsNullOrWhiteSpace(quickBooksCustomerId))
        {
            return Task.FromResult<QuickBooksCustomerMapping?>(null);
        }
        return _db.QuickBooksCustomerMappings
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.TenantId == tenantId
                    && x.QuickBooksCustomerId == quickBooksCustomerId,
                ct);
    }

    public async Task<IReadOnlyList<QuickBooksCustomerMapping>> ListAsync(
        Guid tenantId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 25;
        if (pageSize > 100) pageSize = 100;

        return await _db.QuickBooksCustomerMappings
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.UpdatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task TouchLastExportedAsync(
        Guid tenantId,
        Guid id,
        DateTime nowUtc,
        CancellationToken ct = default)
    {
        // Best-effort, fire-and-no-throw audit stamp. We deliberately
        // load + save (instead of a raw UPDATE) so the row stays
        // tracked through the same DbContext lifetime as the caller
        // and so cross-tenant probes return a no-op.
        var existing = await _db.QuickBooksCustomerMappings
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, ct)
            .ConfigureAwait(false);
        if (existing is null) return;

        existing.LastExportedAtUtc = nowUtc;
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Recognise a duplicate-key error on either of the two
    /// tenant-scoped unique indexes and translate it to a
    /// caller-friendly message. Pattern-matches on the index name
    /// in the inner exception chain to stay portable across
    /// MySQL/MariaDB/Pomelo error variants.
    /// </summary>
    private static bool TryMapUniqueViolation(DbUpdateException ex, out string message)
    {
        for (Exception? cur = ex; cur is not null; cur = cur.InnerException)
        {
            if (cur.Message.Contains(BillingUniqueIndexName, StringComparison.OrdinalIgnoreCase))
            {
                message = "A mapping already exists for this Billing customer.";
                return true;
            }
            if (cur.Message.Contains(QboUniqueIndexName, StringComparison.OrdinalIgnoreCase))
            {
                message = "A mapping already exists for this QuickBooks customer.";
                return true;
            }
        }
        message = string.Empty;
        return false;
    }
}
