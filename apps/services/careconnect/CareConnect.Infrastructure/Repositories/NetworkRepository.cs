using CareConnect.Application.Repositories;
using CareConnect.Domain;
using CareConnect.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CareConnect.Infrastructure.Repositories;

// CC2-INT-B06
public class NetworkRepository : INetworkRepository
{
    private readonly CareConnectDbContext _db;

    public NetworkRepository(CareConnectDbContext db)
    {
        _db = db;
    }

    public async Task<List<ProviderNetwork>> GetAllByTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        return await _db.ProviderNetworks
            .Where(n => n.TenantId == tenantId && !n.IsDeleted)
            .OrderBy(n => n.Name)
            .ToListAsync(ct);
    }

    public async Task<ProviderNetwork?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        return await _db.ProviderNetworks
            .FirstOrDefaultAsync(n => n.TenantId == tenantId && n.Id == id && !n.IsDeleted, ct);
    }

    public async Task<ProviderNetwork?> GetWithProvidersAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        return await _db.ProviderNetworks
            .Include(n => n.NetworkProviders)
                .ThenInclude(np => np.Provider)
            .FirstOrDefaultAsync(n => n.TenantId == tenantId && n.Id == id && !n.IsDeleted, ct);
    }

    public async Task<bool> NameExistsAsync(Guid tenantId, string name, Guid? excludeId = null, CancellationToken ct = default)
    {
        return await _db.ProviderNetworks
            .AnyAsync(n => n.TenantId == tenantId && !n.IsDeleted &&
                           n.Name == name && (excludeId == null || n.Id != excludeId.Value), ct);
    }

    public async Task AddAsync(ProviderNetwork network, CancellationToken ct = default)
    {
        await _db.ProviderNetworks.AddAsync(network, ct);
    }

    public async Task AddProviderAsync(NetworkProvider entry, CancellationToken ct = default)
    {
        await _db.NetworkProviders.AddAsync(entry, ct);
    }

    public async Task<NetworkProvider?> GetMembershipAsync(Guid networkId, Guid providerId, CancellationToken ct = default)
    {
        return await _db.NetworkProviders
            .FirstOrDefaultAsync(np => np.ProviderNetworkId == networkId && np.ProviderId == providerId, ct);
    }

    public Task RemoveProviderAsync(NetworkProvider entry, CancellationToken ct = default)
    {
        _db.NetworkProviders.Remove(entry);
        return Task.CompletedTask;
    }

    public async Task<List<Provider>> GetNetworkProvidersAsync(Guid tenantId, Guid networkId, CancellationToken ct = default)
    {
        return await _db.NetworkProviders
            .Where(np => np.ProviderNetworkId == networkId && np.TenantId == tenantId)
            .Include(np => np.Provider)
            .Select(np => np.Provider)
            .OrderBy(p => p.LastName)
                .ThenBy(p => p.FirstName)
            .ToListAsync(ct);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _db.SaveChangesAsync(ct);
    }
}
