using Microsoft.EntityFrameworkCore;
using Tenant.Application.Interfaces;
using Tenant.Domain;
using Tenant.Infrastructure.Data;

namespace Tenant.Infrastructure.Repositories;

public class EntitlementRepository : IEntitlementRepository
{
    private readonly TenantDbContext _db;

    public EntitlementRepository(TenantDbContext db) => _db = db;

    public Task<TenantProductEntitlement?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.ProductEntitlements.FirstOrDefaultAsync(e => e.Id == id, ct);

    public Task<List<TenantProductEntitlement>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default) =>
        _db.ProductEntitlements
            .Where(e => e.TenantId == tenantId)
            .OrderByDescending(e => e.IsDefault)
            .ThenByDescending(e => e.IsEnabled)
            .ThenBy(e => e.ProductKey)
            .ToListAsync(ct);

    public Task<TenantProductEntitlement?> GetByTenantAndProductKeyAsync(
        Guid tenantId, string productKey, CancellationToken ct = default) =>
        _db.ProductEntitlements
            .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.ProductKey == productKey, ct);

    public Task<TenantProductEntitlement?> GetDefaultForTenantAsync(Guid tenantId, CancellationToken ct = default) =>
        _db.ProductEntitlements
            .FirstOrDefaultAsync(e => e.TenantId == tenantId && e.IsDefault, ct);

    public Task<List<TenantProductEntitlement>> GetDefaultsForTenantAsync(Guid tenantId, CancellationToken ct = default) =>
        _db.ProductEntitlements
            .Where(e => e.TenantId == tenantId && e.IsDefault)
            .ToListAsync(ct);

    public async Task AddAsync(TenantProductEntitlement entitlement, CancellationToken ct = default)
    {
        await _db.ProductEntitlements.AddAsync(entitlement, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(TenantProductEntitlement entitlement, CancellationToken ct = default) =>
        await _db.SaveChangesAsync(ct);

    public async Task UpdateRangeAsync(IEnumerable<TenantProductEntitlement> entitlements, CancellationToken ct = default) =>
        await _db.SaveChangesAsync(ct);

    public async Task DeleteAsync(TenantProductEntitlement entitlement, CancellationToken ct = default)
    {
        _db.ProductEntitlements.Remove(entitlement);
        await _db.SaveChangesAsync(ct);
    }
}
