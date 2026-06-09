namespace Commerce.Contracts.Catalog;

public sealed record AddPlanFeatureRequest(
    Guid FeatureId,
    bool IsEnabled,
    long? LimitValue,
    long? MeteredIncludedUnits);

public sealed record PlanFeatureResponse(
    Guid Id,
    Guid PlanId,
    Guid FeatureId,
    bool IsEnabled,
    long? LimitValue,
    long? MeteredIncludedUnits,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
