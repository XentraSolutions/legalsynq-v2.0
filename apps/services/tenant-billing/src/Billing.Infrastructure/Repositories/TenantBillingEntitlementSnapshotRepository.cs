using Microsoft.EntityFrameworkCore;
using Billing.Domain.Entities;
using Billing.Domain.Repositories;
using Billing.Infrastructure.Data;

namespace Billing.Infrastructure.Repositories;

public sealed class TenantBillingEntitlementSnapshotRepository
    : ITenantBillingEntitlementSnapshotRepository
{
    private readonly BillingDbContext _db;

    public TenantBillingEntitlementSnapshotRepository(BillingDbContext db) => _db = db;

    public async Task<TenantBillingEntitlementSnapshot> AddAsync(
        TenantBillingEntitlementSnapshot snapshot, CancellationToken ct = default)
    {
        await _db.TenantBillingEntitlementSnapshots.AddAsync(snapshot, ct);
        await _db.SaveChangesAsync(ct);
        return snapshot;
    }

    public async Task<TenantBillingEntitlementSnapshot> UpdateAsync(
        TenantBillingEntitlementSnapshot snapshot, CancellationToken ct = default)
    {
        _db.TenantBillingEntitlementSnapshots.Update(snapshot);
        await _db.SaveChangesAsync(ct);
        return snapshot;
    }

    public Task<TenantBillingEntitlementSnapshot?> GetByProfileIdAsync(
        Guid tenantId, Guid profileId, CancellationToken ct = default)
        => _db.TenantBillingEntitlementSnapshots
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId
                                      && s.TenantBillingProfileId == profileId, ct);
}
