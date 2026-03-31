// LSCC-010: Cross-service call to the Identity service for PROVIDER org creation.
// CareConnect calls this during auto-provisioning to create/resolve the
// Identity Organization that gets linked to the provider record.
namespace CareConnect.Application.Interfaces;

/// <summary>
/// Thin cross-service abstraction over the Identity service organization endpoint.
/// Returns null on any failure — all failures trigger LSCC-009 queue fallback.
/// </summary>
public interface IIdentityOrganizationService
{
    /// <summary>
    /// Creates or resolves a minimal PROVIDER Organization in the Identity service
    /// for the given CareConnect provider.
    ///
    /// Idempotency: the identity endpoint uses (TenantId + ProviderCcId) as the
    /// unique key. Repeated calls with the same inputs return the same org ID.
    ///
    /// Returns the Identity OrganizationId on success, null on any failure.
    /// Callers must treat null as "fall back to LSCC-009".
    /// </summary>
    Task<Guid?> EnsureProviderOrganizationAsync(
        Guid              tenantId,
        Guid              providerCcId,
        string            providerName,
        CancellationToken ct = default);
}
