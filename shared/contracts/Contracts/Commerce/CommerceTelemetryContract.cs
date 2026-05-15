namespace Contracts.Commerce;

/// <summary>
/// Point-in-time Commerce integration health telemetry reported by a consuming
/// service. Surfaced by each service's <c>/health</c> or
/// <c>/api/v1/ready</c> responses when Commerce integration is configured.
///
/// <para>
/// This contract is read by the Monitoring service and Control Center to
/// build a platform-wide view of Commerce entitlement health across all
/// product services. Services that do not wire Commerce integration should
/// report <see cref="EntitlementStatus"/> = <c>"disabled"</c> and
/// <see cref="CommerceEnabled"/> = <c>false</c>.
/// </para>
///
/// <para>
/// Use <see cref="CommerceEntitlementStatusValues"/> for the
/// <see cref="EntitlementStatus"/> string values.
/// </para>
/// </summary>
/// <param name="ServiceName">Stable display name of the reporting service.</param>
/// <param name="HostPlatformKey">Host platform key used by this service when calling Commerce.</param>
/// <param name="CommerceEnabled">Whether Commerce HTTP integration is wired and enabled.</param>
/// <param name="CommerceBaseUrl">Commerce service base URL this service is configured to call; null if disabled.</param>
/// <param name="EntitlementClientWired">Whether <c>ICommerceEntitlementClient</c> is registered as a real HTTP client.</param>
/// <param name="LifecycleNotifierWired">Whether <c>ICommerceLifecycleNotifier</c> is registered as a real notifier.</param>
/// <param name="EntitlementStatus">
/// Current entitlement check health status.
/// Use <see cref="CommerceEntitlementStatusValues"/> constants.
/// </param>
/// <param name="EntitlementError">Error message when <paramref name="EntitlementStatus"/> = <c>"error"</c>; null otherwise.</param>
/// <param name="LastSuccessfulCheckUtc">UTC timestamp of the last successful entitlement check; null if never checked.</param>
/// <param name="ReportedAtUtc">UTC timestamp this telemetry snapshot was generated.</param>
public sealed record CommerceTelemetryContract(
    string          ServiceName,
    string          HostPlatformKey,
    bool            CommerceEnabled,
    string?         CommerceBaseUrl,
    bool            EntitlementClientWired,
    bool            LifecycleNotifierWired,
    string          EntitlementStatus,
    string?         EntitlementError,
    DateTimeOffset? LastSuccessfulCheckUtc,
    DateTimeOffset  ReportedAtUtc);

/// <summary>
/// Standard string values for <see cref="CommerceTelemetryContract.EntitlementStatus"/>.
/// </summary>
public static class CommerceEntitlementStatusValues
{
    /// <summary>Last check succeeded and the snapshot is within the freshness threshold.</summary>
    public const string Ok          = "ok";

    /// <summary>Last check succeeded but the snapshot is older than the configured stale threshold.</summary>
    public const string Stale       = "stale";

    /// <summary>Last check failed with an HTTP or network error.</summary>
    public const string Error       = "error";

    /// <summary>No check has been performed since startup.</summary>
    public const string NotChecked  = "not_checked";

    /// <summary>Commerce integration is disabled; the noop client is in use.</summary>
    public const string Disabled    = "disabled";
}
