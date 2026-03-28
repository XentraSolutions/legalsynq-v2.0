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

    public async Task UpdateAsync(Referral referral, ReferralStatusHistory? history = null, CancellationToken ct = default)
    {
        _db.Referrals.Update(referral);

        if (history is not null)
            await _db.ReferralStatusHistories.AddAsync(history, ct);

        await _db.SaveChangesAsync(ct);
    }

    public async Task<List<ReferralStatusHistory>> GetHistoryByReferralAsync(Guid tenantId, Guid referralId, CancellationToken ct = default)
    {
        return await _db.ReferralStatusHistories
            .Where(h => h.TenantId == tenantId && h.ReferralId == referralId)
            .OrderByDescending(h => h.ChangedAtUtc)
            .ToListAsync(ct);
    }
}
