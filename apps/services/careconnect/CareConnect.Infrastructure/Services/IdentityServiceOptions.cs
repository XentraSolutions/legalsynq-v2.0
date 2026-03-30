namespace CareConnect.Infrastructure.Services;

/// <summary>
/// Configuration for the Identity service HTTP client used by
/// HttpOrganizationRelationshipResolver and any other cross-service calls.
///
/// Bind from appsettings via:
///   "IdentityService": {
///     "BaseUrl":           "http://identity-service:5001",
///     "TimeoutSeconds":    5,
///     "AuthHeaderName":    "X-Service-Token",   // optional
///     "AuthHeaderValue":   "my-secret-value"    // optional
///   }
/// </summary>
public sealed class IdentityServiceOptions
{
    public const string SectionName = "IdentityService";

    /// <summary>
    /// Base URL of the Identity service, e.g. http://identity:5001 or https://gateway/identity.
    /// When null or empty, the HTTP resolver returns null without making any network call.
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// Per-request HTTP timeout in seconds. Defaults to 5 s.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 5;

    /// <summary>
    /// Optional service-to-service auth header name, e.g. "X-Service-Token" or "Authorization".
    /// Applied only when both AuthHeaderName and AuthHeaderValue are non-empty.
    /// Leave blank for environments without service-level auth.
    /// </summary>
    public string? AuthHeaderName { get; set; }

    /// <summary>
    /// Value of the service-to-service auth header.
    /// Set via environment variable / secret — never commit real values to appsettings.
    /// </summary>
    public string? AuthHeaderValue { get; set; }
}
