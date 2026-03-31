using CareConnect.Application.DTOs;

namespace CareConnect.Application.Interfaces;

public interface IProviderService
{
    Task<PagedResponse<ProviderResponse>> SearchAsync(Guid tenantId, GetProvidersQuery query, CancellationToken ct = default);
    Task<List<ProviderMarkerResponse>> GetMarkersAsync(Guid tenantId, GetProvidersQuery query, CancellationToken ct = default);
    Task<ProviderResponse> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<ProviderResponse> CreateAsync(Guid tenantId, Guid? userId, CreateProviderRequest request, CancellationToken ct = default);
    Task<ProviderResponse> UpdateAsync(Guid tenantId, Guid id, Guid? userId, UpdateProviderRequest request, CancellationToken ct = default);

    /// <summary>
    /// Returns open appointment slots for the provider in the given date/time range,
    /// optionally filtered by service offering and facility.
    /// </summary>
    Task<ProviderAvailabilityResponse> GetAvailabilityAsync(
        Guid      tenantId,
        Guid      providerId,
        DateTime  from,
        DateTime  to,
        Guid?     serviceOfferingId = null,
        Guid?     facilityId        = null,
        CancellationToken ct        = default);

    /// <summary>
    /// LSCC-002: Admin-safe endpoint that explicitly sets the Identity OrganizationId
    /// for a provider that was seeded or created before org linkage was enforced.
    /// Idempotent — calling again with the same organizationId is a no-op.
    /// </summary>
    // LSCC-002: Admin provider org-link backfill entry point
    Task<ProviderResponse> LinkOrganizationAsync(
        Guid tenantId,
        Guid providerId,
        Guid organizationId,
        CancellationToken ct = default);
}
