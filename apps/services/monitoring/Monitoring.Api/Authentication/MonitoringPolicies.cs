namespace Monitoring.Api.Authentication;

/// <summary>
/// Authorization policy name constants for the Monitoring API.
/// Defined here so endpoints and tests can reference them without magic strings.
/// </summary>
public static class MonitoringPolicies
{
    /// <summary>
    /// Grants access to write/admin endpoints (entity create, update, alert resolve).
    /// Satisfied only by a valid user JWT with the <c>PlatformAdmin</c> role (Bearer scheme).
    /// Service tokens are explicitly excluded to prevent privilege escalation through
    /// compromised platform services or the shared service-token secret.
    /// </summary>
    public const string AdminWrite = "MonitoringAdmin";

    /// <summary>
    /// Grants read-only access to monitoring status, summary, alerts, and uptime endpoints.
    /// Satisfied by either a valid user JWT (Bearer scheme) or a valid service token
    /// (ServiceToken scheme). This allows the Control Center BFF and other platform
    /// services to call these endpoints from the server side.
    /// </summary>
    public const string Read = "MonitoringRead";
}
