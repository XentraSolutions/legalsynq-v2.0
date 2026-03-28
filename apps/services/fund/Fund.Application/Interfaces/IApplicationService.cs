using Fund.Application.DTOs;

namespace Fund.Application.Interfaces;

public interface IApplicationService
{
    Task<ApplicationResponse> CreateAsync(
        Guid tenantId,
        Guid userId,
        CreateApplicationRequest request,
        CancellationToken ct = default);

    Task<List<ApplicationResponse>> GetAllAsync(Guid tenantId, CancellationToken ct = default);
    Task<ApplicationResponse?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
}
