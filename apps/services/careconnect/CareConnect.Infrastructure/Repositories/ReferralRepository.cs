using CareConnect.Application.Repositories;
using CareConnect.Domain;
using CareConnect.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CareConnect.Infrastructure.Repositories;

public class ReferralRepository : IReferralRepository
{
    private readonly CareConnectDbContext _db;

    public ReferralRepository(CareConnectDbContext db)
    {
        _db = db;
    }

    public async Task<List<Referral>> GetAllByTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        return await _db.Referrals
            .Where(r => r.TenantId == tenantId)
            .Include(r => r.Provider)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<Referral?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        return await _db.Referrals
            .Where(r => r.TenantId == tenantId && r.Id == id)
            .Include(r => r.Provider)
            .FirstOrDefaultAsync(ct);
    }

    public async Task AddAsync(Referral referral, CancellationToken ct = default)
    {
        await _db.Referrals.AddAsync(referral, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Referral referral, CancellationToken ct = default)
    {
        _db.Referrals.Update(referral);
        await _db.SaveChangesAsync(ct);
    }
}
