using CareConnect.Application.DTOs;

namespace CareConnect.Application.Interfaces;

public interface ISpecialtyService
{
    Task<List<SpecialtyResponse>> GetAllAsync(bool includeInactive = false, CancellationToken ct = default);
    Task<SpecialtyResponse> CreateAsync(CreateSpecialtyRequest request, CancellationToken ct = default);
    Task<SpecialtyResponse> UpdateAsync(Guid id, UpdateSpecialtyRequest request, CancellationToken ct = default);
    Task DeactivateAsync(Guid id, CancellationToken ct = default);
}
