using Commerce.Application.Common.Exceptions;
using Commerce.Application.Payments.Abstractions;
using Commerce.Contracts.Payments;
using Commerce.Infrastructure.Payments.Mapping;
using Commerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Commerce.Infrastructure.Payments.Services;

public sealed class PaymentRecordQueryService : IPaymentRecordService
{
    private readonly CommerceDbContext _db;
    public PaymentRecordQueryService(CommerceDbContext db) => _db = db;

    public async Task<PaymentResponse> GetAsync(Guid id, CancellationToken ct)
    {
        var p = await _db.Payments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Payment", id.ToString());
        return p.ToResponse();
    }

    public async Task<IReadOnlyList<PaymentResponse>> ListAsync(int take, CancellationToken ct)
    {
        if (take <= 0) take = 50;
        if (take > 500) take = 500;
        var rows = await _db.Payments.AsNoTracking()
            .OrderByDescending(p => p.CreatedAtUtc)
            .Take(take).ToListAsync(ct);
        return rows.Select(r => r.ToResponse()).ToList();
    }

    public async Task<IReadOnlyList<PaymentResponse>> ListForBillingAccountAsync(
        Guid billingAccountId, CancellationToken ct)
    {
        var rows = await _db.Payments.AsNoTracking()
            .Where(p => p.BillingAccountId == billingAccountId)
            .OrderByDescending(p => p.CreatedAtUtc).ToListAsync(ct);
        return rows.Select(r => r.ToResponse()).ToList();
    }

    public async Task<IReadOnlyList<PaymentResponse>> ListForSubscriptionAsync(
        Guid subscriptionId, CancellationToken ct)
    {
        var rows = await _db.Payments.AsNoTracking()
            .Where(p => p.SubscriptionId == subscriptionId)
            .OrderByDescending(p => p.CreatedAtUtc).ToListAsync(ct);
        return rows.Select(r => r.ToResponse()).ToList();
    }

    public async Task<PaymentAttemptResponse> GetAttemptAsync(Guid id, CancellationToken ct)
    {
        var a = await _db.PaymentAttempts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("PaymentAttempt", id.ToString());
        return a.ToResponse();
    }

    public async Task<IReadOnlyList<PaymentAttemptResponse>> ListAttemptsAsync(int take, CancellationToken ct)
    {
        if (take <= 0) take = 50;
        if (take > 500) take = 500;
        var rows = await _db.PaymentAttempts.AsNoTracking()
            .OrderByDescending(a => a.CreatedAtUtc)
            .Take(take).ToListAsync(ct);
        return rows.Select(r => r.ToResponse()).ToList();
    }
}
