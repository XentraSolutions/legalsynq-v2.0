using Commerce.Application.Common.Time;
using Commerce.Domain.Subscriptions;
using Commerce.Domain.Subscriptions.Enums;
using Commerce.Infrastructure.Persistence;

namespace Commerce.Infrastructure.Subscriptions.Services;

/// <summary>
/// Internal helper used by <see cref="SubscriptionService"/> to append
/// a <see cref="SubscriptionChange"/> row inside the same DbContext
/// SaveChanges as the mutation. Mirrors <c>BillingAuditWriter</c>.
/// </summary>
public sealed class SubscriptionChangeWriter
{
    private readonly CommerceDbContext _db;
    private readonly IClock _clock;

    public SubscriptionChangeWriter(CommerceDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public SubscriptionChange Append(
        Guid subscriptionId,
        SubscriptionChangeType changeType,
        DateTime effectiveAtUtc,
        Guid? fromPlanId = null,
        Guid? toPlanId = null,
        Guid? fromPriceId = null,
        Guid? toPriceId = null,
        ProrationBehavior prorationBehavior = ProrationBehavior.None,
        string? reason = null,
        string? metadataJson = null)
    {
        var change = SubscriptionChange.Create(
            subscriptionId, changeType, fromPlanId, toPlanId,
            fromPriceId, toPriceId, effectiveAtUtc,
            prorationBehavior, reason, metadataJson, _clock.UtcNow);
        _db.SubscriptionChanges.Add(change);
        return change;
    }
}
