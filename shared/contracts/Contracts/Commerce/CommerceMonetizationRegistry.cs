namespace Contracts.Commerce;

/// <summary>
/// Lightweight static monetization registry describing how each LegalSynq
/// product/service participates in the Commerce ecosystem.
///
/// <para>
/// This registry is the platform's authoritative lightweight reference for:
/// <list type="bullet">
///   <item><description>Which products are subscription-gated.</description></item>
///   <item><description>Which services are currently monetization-enabled.</description></item>
///   <item><description>Default access mode and operational criticality.</description></item>
/// </list>
/// </para>
///
/// <para>
/// <b>Scope:</b> Platform / SaaS billing only (platform → tenant). This registry
/// has nothing to do with tenant billing (tenant → customer/client).
/// </para>
///
/// <para>
/// The registry is static for simplicity and build-time safety. A future
/// phase can replace or supplement it with a DB-backed catalog entry fetched
/// from the Commerce service when a dynamic catalog becomes necessary.
/// </para>
/// </summary>
public static class CommerceMonetizationRegistry
{
    private static readonly Dictionary<string, CommerceProductRegistryEntry> _byKey;

    static CommerceMonetizationRegistry()
    {
        _byKey = All.ToDictionary(e => e.ProductKey, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// All registered product/service entries in the Commerce monetization registry.
    /// </summary>
    public static readonly IReadOnlyList<CommerceProductRegistryEntry> All =
        new List<CommerceProductRegistryEntry>
        {
            // ── Monetized product services ───────────────────────────────────

            new(
                ProductKey:             "SYNQLIEN",
                DisplayName:            "SynqLien",
                ServiceName:            "Liens",
                EntitlementKey:         "synqlien.access",
                SubscriptionRequired:   true,
                MonetizationEnabled:    false,
                EnforcementEnabled:     false,
                DefaultAccessMode:      CommerceAccessModeValues.Permissive,
                OperationalCriticality: CommerceOperationalCriticality.High),

            new(
                ProductKey:             "SYNQFUND",
                DisplayName:            "SynqFund",
                ServiceName:            "Fund",
                EntitlementKey:         "synqfund.access",
                SubscriptionRequired:   true,
                MonetizationEnabled:    false,
                EnforcementEnabled:     false,
                DefaultAccessMode:      CommerceAccessModeValues.Permissive,
                OperationalCriticality: CommerceOperationalCriticality.High),

            new(
                ProductKey:             "CARECONNECT",
                DisplayName:            "CareConnect",
                ServiceName:            "CareConnect",
                EntitlementKey:         "careconnect.access",
                SubscriptionRequired:   true,
                MonetizationEnabled:    false,
                EnforcementEnabled:     false,
                DefaultAccessMode:      CommerceAccessModeValues.Permissive,
                OperationalCriticality: CommerceOperationalCriticality.High),

            new(
                ProductKey:             "SYNQ_AI",
                DisplayName:            "Xenia",
                ServiceName:            "Xenia",
                EntitlementKey:         "xenia.access",
                SubscriptionRequired:   true,
                MonetizationEnabled:    false,
                EnforcementEnabled:     false,
                DefaultAccessMode:      CommerceAccessModeValues.Permissive,
                OperationalCriticality: CommerceOperationalCriticality.High),

            // ── Platform infrastructure services (not monetized) ─────────────

            new(
                ProductKey:             "PLATFORM_IDENTITY",
                DisplayName:            "Identity",
                ServiceName:            "Identity",
                EntitlementKey:         null,
                SubscriptionRequired:   false,
                MonetizationEnabled:    false,
                EnforcementEnabled:     false,
                DefaultAccessMode:      CommerceAccessModeValues.Open,
                OperationalCriticality: CommerceOperationalCriticality.Critical),

            new(
                ProductKey:             "PLATFORM_TENANT",
                DisplayName:            "Tenant Registry",
                ServiceName:            "Tenant",
                EntitlementKey:         null,
                SubscriptionRequired:   false,
                MonetizationEnabled:    false,
                EnforcementEnabled:     false,
                DefaultAccessMode:      CommerceAccessModeValues.Open,
                OperationalCriticality: CommerceOperationalCriticality.Critical),

            new(
                ProductKey:             "PLATFORM_AUDIT",
                DisplayName:            "Audit",
                ServiceName:            "Audit",
                EntitlementKey:         null,
                SubscriptionRequired:   false,
                MonetizationEnabled:    false,
                EnforcementEnabled:     false,
                DefaultAccessMode:      CommerceAccessModeValues.Open,
                OperationalCriticality: CommerceOperationalCriticality.Critical),

            new(
                ProductKey:             "PLATFORM_NOTIFICATIONS",
                DisplayName:            "Notifications",
                ServiceName:            "Notifications",
                EntitlementKey:         null,
                SubscriptionRequired:   false,
                MonetizationEnabled:    false,
                EnforcementEnabled:     false,
                DefaultAccessMode:      CommerceAccessModeValues.Open,
                OperationalCriticality: CommerceOperationalCriticality.High),

            new(
                ProductKey:             "PLATFORM_MONITORING",
                DisplayName:            "Monitoring",
                ServiceName:            "Monitoring",
                EntitlementKey:         null,
                SubscriptionRequired:   false,
                MonetizationEnabled:    false,
                EnforcementEnabled:     false,
                DefaultAccessMode:      CommerceAccessModeValues.Open,
                OperationalCriticality: CommerceOperationalCriticality.High),
        }.AsReadOnly();

    /// <summary>
    /// Look up a registry entry by product key (case-insensitive).
    /// Returns <c>null</c> when the key is not registered.
    /// </summary>
    public static CommerceProductRegistryEntry? GetByProductKey(string productKey)
        => _byKey.TryGetValue(productKey, out var entry) ? entry : null;

    /// <summary>
    /// Returns all entries that are currently monetization-enabled.
    /// </summary>
    public static IEnumerable<CommerceProductRegistryEntry> GetMonetizationEnabled()
        => All.Where(e => e.MonetizationEnabled);

    /// <summary>
    /// Returns all entries for subscription-required products.
    /// </summary>
    public static IEnumerable<CommerceProductRegistryEntry> GetSubscriptionRequired()
        => All.Where(e => e.SubscriptionRequired);
}

/// <summary>
/// Monetization registry entry describing a single product or service's
/// participation in the Commerce ecosystem.
/// </summary>
/// <param name="ProductKey">Stable Commerce catalog product key (e.g. <c>"SYNQLIEN"</c>).</param>
/// <param name="DisplayName">Human-readable product/service name.</param>
/// <param name="ServiceName">Internal service identifier (aligns with <c>CommerceServiceMetadata.ServiceName</c>).</param>
/// <param name="EntitlementKey">
/// Commerce feature/entitlement key checked during access evaluation.
/// <c>null</c> for platform infrastructure services that are not entitlement-gated.
/// </param>
/// <param name="SubscriptionRequired">
/// When <c>true</c>, an active subscription covering this product key is expected
/// for full commercial access.
/// </param>
/// <param name="MonetizationEnabled">
/// <c>true</c> when the service is actively enforcing or reporting entitlement checks.
/// Starts as <c>false</c> during rollout; flipped to <c>true</c> once enforcement is validated.
/// </param>
/// <param name="EnforcementEnabled">
/// <c>true</c> when the service will actively deny/degrade access based on entitlement result.
/// Independent of <see cref="MonetizationEnabled"/> — a service can be monetization-enabled
/// (reporting) without hard enforcement.
/// </param>
/// <param name="DefaultAccessMode">
/// Default access posture when Commerce is unavailable or integration is disabled.
/// Use <see cref="CommerceAccessModeValues"/> constants.
/// </param>
/// <param name="OperationalCriticality">
/// Service criticality classification. Use <see cref="CommerceOperationalCriticality"/> constants.
/// </param>
public sealed record CommerceProductRegistryEntry(
    string  ProductKey,
    string  DisplayName,
    string  ServiceName,
    string? EntitlementKey,
    bool    SubscriptionRequired,
    bool    MonetizationEnabled,
    bool    EnforcementEnabled,
    string  DefaultAccessMode,
    string  OperationalCriticality);

/// <summary>
/// Standard string values for <see cref="CommerceProductRegistryEntry.DefaultAccessMode"/>.
/// </summary>
public static class CommerceAccessModeValues
{
    /// <summary>Open access — no Commerce gate, always allowed.</summary>
    public const string Open        = "open";

    /// <summary>Permissive fallback — allowed when Commerce is unavailable or integration disabled.</summary>
    public const string Permissive  = "permissive";

    /// <summary>Restricted — degraded access when entitlement is absent.</summary>
    public const string Restricted  = "restricted";

    /// <summary>Enforced — access denied when valid entitlement cannot be confirmed.</summary>
    public const string Enforced    = "enforced";
}

/// <summary>
/// Standard string values for <see cref="CommerceProductRegistryEntry.OperationalCriticality"/>.
/// </summary>
public static class CommerceOperationalCriticality
{
    /// <summary>Platform will not function without this service.</summary>
    public const string Critical = "critical";

    /// <summary>Product functionality significantly impaired without this service.</summary>
    public const string High     = "high";

    /// <summary>Useful but platform remains functional without this service.</summary>
    public const string Medium   = "medium";

    /// <summary>Optional feature or tooling.</summary>
    public const string Low      = "low";
}
