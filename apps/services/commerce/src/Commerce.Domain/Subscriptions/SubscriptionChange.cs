using Commerce.Domain.Common;
using Commerce.Domain.Subscriptions.Enums;

namespace Commerce.Domain.Subscriptions;

public sealed class SubscriptionChange : Entity<Guid>
{
    public Guid SubscriptionId { get; private set; }
    public SubscriptionChangeType ChangeType { get; private set; }
    public Guid? FromPlanId { get; private set; }
    public Guid? ToPlanId { get; private set; }
    public Guid? FromPriceId { get; private set; }
    public Guid? ToPriceId { get; private set; }
    public DateTime EffectiveAtUtc { get; private set; }
    public ProrationBehavior ProrationBehavior { get; private set; }
    public string? Reason { get; private set; }
    public string? MetadataJson { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private SubscriptionChange() { }

    public static SubscriptionChange Create(
        Guid subscriptionId,
        SubscriptionChangeType changeType,
        Guid? fromPlanId,
        Guid? toPlanId,
        Guid? fromPriceId,
        Guid? toPriceId,
        DateTime effectiveAtUtc,
        ProrationBehavior prorationBehavior,
        string? reason,
        string? metadataJson,
        DateTime nowUtc)
    {
        return new SubscriptionChange
        {
            Id = Guid.NewGuid(),
            SubscriptionId = subscriptionId,
            ChangeType = changeType,
            FromPlanId = fromPlanId,
            ToPlanId = toPlanId,
            FromPriceId = fromPriceId,
            ToPriceId = toPriceId,
            EffectiveAtUtc = effectiveAtUtc,
            ProrationBehavior = prorationBehavior,
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            MetadataJson = string.IsNullOrWhiteSpace(metadataJson) ? null : metadataJson,
            CreatedAtUtc = nowUtc
        };
    }
}
