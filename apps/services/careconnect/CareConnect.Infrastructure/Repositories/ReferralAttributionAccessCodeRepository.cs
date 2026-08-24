using CareConnect.Application.Repositories;
using CareConnect.Domain;
using CareConnect.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CareConnect.Infrastructure.Repositories;

public class ReferralAttributionAccessCodeRepository : IReferralAttributionAccessCodeRepository
{
    private readonly CareConnectDbContext _db;

    public ReferralAttributionAccessCodeRepository(CareConnectDbContext db)
    {
        _db = db;
    }

    public async Task<List<ReferralAttributionAccessCode>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        return await _db.ReferralAttributionAccessCodes
            .AsNoTracking()
            .Include(a => a.ReferralAttribution)
            .Where(a => a.TenantId == tenantId)
            .OrderByDescending(a => a.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<ReferralAttributionAccessCode?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        return await _db.ReferralAttributionAccessCodes
            .AsNoTracking()
            .Include(a => a.ReferralAttribution)
            .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == id, ct);
    }

    public async Task<ReferralAttributionAccessCode?> GetByHashAsync(Guid tenantId, string codeHash, CancellationToken ct = default)
    {
        return await _db.ReferralAttributionAccessCodes
            .AsNoTracking()
            .Include(a => a.ReferralAttribution)
            .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.CodeHash == codeHash, ct);
    }

    public Task<int> CountActiveAsync(Guid tenantId, Guid referralAttributionId, CancellationToken ct = default)
    {
        return _db.ReferralAttributionAccessCodes.AsNoTracking()
            .CountAsync(a => a.TenantId == tenantId && a.ReferralAttributionId == referralAttributionId && a.IsActive, ct);
    }

    public Task<ReferralAttributionAccessCode?> GetActiveByAttributionAsync(Guid tenantId, Guid referralAttributionId, CancellationToken ct = default)
    {
        return _db.ReferralAttributionAccessCodes
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.ReferralAttributionId == referralAttributionId && a.IsActive, ct);
    }

    public async Task AddAsync(ReferralAttributionAccessCode code, CancellationToken ct = default)
    {
        await _db.ReferralAttributionAccessCodes.AddAsync(code, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(ReferralAttributionAccessCode code, CancellationToken ct = default)
    {
        _db.ReferralAttributionAccessCodes.Update(code);
        await _db.SaveChangesAsync(ct);
    }
}
