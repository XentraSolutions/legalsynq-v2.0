namespace Contracts.DeepLinks;

/// <summary>
/// Runtime-neutral metadata for one supported deep-link route. Definitions are loaded from the
/// authoritative <c>shared/contracts/deep-links/routes.json</c> artifact.
/// </summary>
public sealed record DeepLinkRouteDefinition(
    string Key,
    string PathTemplate,
    string MobileDestination,
    bool RequiresAuthentication,
    bool RequiresAuthorization,
    IReadOnlyList<string> RequiredPathParameters,
    IReadOnlyList<string> OptionalQueryParameters,
    string FallbackDestination,
    string AnalyticsEvent,
    bool Enabled);

/// <summary>
/// Versioned deep-link route registry contract.
/// </summary>
public sealed record DeepLinkRouteRegistryDocument(
    int Version,
    IReadOnlyList<DeepLinkRouteDefinition> Routes);
