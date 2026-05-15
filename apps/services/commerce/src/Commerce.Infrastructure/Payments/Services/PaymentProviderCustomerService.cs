using Commerce.Application.Common.Exceptions;
using Commerce.Application.Common.Time;
using Commerce.Application.Payments.Abstractions;
using Commerce.Contracts.Payments;
using Commerce.Domain.Payments;
using Commerce.Domain.Payments.Enums;
using Commerce.Infrastructure.Payments.Mapping;
using Commerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Commerce.Infrastructure.Payments.Services;

public sealed class PaymentProviderCustomerService : IPaymentProviderCustomerService
{
    private readonly CommerceDbContext _db;
    private readonly IClock _clock;
    private readonly IPaymentProviderRegistry _registry;

    public PaymentProviderCustomerService(
        CommerceDbContext db, IClock clock, IPaymentProviderRegistry registry)
    {
        _db = db;
        _clock = clock;
        _registry = registry;
    }

    public async Task<PaymentProviderCustomerResponse> CreateOrGetAsync(
        Guid billingAccountId, PaymentProviderType provider,
        string? email, string? name, CancellationToken ct)
    {
        var account = await _db.BillingAccounts.FindAsync(new object[] { billingAccountId }, ct)
            ?? throw new NotFoundException("BillingAccount", billingAccountId.ToString());

        var existing = await _db.PaymentProviderCustomers
            .FirstOrDefaultAsync(c => c.BillingAccountId == billingAccountId && c.Provider == provider, ct);
        if (existing is not null)
        {
            existing.UpdateContact(email ?? existing.Email, name ?? existing.Name, _clock.UtcNow);
            await _db.SaveChangesAsync(ct);
            return existing.ToResponse();
        }

        var providerImpl = _registry.Get(provider);
        var result = await providerImpl.CreateOrGetCustomerAsync(
            new ProviderCustomerRequest(billingAccountId, email, name), ct);

        var entity = PaymentProviderCustomer.Create(
            billingAccountId, provider, result.ProviderCustomerId,
            result.Email ?? email, result.Name ?? name, _clock.UtcNow);
        _db.PaymentProviderCustomers.Add(entity);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Race: another request created the row first. Reload.
            var reloaded = await _db.PaymentProviderCustomers
                .FirstOrDefaultAsync(c => c.BillingAccountId == billingAccountId && c.Provider == provider, ct);
            if (reloaded is null) throw;
            return reloaded.ToResponse();
        }
        _ = account;
        return entity.ToResponse();
    }

    public async Task<IReadOnlyList<PaymentProviderCustomerResponse>> ListForAccountAsync(
        Guid billingAccountId, CancellationToken ct)
    {
        var rows = await _db.PaymentProviderCustomers.AsNoTracking()
            .Where(c => c.BillingAccountId == billingAccountId)
            .OrderBy(c => c.Provider).ToListAsync(ct);
        return rows.Select(r => r.ToResponse()).ToList();
    }
}
