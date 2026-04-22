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
            .OrderBy(p => p.Name)
            .ToListAsync(ct);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _db.SaveChangesAsync(ct);
    }

    // ── CC2-INT-B06-01: Shared provider registry global lookups ───────────────

    public async Task<List<Provider>> SearchProvidersGlobalAsync(
        string? name, string? phone, string? npi, string? city,
        int limit = 20, CancellationToken ct = default)
    {
        var q = _db.Providers.AsQueryable();

        // NPI exact match — highest priority, most specific
        if (!string.IsNullOrWhiteSpace(npi))
        {
            var npiTrimmed = npi.Trim();
            q = q.Where(p => p.Npi == npiTrimmed);
        }
        else
        {
            // Name contains (case-insensitive on MySQL collation)
            if (!string.IsNullOrWhiteSpace(name))
            {
                var nameTrimmed = name.Trim();
                q = q.Where(p => p.Name.Contains(nameTrimmed) ||
                                 (p.OrganizationName != null && p.OrganizationName.Contains(nameTrimmed)));
            }

            // Phone: strip non-digits client-side, match normalized
            if (!string.IsNullOrWhiteSpace(phone))
            {
                var phoneTrimmed = phone.Trim();
                q = q.Where(p => p.Phone.Contains(phoneTrimmed));
            }

            // City
            if (!string.IsNullOrWhiteSpace(city))
            {
                var cityTrimmed = city.Trim();
                q = q.Where(p => p.City.Contains(cityTrimmed));
            }
        }

        return await q
            .OrderBy(p => p.Name)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<Provider?> GetProviderByIdGlobalAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.Providers.FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task<Provider?> GetProviderByNpiAsync(string npi, CancellationToken ct = default)
    {
        var trimmed = npi.Trim();
        return await _db.Providers.FirstOrDefaultAsync(p => p.Npi == trimmed, ct);
    }

    public async Task AddProviderToRegistryAsync(Provider provider, CancellationToken ct = default)
    {
        await _db.Providers.AddAsync(provider, ct);
    }
}
