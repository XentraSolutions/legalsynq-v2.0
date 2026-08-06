using Tenant.Application.DTOs;

namespace Tenant.Application.Interfaces;

public interface ICapabilityService
{
    Task<List<CapabilityResponse>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<CapabilityResponse>       GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<CapabilityResponse>       CreateAsync(Guid tenantId, CreateCapabilityRequest request, CancellationToken ct = default);
    Task<CapabilityResponse>       UpdateAsync(Guid tenantId, Guid id, UpdateCapabilityRequest request, CancellationToken ct = default);
    Task                           DeleteAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    /// <summary>
    /// Lightweight, tenant-global capability check for public/service-to-service reads
    /// (e.g. CareConnect checking "referral_representative_portal_enabled" before
    /// authorizing representative portal access). Returns false — never throws — when
    /// the tenant is unknown or the capability is not configured, so callers can treat
    /// "disabled" as the safe default in every ambiguous case.
    /// </summary>
    Task<bool> IsEnabledAsync(Guid tenantId, string capabilityKey, CancellationToken ct = default);
}
