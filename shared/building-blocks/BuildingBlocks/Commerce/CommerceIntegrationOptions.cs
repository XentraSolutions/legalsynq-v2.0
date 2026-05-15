namespace BuildingBlocks.Commerce;

/// <summary>
/// Configuration options for the platform-wide Commerce integration layer.
///
/// <para>
/// Bind this class from the <c>CommerceIntegration</c> configuration section
/// (e.g. <c>appsettings.json</c>):
/// <code>
/// {
///   "CommerceIntegration": {
///     "Enabled": false,
///     "BaseUrl": "http://127.0.0.1:5030",
///     "HostPlatformKey": "legalsynq",
///     "TimeoutSeconds": 10,
///     "StaleThresholdSeconds": 3600
///   }
/// }
/// </code>
/// </para>
///
/// <para>
/// Set <c>Enabled = true</c> to replace the noop implementations with real
/// HTTP clients. When <c>false</c> (default), no Commerce calls are made and
/// services operate in permissive standalone mode.
/// </para>
/// </summary>
public sealed class CommerceIntegrationOptions
{
    /// <summary>Configuration section key.</summary>
    public const string SectionName = "CommerceIntegration";

    /// <summary>
    /// Master switch. When <c>false</c> the noop implementations are used and
    /// no HTTP calls to Commerce are made. Defaults to <c>false</c>.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Commerce service base URL. Defaults to the standard dev-environment
    /// address; override per environment via configuration or environment vars.
    /// </summary>
    public string BaseUrl { get; set; } = "http://127.0.0.1:5030";

    /// <summary>
    /// Host platform key registered in Commerce for this LegalSynq deployment.
    /// Used as the default <c>hostPlatformKey</c> in snapshot calls.
    /// </summary>
    public string HostPlatformKey { get; set; } = "legalsynq";

    /// <summary>HTTP timeout for Commerce API calls in seconds. Defaults to 10.</summary>
    public int TimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Age in seconds after which a cached entitlement snapshot is considered
    /// stale for telemetry reporting purposes. Defaults to 3 600 (1 hour).
    /// Does NOT control in-memory caching — caching is not added at this layer.
    /// </summary>
    public int StaleThresholdSeconds { get; set; } = 3600;

    /// <summary>
    /// Optional bearer token for internal service-to-service Commerce calls.
    /// When set, the HTTP client attaches this as an <c>Authorization: Bearer</c>
    /// header. Leave null to skip the header (Commerce's integration endpoints
    /// may be open to internal network traffic).
    /// </summary>
    public string? InternalServiceToken { get; set; }
}
