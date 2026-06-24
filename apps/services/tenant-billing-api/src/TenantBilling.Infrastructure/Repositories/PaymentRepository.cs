using Microsoft.EntityFrameworkCore;
using TenantBilling.Domain.Entities;
using TenantBilling.Domain.Repositories;
using TenantBilling.Infrastructure.Data;

namespace TenantBilling.Infrastructure.Repositories;

public sealed class PaymentRepository : IPaymentRepository
{
    private readonly TenantBillingDbContext _db;

    public PaymentRepository(TenantBillingDbContext db) => _db = db;

    public async Task<Payment> AddAsync(Payment payment, CancellationToken ct = default)
    {
        await _db.Payments.AddAsync(payment, ct);
        await _db.SaveChangesAsync(ct);
        return payment;
    }

    public async Task<Payment> UpdateAsync(Payment payment, CancellationToken ct = default)
    {
        _db.Payments.Update(payment);
        await _db.SaveChangesAsync(ct);
        return payment;
    }

    public Task<Payment?> GetByIdForTenantAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => _db.Payments
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Id == id, ct);

    public async Task<IReadOnlyList<Payment>> GetAllForTenantAsync(Guid tenantId, CancellationToken ct = default)
        => await _db.Payments
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(ct);

    public Task<bool> ExistsByTenantAndReferenceAsync(Guid tenantId, string transactionReference, CancellationToken ct = default)
        => _db.Payments
            .AsNoTracking()
            .AnyAsync(p => p.TenantId == tenantId && p.TransactionReference == transactionReference, ct);

    public async Task<IReadOnlyList<Payment>> GetByInvoiceIdAsync(Guid tenantId, Guid invoiceId, CancellationToken ct = default)
        => await _db.Payments
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.InvoiceId == invoiceId)
            .OrderByDescending(p => p.PaidAt)
            .ThenByDescending(p => p.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Payment>> ListAsync(
        Guid tenantId,
        Guid? invoiceId,
        string? status,
        string? method,
        DateTime? fromDate,
        DateTime? toDate,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var skip = Math.Max(0, (page - 1) * pageSize);
        return await ApplyFilters(_db.Payments.AsNoTracking(), tenantId, invoiceId, status, method, fromDate, toDate)
            .OrderByDescending(p => p.PaidAt)
            .ThenByDescending(p => p.CreatedAt)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    public Task<int> CountAsync(
        Guid tenantId,
        Guid? invoiceId,
        string? status,
        string? method,
        DateTime? fromDate,
        DateTime? toDate,
        CancellationToken ct = default)
        => ApplyFilters(_db.Payments.AsNoTracking(), tenantId, invoiceId, status, method, fromDate, toDate)
            .CountAsync(ct);

    public async Task<decimal> SumRecordedPaymentsForInvoiceAsync(Guid tenantId, Guid invoiceId, CancellationToken ct = default)
    {
        // Only non-voided payments contribute to the invoice's paid total.
        // We compare on the lifecycle status string set by the service
        // ("Recorded" today; "Voided" once the void flow is implemented).
        return await _db.Payments
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId
                && p.InvoiceId == invoiceId
                && p.Status != "Voided")
            .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;
    }

    /// <summary>
    /// STAT-B01: tenant + customer scoped recorded-payment list.
    /// The double tenant filter (on the payment row AND through the
    /// joined invoice) is intentional defence-in-depth — a payment
    /// whose <c>TenantId</c> is somehow stale relative to its parent
    /// invoice cannot leak across tenants because BOTH ends of the
    /// join are pinned to <paramref name="tenantId"/>. Voided
    /// payments are filtered out at the database level since the
    /// statement engine only ever counts recorded ones.
    /// </summary>
    public async Task<IReadOnlyList<Payment>> GetRecordedPaymentsForCustomerAsync(
        Guid tenantId, Guid customerId, CancellationToken ct = default)
        => await _db.Payments
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId
                && p.Status != "Voided"
                && p.Invoice != null
                && p.Invoice.TenantId == tenantId
                && p.Invoice.CustomerId == customerId)
            .OrderBy(p => p.PaidAt)
            .ThenBy(p => p.Id)
            .ToListAsync(ct);

    private static IQueryable<Payment> ApplyFilters(
        IQueryable<Payment> query,
        Guid tenantId,
        Guid? invoiceId,
        string? status,
        string? method,
        DateTime? fromDate,
        DateTime? toDate)
    {
        query = query.Where(p => p.TenantId == tenantId);

        if (invoiceId.HasValue && invoiceId.Value != Guid.Empty)
            query = query.Where(p => p.InvoiceId == invoiceId.Value);

        if (!string.IsNullOrWhiteSpace(status))
        {
            var st = status.Trim();
            query = query.Where(p => p.Status.ToLower() == st.ToLower());
        }

        if (!string.IsNullOrWhiteSpace(method))
        {
            var m = method.Trim();
            query = query.Where(p => p.Method.ToLower() == m.ToLower());
        }

        if (fromDate.HasValue)
            query = query.Where(p => p.PaidAt >= fromDate.Value);
        if (toDate.HasValue)
            query = query.Where(p => p.PaidAt <= toDate.Value);

        return query;
    }
}
