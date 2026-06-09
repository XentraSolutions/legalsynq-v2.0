using Commerce.Domain.Subscriptions;
using Commerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Commerce.Infrastructure.Subscriptions.Services;

/// <summary>
/// Allocates the next <see cref="SubscriptionNumber"/> using max+1 over
/// existing rows. Mirrors the semantics of
/// <c>AccountNumberGenerator</c>: under concurrent multi-instance
/// writes two callers may compute the same number, but the unique
/// index <c>ux_subscriptions_subscription_number</c> guarantees only
/// one insert succeeds; the application service retries on conflict.
/// </summary>
public interface ISubscriptionNumberGenerator
{
    Task<string> AllocateAsync(CancellationToken ct);
}

public sealed class SubscriptionNumberGenerator : ISubscriptionNumberGenerator
{
    private readonly CommerceDbContext _db;

    public SubscriptionNumberGenerator(CommerceDbContext db) => _db = db;

    public async Task<string> AllocateAsync(CancellationToken ct)
    {
        var existing = await _db.Subscriptions
            .AsNoTracking()
            .Select(x => x.SubscriptionNumber)
            .ToListAsync(ct);

        long next = 1;
        foreach (var num in existing)
        {
            if (SubscriptionNumber.TryParseSequence(num, out var seq) && seq >= next)
                next = seq + 1;
        }
        return SubscriptionNumber.Format(next);
    }
}
