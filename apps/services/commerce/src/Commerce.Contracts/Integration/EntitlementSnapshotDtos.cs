namespace Commerce.Contracts.Integration;

/// <summary>
/// Aggregate "what does this account commercially have right now"
/// snapshot. Output-only — Commerce publishes; the host consumes and
/// enforces. The snapshot intentionally omits payment provider raw
/// payloads, secrets, and any Stripe event JSON.
/// </summary>
public sealed record CommerceEntitlementSnapshot(
    Guid BillingAccountId,
    string AccountNumber,
    string DisplayName,
    string? HostPlatformKey,
    string? ExternalTenantId,
    string AccountStandingStatus,
    string? AccountStandingReason,
    DateTime? AccountStandingGracePeriodEndsAtUtc,
    AccessRecommendation AccessRecommendation,
    IReadOnlyList<EntitlementProductRef> Products,
    IReadOnlyList<EntitlementPlanRef> Plans,
    IReadOnlyList<EntitlementSubscriptionRef> Subscriptions,
    IReadOnlyList<EntitlementFeatureLimit> Limits,
    DateTime GeneratedAtUtc);

public sealed record EntitlementProductRef(
    Guid ProductId,
    string ProductKey,
    string ProductName);

public sealed record EntitlementPlanRef(
    Guid PlanId,
    string PlanKey,
    string PlanName,
    Guid? ProductId,
    string? ProductKey);

public sealed record EntitlementSubscriptionRef(
    Guid SubscriptionId,
    string SubscriptionNumber,
    string Status,
    DateTime CurrentPeriodStartUtc,
    DateTime CurrentPeriodEndUtc,
    DateTime? TrialEndUtc,
    bool CancelAtPeriodEnd,
    IReadOnlyList<EntitlementSubscriptionItemRef> Items);

public sealed record EntitlementSubscriptionItemRef(
    Guid SubscriptionItemId,
    Guid PlanId,
    string PlanKey,
    int Quantity);

public sealed record EntitlementFeatureLimit(
    Guid PlanId,
    string PlanKey,
    Guid FeatureId,
    string FeatureKey,
    string FeatureName,
    bool IsEnabled,
    long? LimitValue,
    long? MeteredIncludedUnits);

/// <summary>
/// Trivial response for the contracts health endpoint. Confirms the
/// integration surface area is reachable and reports which interfaces
/// are wired.
/// </summary>
public sealed record IntegrationContractsHealthResponse(
    string Status,
    string IdentityContextAccessor,
    string TenantResolver,
    string ProvisioningHookPublisher,
    DateTime GeneratedAtUtc);
