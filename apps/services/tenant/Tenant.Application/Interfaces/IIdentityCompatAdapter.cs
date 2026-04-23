namespace Tenant.Application.Interfaces;

/// <summary>
/// TENANT-B11 — Read-through adapter for Identity-owned compat data.
///
/// Used by TenantAdminService to fetch data that is still authoritative in
/// Identity (e.g. sessionTimeoutMinutes) for inclusion in the Tenant admin
/// aggregate response. All operations are non-blocking best-effort: a failure
/// or timeout results in a null/Unavailable value, not an exception.
/// </summary>
public interface IIdentityCompatAdapter
{
    /// <summary>
    /// Returns the per-tenant idle session timeout (minutes) from Identity,
    /// or <c>null</c> if Identity is unreachable or has no override configured.
    /// </summary>
    Task<int?> GetSessionTimeoutMinutesAsync(Guid tenantId, CancellationToken ct = default);
}
