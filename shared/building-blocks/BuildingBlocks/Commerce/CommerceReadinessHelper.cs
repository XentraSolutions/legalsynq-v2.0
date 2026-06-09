using Contracts.Commerce;

namespace BuildingBlocks.Commerce;

/// <summary>
/// Utility for building <see cref="CommerceTelemetryContract"/> snapshots in
/// service health/readiness endpoints.
///
/// <para>
/// Services that have adopted <see cref="ICommerceEntitlementClient"/> can
/// include a Commerce telemetry block in their readiness response by calling
/// <see cref="BuildFromOptions"/> (for configuration-only status) or
/// <see cref="BuildTelemetry"/> (for full runtime status with last-check
/// tracking).
/// </para>
///
/// <para>
/// Example usage in a readiness endpoint:
/// <code>
/// var commerce = CommerceReadinessHelper.BuildFromOptions(
///     "Synq Liens",
///     app.Services.GetRequiredService&lt;IOptions&lt;CommerceIntegrationOptions&gt;&gt;().Value);
/// return Results.Ok(new { status = "ok", commerce });
/// </code>
/// </para>
/// </summary>
public static class CommerceReadinessHelper
{
    /// <summary>
    /// Derives the appropriate <see cref="CommerceEntitlementStatusValues"/> string
    /// from current integration state and the last successful check timestamp.
    /// </summary>
    /// <param name="opts">Current Commerce integration options.</param>
    /// <param name="lastSuccessfulCheckUtc">UTC time of the last successful entitlement fetch; <c>null</c> if never checked.</param>
    /// <param name="hasError">Whether the most recent check failed with an error.</param>
    /// <param name="errorMessage">Error message when <paramref name="hasError"/> is <c>true</c>.</param>
    public static string GetEntitlementStatus(
        CommerceIntegrationOptions opts,
        DateTimeOffset?            lastSuccessfulCheckUtc,
        bool                       hasError      = false,
        string?                    errorMessage  = null)
    {
        if (!opts.Enabled)
            return CommerceEntitlementStatusValues.Disabled;

        if (hasError)
            return CommerceEntitlementStatusValues.Error;

        if (lastSuccessfulCheckUtc is null)
            return CommerceEntitlementStatusValues.NotChecked;

        var ageSeconds = (DateTimeOffset.UtcNow - lastSuccessfulCheckUtc.Value).TotalSeconds;
        return ageSeconds > opts.StaleThresholdSeconds
            ? CommerceEntitlementStatusValues.Stale
            : CommerceEntitlementStatusValues.Ok;
    }

    /// <summary>
    /// Builds a full <see cref="CommerceTelemetryContract"/> with all fields populated.
    /// </summary>
    public static CommerceTelemetryContract BuildTelemetry(
        string              serviceName,
        CommerceIntegrationOptions opts,
        bool                entitlementClientIsReal,
        bool                lifecycleNotifierIsReal,
        string              entitlementStatus,
        string?             entitlementError        = null,
        DateTimeOffset?     lastSuccessfulCheckUtc  = null)
        => new(
            ServiceName:            serviceName,
            HostPlatformKey:        opts.HostPlatformKey,
            CommerceEnabled:        opts.Enabled,
            CommerceBaseUrl:        opts.Enabled ? opts.BaseUrl : null,
            EntitlementClientWired: entitlementClientIsReal,
            LifecycleNotifierWired: lifecycleNotifierIsReal,
            EntitlementStatus:      entitlementStatus,
            EntitlementError:       entitlementError,
            LastSuccessfulCheckUtc: lastSuccessfulCheckUtc,
            ReportedAtUtc:          DateTimeOffset.UtcNow);

    /// <summary>
    /// Zero-check convenience overload — derives all status from options alone.
    /// Suitable for services that include Commerce telemetry in readiness responses
    /// without performing an active entitlement probe.
    /// </summary>
    public static CommerceTelemetryContract BuildFromOptions(
        string                     serviceName,
        CommerceIntegrationOptions opts)
    {
        var status = GetEntitlementStatus(opts, lastSuccessfulCheckUtc: null);
        return BuildTelemetry(
            serviceName:            serviceName,
            opts:                   opts,
            entitlementClientIsReal: opts.Enabled,
            lifecycleNotifierIsReal: opts.Enabled,
            entitlementStatus:      status);
    }
}
