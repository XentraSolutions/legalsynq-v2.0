using Commerce.Domain.Catalog.Enums;
using Commerce.Domain.Common;
using Commerce.Domain.Subscriptions.Enums;

namespace Commerce.Domain.Subscriptions;

public sealed class SubscriptionItem : Entity<Guid>
{
    public Guid SubscriptionId { get; private set; }
    public Guid PlanId { get; private set; }
    public Guid PriceId { get; private set; }
    public int Quantity { get; private set; }
    public long UnitAmountMinor { get; private set; }
    public string Currency { get; private set; } = default!;
    public BillingInterval BillingInterval { get; private set; }
    public SubscriptionItemStatus Status { get; private set; }
    public DateTime EffectiveFromUtc { get; private set; }
    public DateTime? EffectiveToUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private SubscriptionItem() { }

    public static SubscriptionItem Create(
        Guid subscriptionId,
        Guid planId,
        Guid priceId,
        int quantity,
        long unitAmountMinor,
        string currency,
        BillingInterval interval,
        DateTime effectiveFromUtc,
        DateTime nowUtc)
    {
        if (quantity < 1)
            throw new InvalidOperationException("Quantity must be >= 1.");
        if (unitAmountMinor < 0)
            throw new InvalidOperationException("UnitAmountMinor must be >= 0.");
        if (string.IsNullOrWhiteSpace(currency))
            throw new InvalidOperationException("Currency is required.");

        return new SubscriptionItem
        {
            Id = Guid.NewGuid(),
            SubscriptionId = subscriptionId,
            PlanId = planId,
            PriceId = priceId,
            Quantity = quantity,
            UnitAmountMinor = unitAmountMinor,
            Currency = currency.Trim().ToUpperInvariant(),
            BillingInterval = interval,
            Status = SubscriptionItemStatus.Active,
            EffectiveFromUtc = effectiveFromUtc,
            EffectiveToUtc = null,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc
        };
    }

    public void Close(DateTime effectiveToUtc, DateTime nowUtc)
    {
        if (Status != SubscriptionItemStatus.Active && Status != SubscriptionItemStatus.PendingChange)
            throw new InvalidOperationException(
                $"SubscriptionItem in status '{Status}' cannot be closed.");
        if (effectiveToUtc <= EffectiveFromUtc)
            throw new InvalidOperationException("EffectiveToUtc must be after EffectiveFromUtc.");
        EffectiveToUtc = effectiveToUtc;
        Status = SubscriptionItemStatus.Expired;
        UpdatedAtUtc = nowUtc;
    }

    public void CancelImmediate(DateTime nowUtc)
    {
        Status = SubscriptionItemStatus.Cancelled;
        EffectiveToUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }
}
