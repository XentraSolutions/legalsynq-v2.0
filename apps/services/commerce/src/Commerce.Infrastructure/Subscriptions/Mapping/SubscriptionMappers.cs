using Commerce.Contracts.Subscriptions;
using Commerce.Domain.Subscriptions;

namespace Commerce.Infrastructure.Subscriptions.Mapping;

internal static class SubscriptionMappers
{
    public static SubscriptionItemResponse ToResponse(this SubscriptionItem e) => new(
        e.Id, e.SubscriptionId, e.PlanId, e.PriceId, e.Quantity, e.UnitAmountMinor,
        e.Currency, e.BillingInterval, e.Status, e.EffectiveFromUtc, e.EffectiveToUtc,
        e.CreatedAtUtc, e.UpdatedAtUtc);

    public static SubscriptionResponse ToResponse(this Subscription e, IEnumerable<SubscriptionItem> items) => new(
        e.Id, e.BillingAccountId, e.SubscriptionNumber, e.Status,
        e.StartDateUtc, e.CurrentPeriodStartUtc, e.CurrentPeriodEndUtc,
        e.TrialStartUtc, e.TrialEndUtc, e.CancelAtPeriodEnd, e.CancelledAtUtc,
        e.CancellationReason, e.CreatedAtUtc, e.UpdatedAtUtc,
        items.Select(ToResponse).ToList());

    public static SubscriptionChangeResponse ToResponse(this SubscriptionChange e) => new(
        e.Id, e.SubscriptionId, e.ChangeType, e.FromPlanId, e.ToPlanId,
        e.FromPriceId, e.ToPriceId, e.EffectiveAtUtc, e.ProrationBehavior,
        e.Reason, e.MetadataJson, e.CreatedAtUtc);
}
