using CareConnect.Application.Repositories;
using CareConnect.Domain;
using CareConnect.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CareConnect.Infrastructure.Repositories;

public class ProviderRepository : IProviderRepository
{
    private readonly CareConnectDbContext _db;

    public ProviderRepository(CareConnectDbContext db)
    {
        _db = db;
    }

    public async Task<List<Provider>> GetAllByTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        return await _db.Providers
            .Where(p => p.TenantId == tenantId)
            .Include(p => p.ProviderCategories)
                .ThenInclude(pc => pc.Category)
            .OrderBy(p => p.Name)
            .ToListAsync(ct);
    }

    public async Task<Provider?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        return await _db.Providers
            .Where(p => p.TenantId == tenantId && p.Id == id)
            .Include(p => p.ProviderCategories)
                .ThenInclude(pc => pc.Category)
            .FirstOrDefaultAsync(ct);
    }

    public async Task AddAsync(Provider provider, CancellationToken ct = default)
    {
        await _db.Providers.AddAsync(provider, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Provider provider, CancellationToken ct = default)
    {
        _db.Providers.Update(provider);
        await _db.SaveChangesAsync(ct);
    }

    public async Task SyncCategoriesAsync(Guid providerId, List<Guid> categoryIds, CancellationToken ct = default)
    {
        var existing = await _db.ProviderCategories
            .Where(pc => pc.ProviderId == providerId)
            .ToListAsync(ct);

        _db.ProviderCategories.RemoveRange(existing);

        if (categoryIds.Count > 0)
        {
            var newLinks = categoryIds.Select(cid => new ProviderCategory
            {
                ProviderId = providerId,
                CategoryId = cid
            });
            await _db.ProviderCategories.AddRangeAsync(newLinks, ct);
        }

        await _db.SaveChangesAsync(ct);
    }
}
