using CareConnect.Application.Repositories;
using CareConnect.Domain;
using CareConnect.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CareConnect.Infrastructure.Repositories;

public class ReferralAttributionRepository : IReferralAttributionRepository
{
    private readonly CareConnectDbContext _db;

    public ReferralAttributionRepository(CareConnectDbContext db)
    {
        _db = db;
    }

    public async Task<List<ReferralAttribution>> ListByTenantAsync(Guid tenantId, bool? activeOnly, CancellationToken ct = default)
    {
        var q = _db.ReferralAttributions.AsNoTracking().Where(a => a.TenantId == tenantId);
        if (activeOnly == true)
            q = q.Where(a => a.IsActive);

        return await q
            .OrderBy(a => a.DisplayOrder ?? int.MaxValue)
            .ThenBy(a => a.FirstName)
            .ThenBy(a => a.LastName)
            .ToListAsync(ct);
    }

    public async Task<ReferralAttribution?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        return await _db.ReferralAttributions
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == id, ct);
    }

    public async Task<ReferralAttribution?> GetByCodeAsync(Guid tenantId, string normalizedCode, CancellationToken ct = default)
    {
        return await _db.ReferralAttributions
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Code == normalizedCode, ct);
    }

    public async Task AddAsync(ReferralAttribution attribution, CancellationToken ct = default)
    {
        await _db.ReferralAttributions.AddAsync(attribution, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(ReferralAttribution attribution, CancellationToken ct = default)
    {
        _db.ReferralAttributions.Update(attribution);
        await _db.SaveChangesAsync(ct);
    }

    public Task<bool> IsUsedByAnyReferralAsync(Guid tenantId, Guid attributionId, CancellationToken ct = default)
    {
        return _db.Referrals.AsNoTracking()
            .AnyAsync(r => r.TenantId == tenantId && r.ReferralAttributionId == attributionId, ct);
    }
}
