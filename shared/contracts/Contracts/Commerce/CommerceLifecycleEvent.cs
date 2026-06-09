namespace Contracts.Commerce;

/// <summary>
/// Self-contained lifecycle event envelope for Commerce ecosystem events.
///
/// <para>
/// This record is used by the <c>ICommerceLifecycleNotifier</c> in
/// <c>BuildingBlocks</c> and by any message bus adapters that broadcast
/// Commerce state changes across the platform. It is intentionally
/// de-coupled from Commerce's internal domain models — <c>ExternalTenantId</c>
/// maps to the host platform's tenant identifier, not to Commerce's internal
/// billing account primary key.
/// </para>
///
/// <para>
/// Use <see cref="CommerceEventTypes"/> constants for <see cref="EventType"/>
/// values to ensure compile-time consistency.
/// </para>
/// </summary>
/// <param name="EventType">
/// Canonical event type string — use <see cref="CommerceEventTypes"/> constants.
/// </param>
/// <param name="HostPlatformKey">
/// Stable identifier for the host platform (e.g. <c>"legalsynq"</c>).
/// </param>
/// <param name="ExternalTenantId">
/// The host platform's tenant identifier (e.g. LegalSynq tenant GUID string).
/// </param>
/// <param name="OccurredAtUtc">UTC timestamp the event occurred.</param>
/// <param name="CorrelationId">
/// Optional correlation id linking this event to a broader operation trace.
/// </param>
/// <param name="BillingAccountId">
/// Optional Commerce billing account id (Commerce-side identifier).
/// </param>
/// <param name="SubscriptionId">Optional subscription id, when event concerns a specific subscription.</param>
/// <param name="ProductKey">Optional catalog product key, when event concerns a specific product.</param>
/// <param name="PlanKey">Optional catalog plan key, when event concerns a specific plan.</param>
/// <param name="AccessRecommendation">
/// Optional new access recommendation string (see <c>CommerceAccessRecommendations</c>),
/// populated when the event is <see cref="CommerceEventTypes.AccessRecommendationChanged"/>.
/// </param>
/// <param name="Metadata">
/// Optional open-ended key/value pairs for event-specific contextual data.
/// Producers should document expected keys; consumers must handle missing keys gracefully.
/// </param>
public sealed record CommerceLifecycleEvent(
    string                                  EventType,
    string                                  HostPlatformKey,
    string                                  ExternalTenantId,
    DateTimeOffset                          OccurredAtUtc,
    string?                                 CorrelationId        = null,
    string?                                 BillingAccountId     = null,
    string?                                 SubscriptionId       = null,
    string?                                 ProductKey           = null,
    string?                                 PlanKey              = null,
    string?                                 AccessRecommendation = null,
    IReadOnlyDictionary<string, string>?    Metadata             = null);

/// <summary>
/// Canonical string values for the <see cref="CommerceLifecycleEvent.AccessRecommendation"/> field.
/// Mirrors the <c>AccessRecommendation</c> enum in <c>Commerce.Contracts.Integration</c>
/// without introducing a cross-project dependency.
/// </summary>
public static class CommerceAccessRecommendations
{
    /// <summary>Commerce has no opinion (e.g. tenant is unknown to Commerce).</summary>
    public const string Unknown      = "Unknown";

    /// <summary>Full commercial access is permitted.</summary>
    public const string Allow        = "Allow";

    /// <summary>Read-only access recommended — no active subscription.</summary>
    public const string ReadOnly     = "ReadOnly";

    /// <summary>
    /// Grace-period limited access — account is inside billing grace window.
    /// </summary>
    public const string GraceLimited = "GraceLimited";

    /// <summary>Access should be blocked — account suspended or closed.</summary>
    public const string Block        = "Block";
}
