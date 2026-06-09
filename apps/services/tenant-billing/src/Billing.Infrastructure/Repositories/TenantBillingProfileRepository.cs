using Microsoft.EntityFrameworkCore;
using Billing.Domain.Entities;
using Billing.Domain.Repositories;
using Billing.Infrastructure.Data;

namespace Billing.Infrastructure.Repositories;

public sealed class TenantBillingProfileRepository : ITenantBillingProfileRepository
{
    private readonly BillingDbContext _db;

    public TenantBillingProfileRepository(BillingDbContext db) => _db = db;

    public async Task<TenantBillingProfile> AddAsync(TenantBillingProfile profile, CancellationToken ct = default)
    {
        await _db.TenantBillingProfiles.AddAsync(profile, ct);
        await _db.SaveChangesAsync(ct);
        return profile;
    }

    public async Task<TenantBillingProfile> UpdateAsync(TenantBillingProfile profile, CancellationToken ct = default)
    {
        _db.TenantBillingProfiles.Update(profile);
        await _db.SaveChangesAsync(ct);
        return profile;
    }

    public Task<TenantBillingProfile?> GetByIdAsync(Guid tenantId, Guid profileId, CancellationToken ct = default)
        => _db.TenantBillingProfiles
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Id == profileId, ct);

    public Task<TenantBillingProfile?> GetActiveByTenantAsync(Guid tenantId, CancellationToken ct = default)
        => _db.TenantBillingProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p =>
                p.TenantId == tenantId
                && p.Status == TenantBillingProfileStatus.Active, ct);

    public Task<TenantBillingProfile?> GetByBillingAccountAsync(
        Guid tenantId,
        Guid billingAccountId,
        CancellationToken ct = default)
        => _db.TenantBillingProfiles
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId
                        && p.BillingAccountId == billingAccountId
                        && p.Status != TenantBillingProfileStatus.Closed)
            .OrderByDescending(p => p.UpdatedAtUtc)
            .FirstOrDefaultAsync(ct);

    public Task<TenantBillingProfile?> GetAnyByBillingAccountAsync(
        Guid tenantId,
        Guid billingAccountId,
        CancellationToken ct = default)
        => _db.TenantBillingProfiles
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId
                        && p.BillingAccountId == billingAccountId)
            .OrderByDescending(p => p.UpdatedAtUtc)
            .FirstOrDefaultAsync(ct);

    public async Task<bool> HasOpenProfileForTenantAsync(Guid tenantId, CancellationToken ct = default)
        => await _db.TenantBillingProfiles
            .AsNoTracking()
            .AnyAsync(p => p.TenantId == tenantId
                           && p.Status != TenantBillingProfileStatus.Closed, ct);

    public async Task<bool> IsBillingAccountClaimedAsync(Guid billingAccountId, CancellationToken ct = default)
        => await _db.TenantBillingProfiles
            .AsNoTracking()
            .AnyAsync(p => p.BillingAccountId == billingAccountId
                           && p.Status != TenantBillingProfileStatus.Closed, ct);

    public async Task<IReadOnlyList<TenantBillingProfile>> ListAsync(
        Guid tenantId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        return await _db.TenantBillingProfiles
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId)
            .OrderByDescending(p => p.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    public Task<int> CountAsync(Guid tenantId, CancellationToken ct = default)
        => _db.TenantBillingProfiles
            .AsNoTracking()
            .CountAsync(p => p.TenantId == tenantId, ct);
}
