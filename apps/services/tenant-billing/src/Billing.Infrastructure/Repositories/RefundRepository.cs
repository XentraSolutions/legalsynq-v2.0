using Microsoft.EntityFrameworkCore;
using Billing.Domain.Entities;
using Billing.Domain.Repositories;
using Billing.Infrastructure.Data;

namespace Billing.Infrastructure.Repositories;

public sealed class RefundRepository : IRefundRepository
{
    private readonly BillingDbContext _db;

    public RefundRepository(BillingDbContext db) => _db = db;

    public async Task<Refund> AddAsync(Refund refund, CancellationToken ct = default)
    {
        await _db.Refunds.AddAsync(refund, ct);
        await _db.SaveChangesAsync(ct);
        return refund;
    }

    public Task<Refund?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Refunds.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<IReadOnlyList<Refund>> GetAllAsync(CancellationToken ct = default)
        => await _db.Refunds.AsNoTracking()
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Refund>> GetByInvoiceAsync(Guid invoiceId, CancellationToken ct = default)
        => await _db.Refunds.AsNoTracking()
            .Where(r => r.InvoiceId == invoiceId)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync(ct);
}
