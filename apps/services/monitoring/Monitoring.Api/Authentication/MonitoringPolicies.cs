namespace Monitoring.Api.Authentication;

/// <summary>
/// Authorization policy name constants for the Monitoring API.
/// Defined here so endpoints and tests can reference them without magic strings.
/// </summary>
public static class MonitoringPolicies
{
    /// <summary>
    /// Grants access to write/admin endpoints (entity create, update).
    /// Satisfied by either:
    /// <list type="bullet">
    ///   <item>A valid user JWT with the <c>PlatformAdmin</c> role (Bearer scheme), or</item>
    ///   <item>A valid service token with subject <c>service:*</c> (ServiceToken scheme).</item>
    /// </list>
    /// </summary>
    public const string AdminWrite = "MonitoringAdmin";
}
