using CareConnect.Application.DTOs;
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

    public async Task<(List<Provider> Items, int TotalCount)> SearchAsync(Guid tenantId, GetProvidersQuery query, CancellationToken ct = default)
    {
        var baseQuery = _db.Providers
            .Where(p => p.TenantId == tenantId)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Name))
            baseQuery = baseQuery.Where(p => p.Name.Contains(query.Name));

        if (!string.IsNullOrWhiteSpace(query.CategoryCode))
            baseQuery = baseQuery.Where(p => p.ProviderCategories
                .Any(pc => pc.Category != null && pc.Category.Code == query.CategoryCode));

        if (!string.IsNullOrWhiteSpace(query.City))
            baseQuery = baseQuery.Where(p => p.City == query.City);

        if (!string.IsNullOrWhiteSpace(query.State))
            baseQuery = baseQuery.Where(p => p.State == query.State);

        if (query.AcceptingReferrals.HasValue)
            baseQuery = baseQuery.Where(p => p.AcceptingReferrals == query.AcceptingReferrals.Value);

        if (query.IsActive.HasValue)
            baseQuery = baseQuery.Where(p => p.IsActive == query.IsActive.Value);

        var totalCount = await baseQuery.CountAsync(ct);

        var ids = await baseQuery
            .OrderBy(p => p.Name)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(p => p.Id)
            .ToListAsync(ct);

        var items = await _db.Providers
            .Where(p => ids.Contains(p.Id))
            .Include(p => p.ProviderCategories)
                .ThenInclude(pc => pc.Category)
            .OrderBy(p => p.Name)
            .ToListAsync(ct);

        return (items, totalCount);
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
