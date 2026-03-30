namespace CareConnect.Infrastructure.Services;

/// <summary>
/// Configuration for the Identity service HTTP client used by
/// HttpOrganizationRelationshipResolver and any other cross-service calls.
///
/// Bind from appsettings via:
///   "IdentityService": {
///     "BaseUrl": "http://identity-service:5001",
///     "TimeoutSeconds": 5
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
}
