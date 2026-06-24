namespace Commerce.Contracts.Integration;

/// <summary>
/// Response shape for the access-recommendation endpoint. Pairs the
/// recommendation enum with an explanation so a host UI / log can show
/// why Commerce reached this conclusion.
/// </summary>
public sealed record AccessRecommendationResponse(
    Guid BillingAccountId,
    string? HostPlatformKey,
    string? ExternalTenantId,
    AccessRecommendation Recommendation,
    string Reason,
    string AccountStandingStatus,
    bool HasActiveOrTrialingSubscription,
    DateTime GeneratedAtUtc);
