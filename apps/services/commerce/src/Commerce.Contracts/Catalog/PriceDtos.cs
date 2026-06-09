using Commerce.Domain.Catalog.Enums;

namespace Commerce.Contracts.Catalog;

public sealed record CreatePriceRequest(
    Guid? PlanId,
    Guid? AddonId,
    Guid? BundleId,
    string Currency,
    long AmountMinor,
    BillingInterval BillingInterval,
    DateTime EffectiveFromUtc,
    DateTime? EffectiveToUtc);

public sealed record UpdatePriceRequest(
    string Currency,
    long AmountMinor,
    BillingInterval BillingInterval,
    DateTime EffectiveFromUtc,
    DateTime? EffectiveToUtc);

public sealed record PriceResponse(
    Guid Id,
    Guid? PlanId,
    Guid? AddonId,
    Guid? BundleId,
    string Currency,
    long AmountMinor,
    BillingInterval BillingInterval,
    CatalogStatus Status,
    DateTime EffectiveFromUtc,
    DateTime? EffectiveToUtc,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
