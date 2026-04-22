using CareConnect.Domain;

namespace CareConnect.Application.Repositories;

// CC2-INT-B06
public interface INetworkRepository
{
    Task<List<ProviderNetwork>> GetAllByTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<ProviderNetwork?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<ProviderNetwork?> GetWithProvidersAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<bool> NameExistsAsync(Guid tenantId, string name, Guid? excludeId = null, CancellationToken ct = default);
    Task AddAsync(ProviderNetwork network, CancellationToken ct = default);
    Task AddProviderAsync(NetworkProvider entry, CancellationToken ct = default);
    Task<NetworkProvider?> GetMembershipAsync(Guid networkId, Guid providerId, CancellationToken ct = default);
    Task RemoveProviderAsync(NetworkProvider entry, CancellationToken ct = default);
    Task<List<Provider>> GetNetworkProvidersAsync(Guid tenantId, Guid networkId, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
