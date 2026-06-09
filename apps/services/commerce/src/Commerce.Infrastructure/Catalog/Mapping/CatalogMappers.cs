using Commerce.Contracts.Catalog;
using Commerce.Domain.Catalog;

namespace Commerce.Infrastructure.Catalog.Mapping;

internal static class CatalogMappers
{
    public static ProductResponse ToResponse(this Product e) =>
        new(e.Id, e.Key, e.Name, e.Description, e.Status, e.SortOrder, e.CreatedAtUtc, e.UpdatedAtUtc);

    public static FeatureResponse ToResponse(this Feature e) =>
        new(e.Id, e.ProductId, e.Key, e.Name, e.Description, e.FeatureType, e.Status, e.CreatedAtUtc, e.UpdatedAtUtc);

    public static PlanResponse ToResponse(this Plan e) =>
        new(e.Id, e.ProductId, e.Key, e.Name, e.Description, e.Status, e.BillingInterval, e.TrialDays, e.SortOrder, e.CreatedAtUtc, e.UpdatedAtUtc);

    public static PlanFeatureResponse ToResponse(this PlanFeature e) =>
        new(e.Id, e.PlanId, e.FeatureId, e.IsEnabled, e.LimitValue, e.MeteredIncludedUnits, e.CreatedAtUtc, e.UpdatedAtUtc);

    public static AddonResponse ToResponse(this Addon e) =>
        new(e.Id, e.ProductId, e.Key, e.Name, e.Description, e.Status, e.CreatedAtUtc, e.UpdatedAtUtc);

    public static BundleResponse ToResponse(this Bundle e) =>
        new(e.Id, e.Key, e.Name, e.Description, e.Status, e.CreatedAtUtc, e.UpdatedAtUtc);

    public static BundleItemResponse ToResponse(this BundleItem e) =>
        new(e.Id, e.BundleId, e.ProductId, e.PlanId, e.AddonId, e.CreatedAtUtc);

    public static PriceResponse ToResponse(this Price e) =>
        new(e.Id, e.PlanId, e.AddonId, e.BundleId, e.Currency, e.AmountMinor, e.BillingInterval, e.Status, e.EffectiveFromUtc, e.EffectiveToUtc, e.CreatedAtUtc, e.UpdatedAtUtc);
}
