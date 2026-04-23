using Tenant.Application.DTOs;

namespace Tenant.Application.Interfaces;

/// <summary>
/// TENANT-B11 — Admin-focused read/write service for tenant management.
///
/// Aggregates data from multiple Tenant repositories plus Identity compat
/// reads, producing responses compatible with the control-center admin mappers.
/// </summary>
public interface ITenantAdminService
{
    /// <summary>
    /// Returns a paged list of tenants with fields compatible with the
    /// control-center <c>mapTenantSummary</c> mapper.
    /// </summary>
    Task<(List<TenantAdminSummaryResponse> Items, int Total)> ListAdminAsync(
        int page,
        int pageSize,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the full admin detail for a single tenant, including branding
    /// logos, entitlements, domain count, settings summary, and a
    /// read-through for Identity-owned fields such as sessionTimeoutMinutes.
    /// Returns <c>null</c> if the tenant does not exist in Tenant DB.
    /// </summary>
    Task<TenantAdminDetailResponse?> GetAdminDetailAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Updates only the status field of the tenant.
    /// Returns the updated admin detail response, or throws NotFoundException.
    /// </summary>
    Task<TenantAdminSummaryResponse> UpdateStatusAsync(Guid id, string status, CancellationToken ct = default);
}
