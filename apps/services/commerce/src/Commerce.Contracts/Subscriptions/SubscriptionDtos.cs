using Commerce.Domain.Catalog.Enums;
using Commerce.Domain.Subscriptions.Enums;

namespace Commerce.Contracts.Subscriptions;

public sealed record CreateSubscriptionRequest(
    Guid BillingAccountId,
    Guid PlanId,
    Guid PriceId,
    int Quantity,
    DateTime? StartDateUtc = null,
    int? TrialDays = null,
    string? MetadataJson = null);

public sealed record ChangeSubscriptionPlanRequest(
    Guid NewPlanId,
    Guid NewPriceId,
    int? Quantity,
    DateTime? EffectiveAtUtc,
    ProrationBehavior ProrationBehavior,
    string? Reason = null,
    string? MetadataJson = null);

public sealed record CancelSubscriptionRequest(
    bool CancelAtPeriodEnd,
    string? Reason = null);

public sealed record RenewSubscriptionRequest(
    DateTime? NewPeriodStartUtc = null);

public sealed record SubscriptionItemResponse(
    Guid Id,
    Guid SubscriptionId,
    Guid PlanId,
    Guid PriceId,
    int Quantity,
    long UnitAmountMinor,
    string Currency,
    BillingInterval BillingInterval,
    SubscriptionItemStatus Status,
    DateTime EffectiveFromUtc,
    DateTime? EffectiveToUtc,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record SubscriptionResponse(
    Guid Id,
    Guid BillingAccountId,
    string SubscriptionNumber,
    SubscriptionStatus Status,
    DateTime StartDateUtc,
    DateTime CurrentPeriodStartUtc,
    DateTime CurrentPeriodEndUtc,
    DateTime? TrialStartUtc,
    DateTime? TrialEndUtc,
    bool CancelAtPeriodEnd,
    DateTime? CancelledAtUtc,
    string? CancellationReason,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    IReadOnlyList<SubscriptionItemResponse> Items);

public sealed record SubscriptionChangeResponse(
    Guid Id,
    Guid SubscriptionId,
    SubscriptionChangeType ChangeType,
    Guid? FromPlanId,
    Guid? ToPlanId,
    Guid? FromPriceId,
    Guid? ToPriceId,
    DateTime EffectiveAtUtc,
    ProrationBehavior ProrationBehavior,
    string? Reason,
    string? MetadataJson,
    DateTime CreatedAtUtc);
