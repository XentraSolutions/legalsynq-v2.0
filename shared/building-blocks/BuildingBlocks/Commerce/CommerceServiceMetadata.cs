namespace BuildingBlocks.Commerce;

/// <summary>
/// Declarative monetization metadata that a service attaches to itself to
/// indicate how it participates in the Commerce ecosystem.
///
/// <para>
/// Services that register <see cref="CommerceServiceMetadata"/> as a singleton
/// expose this via their <c>/health</c> or <c>/api/v1/ready</c> responses,
/// enabling the Monitoring layer and Control Center to build a platform-wide
/// view of which services enforce (or skip) entitlement checks.
/// </para>
///
/// <para>
/// Registration example (Program.cs):
/// <code>
/// services.AddSingleton(new CommerceServiceMetadata(
///     ServiceName:              "Synq Liens",
///     ProductKey:               "SYNQLIEN",
///     PrimaryFeatureKey:        null,
///     SubscriptionRequired:     true,
///     MonetizationEnabled:      false,    // not yet enforcing
///     CommerceIntegrationActive:false));
/// </code>
/// </para>
/// </summary>
/// <param name="ServiceName">Human-readable service name.</param>
/// <param name="ProductKey">
/// The Commerce catalog product key that licenses access to this service
/// (e.g. <c>"SYNQLIEN"</c>, <c>"SYNQFUND"</c>, <c>"CARECONNECT"</c>).
/// <c>null</c> for infrastructure services that are not monetized.
/// </param>
/// <param name="PrimaryFeatureKey">
/// Optional Commerce feature key for the primary capability gate
/// (e.g. <c>"lien.marketplace"</c>). When set, the service checks
/// this specific feature limit in addition to the product subscription.
/// </param>
/// <param name="SubscriptionRequired">
/// When <c>true</c>, this service should block or degrade when no active
/// Commerce subscription covering <see cref="ProductKey"/> is found.
/// When <c>false</c>, the service operates in open/freemium mode.
/// </param>
/// <param name="MonetizationEnabled">
/// <c>true</c> when the service is actively enforcing entitlement checks.
/// <c>false</c> during integration rollout or for services in permissive
/// open-access mode.
/// </param>
/// <param name="CommerceIntegrationActive">
/// <c>true</c> when the HTTP <c>ICommerceEntitlementClient</c> is wired
/// (not the noop implementation). Can be <c>true</c> while
/// <see cref="MonetizationEnabled"/> is <c>false</c> (fetching/logging
/// without enforcing).
/// </param>
public sealed record CommerceServiceMetadata(
    string  ServiceName,
    string? ProductKey,
    string? PrimaryFeatureKey,
    bool    SubscriptionRequired,
    bool    MonetizationEnabled,
    bool    CommerceIntegrationActive);
