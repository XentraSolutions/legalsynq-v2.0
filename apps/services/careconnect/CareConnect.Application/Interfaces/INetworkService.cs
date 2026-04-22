using CareConnect.Application.DTOs;

namespace CareConnect.Application.Interfaces;

// CC2-INT-B06
public interface INetworkService
{
    Task<List<NetworkSummaryResponse>> GetAllAsync(Guid tenantId, CancellationToken ct = default);
    Task<NetworkDetailResponse> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<NetworkSummaryResponse> CreateAsync(Guid tenantId, Guid? userId, CreateNetworkRequest request, CancellationToken ct = default);
    Task<NetworkSummaryResponse> UpdateAsync(Guid tenantId, Guid id, Guid? userId, UpdateNetworkRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task AddProviderAsync(Guid tenantId, Guid networkId, Guid providerId, Guid? userId, CancellationToken ct = default);
    Task RemoveProviderAsync(Guid tenantId, Guid networkId, Guid providerId, CancellationToken ct = default);
    Task<List<NetworkProviderMarker>> GetMarkersAsync(Guid tenantId, Guid networkId, CancellationToken ct = default);
}
