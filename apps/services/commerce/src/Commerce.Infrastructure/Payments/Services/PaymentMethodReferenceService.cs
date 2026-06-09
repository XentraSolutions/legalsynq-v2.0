using Commerce.Application.Common.Exceptions;
using Commerce.Application.Common.Time;
using Commerce.Application.Payments.Abstractions;
using Commerce.Contracts.Payments;
using Commerce.Infrastructure.Payments.Mapping;
using Commerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Commerce.Infrastructure.Payments.Services;

public sealed class PaymentMethodReferenceService : IPaymentMethodReferenceService
{
    private readonly CommerceDbContext _db;
    private readonly IClock _clock;

    public PaymentMethodReferenceService(CommerceDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<IReadOnlyList<PaymentMethodReferenceResponse>> ListForAccountAsync(
        Guid billingAccountId, CancellationToken ct)
    {
        var rows = await _db.PaymentMethodReferences.AsNoTracking()
            .Where(p => p.BillingAccountId == billingAccountId)
            .OrderByDescending(p => p.IsDefault)
            .ThenBy(p => p.CreatedAtUtc)
            .ToListAsync(ct);
        return rows.Select(r => r.ToResponse()).ToList();
    }

    public async Task<PaymentMethodReferenceResponse> MakeDefaultAsync(
        Guid billingAccountId, Guid paymentMethodId, CancellationToken ct)
    {
        var target = await _db.PaymentMethodReferences
            .FirstOrDefaultAsync(p => p.Id == paymentMethodId && p.BillingAccountId == billingAccountId, ct)
            ?? throw new NotFoundException("PaymentMethodReference", paymentMethodId.ToString());

        var siblings = await _db.PaymentMethodReferences
            .Where(p => p.BillingAccountId == billingAccountId && p.IsDefault && p.Id != paymentMethodId)
            .ToListAsync(ct);
        foreach (var s in siblings) s.DemoteDefault(_clock.UtcNow);
        target.MakeDefault(_clock.UtcNow);
        await _db.SaveChangesAsync(ct);
        return target.ToResponse();
    }
}
