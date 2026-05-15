using Microsoft.EntityFrameworkCore;
using TenantBilling.Domain.Entities;
using TenantBilling.Domain.Repositories;
using TenantBilling.Domain.Services;
using TenantBilling.Infrastructure.Data;

namespace TenantBilling.Infrastructure.Repositories;

public sealed class InvoiceRepository : IInvoiceRepository
{
    private readonly TenantBillingDbContext _db;
    private readonly IInvoiceTemplateStampingService _stamping;

    public InvoiceRepository(TenantBillingDbContext db, IInvoiceTemplateStampingService stamping)
    {
        _db = db;
        _stamping = stamping;
    }

    public async Task<Invoice> AddAsync(Invoice invoice, CancellationToken ct = default)
    {
        await _db.Invoices.AddAsync(invoice, ct);
        await _db.SaveChangesAsync(ct);
        return invoice;
    }

    public Task<Invoice?> GetByIdForTenantAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => _db.Invoices
            .AsNoTracking()
            .Include(i => i.LineItems)
            .Include(i => i.Payments)
            .Include(i => i.Refunds)
            .FirstOrDefaultAsync(i => i.TenantId == tenantId && i.Id == id, ct);

    public async Task<IReadOnlyList<Invoice>> GetAllForTenantAsync(Guid tenantId, CancellationToken ct = default)
        => await _db.Invoices
            .AsNoTracking()
            .Include(i => i.LineItems)
            .Where(i => i.TenantId == tenantId)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Invoice>> ListAsync(
        Guid tenantId,
        string? search,
        string? status,
        Guid? customerId,
        DateTime? fromDate,
        DateTime? toDate,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var skip = Math.Max(0, (page - 1) * pageSize);
        var query = ApplyFilters(_db.Invoices.AsNoTracking().Include(i => i.LineItems), tenantId, search, status, customerId, fromDate, toDate);
        return await query
            .OrderByDescending(i => i.CreatedAt)
            .ThenBy(i => i.InvoiceNumber)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    public Task<int> CountAsync(
        Guid tenantId,
        string? search,
        string? status,
        Guid? customerId,
        DateTime? fromDate,
        DateTime? toDate,
        CancellationToken ct = default)
        => ApplyFilters(_db.Invoices.AsNoTracking(), tenantId, search, status, customerId, fromDate, toDate)
            .CountAsync(ct);

    private static IQueryable<Invoice> ApplyFilters(
        IQueryable<Invoice> query,
        Guid tenantId,
        string? search,
        string? status,
        Guid? customerId,
        DateTime? fromDate,
        DateTime? toDate)
    {
        query = query.Where(i => i.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(i =>
                EF.Functions.Like(i.InvoiceNumber, $"%{s}%")
                || (i.Notes != null && EF.Functions.Like(i.Notes, $"%{s}%")));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var st = status.Trim();
            query = query.Where(i => i.Status.ToLower() == st.ToLower());
        }

        if (customerId.HasValue && customerId.Value != Guid.Empty)
            query = query.Where(i => i.CustomerId == customerId.Value);

        if (fromDate.HasValue)
            query = query.Where(i => i.IssueDate >= fromDate.Value);
        if (toDate.HasValue)
            query = query.Where(i => i.IssueDate <= toDate.Value);

        return query;
    }

    public Task<bool> ExistsByTenantAndNumberAsync(
        Guid tenantId,
        string invoiceNumber,
        Guid? excludingInvoiceId = null,
        CancellationToken ct = default)
        => _db.Invoices
            .AsNoTracking()
            .AnyAsync(i =>
                i.TenantId == tenantId
                && i.InvoiceNumber == invoiceNumber
                && (!excludingInvoiceId.HasValue || i.Id != excludingInvoiceId.Value),
                ct);

    public async Task<string?> GetLatestInvoiceNumberAsync(Guid tenantId, int year, CancellationToken ct = default)
    {
        var prefix = $"INV-{year:D4}-";
        // Lexicographic ordering on a fixed-width zero-padded suffix is
        // identical to numeric ordering, so OrderByDescending(InvoiceNumber)
        // gives us the most recently allocated number for the year.
        return await _db.Invoices
            .AsNoTracking()
            .Where(i => i.TenantId == tenantId && i.InvoiceNumber.StartsWith(prefix))
            .OrderByDescending(i => i.InvoiceNumber)
            .Select(i => i.InvoiceNumber)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<Invoice?> UpdateStatusAsync(
        Guid tenantId,
        Guid invoiceId,
        string status,
        DateTime updatedAt,
        DateTime? issuedAt = null,
        CancellationToken ct = default)
    {
        // Load tracked AND tenant-scoped so EF picks up the column updates
        // on SaveChanges and we never mutate another tenant's invoice.
        var invoice = await _db.Invoices
            .FirstOrDefaultAsync(i => i.TenantId == tenantId && i.Id == invoiceId, ct);
        if (invoice is null) return null;

        invoice.Status = status;
        invoice.UpdatedAt = updatedAt;
        // IssuedAt is set on the first Draft → Issued transition only. We
        // never overwrite a previous IssuedAt when transitioning between
        // post-Issued states (PartiallyPaid, Paid, etc.).
        if (issuedAt.HasValue && invoice.IssuedAt is null)
            invoice.IssuedAt = issuedAt;

        await _db.SaveChangesAsync(ct);

        // Re-read with line items + payments via the standard read path so
        // callers get a consistent shape. Detach tracked entity first to avoid
        // identity-tracking conflicts with the AsNoTracking read.
        _db.Entry(invoice).State = EntityState.Detached;
        return await GetByIdForTenantAsync(tenantId, invoiceId, ct);
    }

    public async Task<IReadOnlyList<Invoice>> GetInvoicesEligibleForOverdueAsync(
        Guid? tenantId,
        DateTime nowUtc,
        int take,
        CancellationToken ct = default)
    {
        var clampedTake = take <= 0 ? 0 : take;
        if (clampedTake == 0) return Array.Empty<Invoice>();

        // Date-boundary semantics — must match
        // InvoiceStatus.ComputeStatus and InvoiceService.MarkOverdueAsync
        // (single). An invoice that is "due today" (any time today UTC)
        // is NOT yet overdue; only invoices whose DueDate is strictly
        // before today UTC midnight are eligible.
        var dueBefore = nowUtc.Date;

        var query = _db.Invoices
            .AsNoTracking()
            .Where(i =>
                (i.Status == InvoiceStatus.Issued || i.Status == InvoiceStatus.PartiallyPaid)
                && i.DueDate < dueBefore);

        if (tenantId.HasValue && tenantId.Value != Guid.Empty)
            query = query.Where(i => i.TenantId == tenantId.Value);

        return await query
            // Oldest due first so the longest-overdue invoices land in the
            // batch first when a take cap clips the result.
            .OrderBy(i => i.DueDate)
            .Take(clampedTake)
            .ToListAsync(ct);
    }

    public async Task<Invoice?> ApplyStampAsync(
        Guid tenantId,
        Guid invoiceId,
        InvoiceTemplate template,
        DateTime stampedAtUtc,
        CancellationToken ct = default)
    {
        if (template is null) throw new ArgumentNullException(nameof(template));

        // Tracked + tenant-scoped load: the resulting tracked entity
        // gets the snapshot fields stamped onto it and EF picks the
        // changes up on SaveChanges. Tenant-scoping in the predicate
        // means we never accidentally stamp another tenant's row.
        var invoice = await _db.Invoices
            .FirstOrDefaultAsync(i => i.TenantId == tenantId && i.Id == invoiceId, ct);
        if (invoice is null) return null;

        // Idempotency lives in the stamping service, not here. An
        // already-stamped invoice short-circuits with no DB write —
        // we deliberately skip SaveChanges in that case so a
        // re-issue (or any future "ensure" caller) is a true no-op.
        var applied = _stamping.EnsureStampedInvoice(invoice, template, stampedAtUtc);
        if (applied)
        {
            invoice.UpdatedAt = stampedAtUtc;
            await _db.SaveChangesAsync(ct);
        }

        // Re-read via the standard read path so callers always get
        // the consistent shape (line items + payments + refunds
        // included). Detach the tracked instance first to avoid
        // identity-tracking conflicts with the AsNoTracking read.
        _db.Entry(invoice).State = EntityState.Detached;
        return await GetByIdForTenantAsync(tenantId, invoiceId, ct);
    }

    public async Task<Invoice?> TryMarkOverdueAsync(
        Guid tenantId,
        Guid invoiceId,
        DateTime nowUtc,
        CancellationToken ct = default)
    {
        var dueBefore = nowUtc.Date;

        if (_db.Database.IsRelational())
        {
            // Production path. Single-statement conditional UPDATE
            // with the eligibility predicate in the WHERE clause —
            // the database atomically decides whether to write
            // Overdue or do nothing. No tracked-load + SaveChanges
            // round-trip means there is no lost-update window: a
            // concurrent transaction that has just committed
            // Status=Paid will be observed by the WHERE filter and
            // our UPDATE will affect 0 rows. ExecuteUpdateAsync
            // runs in its own implicit transaction at the provider's
            // default isolation level, which is sufficient for a
            // true CAS here (no row version required).
            var rowsAffected = await _db.Invoices
                .Where(i =>
                    i.TenantId == tenantId
                    && i.Id == invoiceId
                    && (i.Status == InvoiceStatus.Issued || i.Status == InvoiceStatus.PartiallyPaid)
                    && i.DueDate < dueBefore)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(i => i.Status, InvoiceStatus.Overdue)
                    .SetProperty(i => i.UpdatedAt, nowUtc), ct);

            if (rowsAffected == 0) return null;

            return await GetByIdForTenantAsync(tenantId, invoiceId, ct);
        }

        // Test/InMemory fallback: the InMemory provider in EF Core 8
        // does not faithfully execute ExecuteUpdateAsync against the
        // backing dictionary, so we use a tracked-load + SaveChanges
        // pair that re-checks the eligibility predicate at write
        // time. This is best-effort (not a true CAS) but is fine for
        // tests, where there is no real concurrency.
        var invoice = await _db.Invoices
            .FirstOrDefaultAsync(i =>
                i.TenantId == tenantId
                && i.Id == invoiceId
                && (i.Status == InvoiceStatus.Issued || i.Status == InvoiceStatus.PartiallyPaid)
                && i.DueDate < dueBefore, ct);
        if (invoice is null) return null;

        invoice.Status = InvoiceStatus.Overdue;
        invoice.UpdatedAt = nowUtc;
        await _db.SaveChangesAsync(ct);

        _db.Entry(invoice).State = EntityState.Detached;
        return await GetByIdForTenantAsync(tenantId, invoiceId, ct);
    }

    /// <summary>
    /// STAT-B01: tenant + customer scoped invoice list. No
    /// <c>Include</c> beyond the bare invoice row — the statement
    /// engine only needs aggregate fields (TotalAmount, IssueDate,
    /// DueDate, Status, Currency) and would pay needlessly for line
    /// items / payments via the navigation properties.
    /// </summary>
    public async Task<IReadOnlyList<Invoice>> GetInvoicesForCustomerAsync(
        Guid tenantId, Guid customerId, CancellationToken ct = default)
        => await _db.Invoices
            .AsNoTracking()
            .Where(i => i.TenantId == tenantId && i.CustomerId == customerId)
            .OrderBy(i => i.IssueDate)
            .ThenBy(i => i.InvoiceNumber)
            .ToListAsync(ct);
}
