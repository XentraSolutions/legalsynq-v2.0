using Microsoft.EntityFrameworkCore;
using Billing.Domain.Entities;
using Billing.Domain.Repositories;
using Billing.Infrastructure.Data;

namespace Billing.Infrastructure.Repositories;

/// <summary>
/// MS-BILL-WRITE-005 — EF Core implementation of
/// <see cref="IInvoiceAdjustmentRepository"/>. Append-only by
/// contract: the type intentionally exposes NO Update/Delete method.
/// Mirrors the <c>RefundRepository</c> shape for consistency.
/// </summary>
public sealed class InvoiceAdjustmentRepository : IInvoiceAdjustmentRepository
{
    private readonly BillingDbContext _db;

    public InvoiceAdjustmentRepository(BillingDbContext db) => _db = db;

    public async Task<InvoiceAdjustment> AddAsync(
        InvoiceAdjustment adjustment, CancellationToken ct = default)
    {
        await _db.InvoiceAdjustments.AddAsync(adjustment, ct);
        await _db.SaveChangesAsync(ct);
        return adjustment;
    }

    public Task<InvoiceAdjustment?> GetByIdAsync(
        Guid tenantId, Guid id, CancellationToken ct = default)
        => _db.InvoiceAdjustments.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id && a.TenantId == tenantId, ct);

    public async Task<IReadOnlyList<InvoiceAdjustment>> GetByInvoiceAsync(
        Guid tenantId, Guid invoiceId, CancellationToken ct = default)
        => await _db.InvoiceAdjustments.AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.InvoiceId == invoiceId)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync(ct);

    public async Task<(decimal CreditSum, decimal DebitSum)> SumByInvoiceAsync(
        Guid tenantId, Guid invoiceId, CancellationToken ct = default)
    {
        // Two scalar aggregates over the same filtered set. EF Core
        // emits one SQL query per Sum call; both are bounded to the
        // tenant + invoice pair via the indexed predicate so the
        // cost is negligible.
        var rows = _db.InvoiceAdjustments.AsNoTracking()
            .Where(a => a.TenantId == tenantId && a.InvoiceId == invoiceId);

        var credit = await rows
            .Where(a => a.Type == "Credit")
            .SumAsync(a => (decimal?)a.Amount, ct) ?? 0m;
        var debit = await rows
            .Where(a => a.Type == "Debit")
            .SumAsync(a => (decimal?)a.Amount, ct) ?? 0m;

        return (credit, debit);
    }
}
