using Liens.Application.DTOs;

namespace Liens.Application.Interfaces;

public interface ILienService
{
    Task<PaginatedResult<LienResponse>> SearchAsync(
        Guid tenantId, string? search, string? status, string? lienType,
        Guid? caseId, Guid? facilityId, int page, int pageSize,
        CancellationToken ct = default);

    Task<LienResponse?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    Task<LienResponse?> GetByLienNumberAsync(Guid tenantId, string lienNumber, CancellationToken ct = default);

    Task<LienResponse> CreateAsync(
        Guid tenantId, Guid orgId, Guid actingUserId,
        CreateLienRequest request, CancellationToken ct = default);

    Task<LienResponse> UpdateAsync(
        Guid tenantId, Guid id, Guid actingUserId,
        UpdateLienRequest request, CancellationToken ct = default);

    Task<LienResponse> SetLegacyMedicalStatusAsync(
        Guid tenantId, Guid id, Guid actingUserId,
        string status, CancellationToken ct = default);

    Task DeleteAsync(Guid tenantId, Guid id, Guid actingUserId, CancellationToken ct = default);
}
