// BLK-PERF-01: All read-only queries use AsNoTracking() to avoid EF Core change-tracking overhead.
// GetAllWithProviderCountAsync added to eliminate N+1 in the public network list endpoint.
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
            .AsNoTracking()
            .Where(n => n.TenantId == tenantId && !n.IsDeleted)
            .OrderBy(n => n.Name)
            .ToListAsync(ct);
    }

    // BLK-PERF-01: Replaces the N+1 pattern in PublicNetworkEndpoints GET / where each
    // network in the list triggered a separate GetWithProvidersAsync round-trip.
    // A single query projects network fields + sub-query COUNT() of NetworkProviders.
    public async Task<List<(Guid Id, string Name, string? Description, int ProviderCount)>> GetAllWithProviderCountAsync(
        Guid tenantId, CancellationToken ct = default)
    {
        return await _db.ProviderNetworks
            .AsNoTracking()
            .Where(n => n.TenantId == tenantId && !n.IsDeleted)
            .OrderBy(n => n.Name)
            .Select(n => ValueTuple.Create(
                n.Id,
                n.Name,
                (string?)n.Description,
                n.NetworkProviders.Count()))
            .ToListAsync(ct);
    }

    public async Task<ProviderNetwork?> GetByIdGlobalAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.ProviderNetworks
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == id && !n.IsDeleted, ct);
    }

    public async Task<ProviderNetwork?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        return await _db.ProviderNetworks
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.TenantId == tenantId && n.Id == id && !n.IsDeleted, ct);
    }

    public async Task<ProviderNetwork?> GetWithProvidersAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        return await _db.ProviderNetworks
            .AsNoTracking()
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
            .AsNoTracking()
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
            .AsNoTracking()
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
        var q = _db.Providers.AsNoTracking().AsQueryable();

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
        return await _db.Providers.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task<Provider?> GetProviderByNpiAsync(string npi, CancellationToken ct = default)
    {
        var trimmed = npi.Trim();
        return await _db.Providers.AsNoTracking().FirstOrDefaultAsync(p => p.Npi == trimmed, ct);
    }

    public async Task<Provider?> GetProviderByTenantEmailAsync(Guid tenantId, string email, CancellationToken ct = default)
    {
        var trimmed = email.Trim().ToLowerInvariant();
        return await _db.Providers.AsNoTracking()
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Email == trimmed, ct);
    }

    public async Task AddProviderToRegistryAsync(Provider provider, CancellationToken ct = default)
    {
        await _db.Providers.AddAsync(provider, ct);
    }

    public async Task<Dictionary<string, Provider>> GetProvidersByNpisAsync(IEnumerable<string> npis, CancellationToken ct = default)
    {
        var normalized = npis
            .Where(npi => !string.IsNullOrWhiteSpace(npi))
            .Select(npi => npi.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (normalized.Count == 0) return new Dictionary<string, Provider>(StringComparer.Ordinal);

        var providers = await _db.Providers.AsNoTracking()
            .Where(p => p.Npi != null && normalized.Contains(p.Npi))
            .ToListAsync(ct);

        return providers
            .GroupBy(p => p.Npi!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.OrderBy(p => p.CreatedAtUtc).First(), StringComparer.Ordinal);
    }

    public async Task<Dictionary<string, Provider>> GetProvidersByTenantEmailsAsync(
        Guid tenantId, IEnumerable<string> emails, CancellationToken ct = default)
    {
        var normalized = emails
            .Where(email => !string.IsNullOrWhiteSpace(email))
            .Select(email => email.Trim().ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (normalized.Count == 0) return new Dictionary<string, Provider>(StringComparer.Ordinal);

        var providers = await _db.Providers.AsNoTracking()
            .Where(p => p.TenantId == tenantId && normalized.Contains(p.Email))
            .ToListAsync(ct);

        return providers
            .GroupBy(p => p.Email, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.OrderBy(p => p.CreatedAtUtc).First(), StringComparer.Ordinal);
    }

    public async Task<HashSet<Guid>> GetNetworkProviderIdsAsync(Guid tenantId, Guid networkId, CancellationToken ct = default)
    {
        var providerIds = await _db.NetworkProviders
            .AsNoTracking()
            .Where(np => np.TenantId == tenantId && np.ProviderNetworkId == networkId)
            .Select(np => np.ProviderId)
            .ToListAsync(ct);

        return providerIds.ToHashSet();
    }

    public async Task<bool> IsProviderInTenantNetworkAsync(Guid tenantId, Guid providerId, CancellationToken ct = default)
    {
        return await _db.NetworkProviders
            .AsNoTracking()
            .AnyAsync(np =>
                np.ProviderId == providerId &&
                np.TenantId   == tenantId   &&
                _db.ProviderNetworks.Any(n => n.Id == np.ProviderNetworkId && n.TenantId == tenantId && !n.IsDeleted),
                ct);
    }

    public void ClearTracking()
    {
        _db.ChangeTracker.Clear();
    }

    public async Task SyncProviderCategoriesAsync(Guid providerId, List<Guid> categoryIds, CancellationToken ct = default)
    {
        var existing = await _db.ProviderCategories
            .Where(pc => pc.ProviderId == providerId)
            .ToListAsync(ct);

        _db.ProviderCategories.RemoveRange(existing);

        foreach (var catId in categoryIds)
        {
            _db.ProviderCategories.Add(new ProviderCategory
            {
                ProviderId = providerId,
                CategoryId = catId,
            });
        }
    }
}
