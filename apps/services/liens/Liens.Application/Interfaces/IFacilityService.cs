using Liens.Application.DTOs;

namespace Liens.Application.Interfaces;

public interface IFacilityService
{
    Task<PaginatedResult<FacilityResponse>> SearchAsync(
        Guid tenantId, string? search, bool? isActive,
        int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// Searches legacy facility records using the IDs stored on liens.
    /// </summary>
    Task<PaginatedResult<FacilityResponse>> SearchLienFacilitiesAsync(
        Guid tenantId, string? search, bool? isActive,
        int page, int pageSize, CancellationToken ct = default);

    Task<List<FacilityResponse>> GetAllAsync(Guid tenantId, bool? isActive = true, CancellationToken ct = default);

    Task<FacilityResponse?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    Task<FacilityResponse> CreateAsync(
        Guid tenantId, Guid orgId, Guid actingUserId,
        CreateFacilityRequest request, CancellationToken ct = default);

    Task<FacilityResponse> UpdateAsync(
        Guid tenantId, Guid id, Guid actingUserId,
        UpdateFacilityRequest request, CancellationToken ct = default);

    Task<FacilityResponse> DeactivateAsync(
        Guid tenantId, Guid id, Guid actingUserId, CancellationToken ct = default);

    // Contact-person operations
    Task<List<FacilityContactPersonResponse>> GetContactPersonsAsync(
        Guid tenantId, Guid facilityId, CancellationToken ct = default);

    Task<FacilityContactPersonResponse> CreateContactPersonAsync(
        Guid tenantId, Guid facilityId, Guid actingUserId,
        CreateFacilityContactPersonRequest request, CancellationToken ct = default);

    Task<FacilityContactPersonResponse> UpdateContactPersonAsync(
        Guid tenantId, Guid facilityId, Guid personId, Guid actingUserId,
        UpdateFacilityContactPersonRequest request, CancellationToken ct = default);

    Task DeleteContactPersonAsync(
        Guid tenantId, Guid facilityId, Guid personId, Guid actingUserId, CancellationToken ct = default);
}
