using Microsoft.EntityFrameworkCore;
using Tenant.Application.Interfaces;
using Tenant.Domain;
using Tenant.Infrastructure.Data;

namespace Tenant.Infrastructure.Repositories;

public class CapabilityRepository : ICapabilityRepository
{
    private readonly TenantDbContext _db;

    public CapabilityRepository(TenantDbContext db) => _db = db;

    public Task<TenantCapability?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Capabilities.FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<List<TenantCapability>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default) =>
        _db.Capabilities
            .Where(c => c.TenantId == tenantId)
            .OrderBy(c => c.CapabilityKey)
            .ToListAsync(ct);

    public Task<TenantCapability?> GetByKeyAsync(
        Guid tenantId, string capabilityKey, Guid? productEntitlementId, CancellationToken ct = default) =>
        _db.Capabilities.FirstOrDefaultAsync(c =>
            c.TenantId             == tenantId &&
            c.CapabilityKey        == capabilityKey &&
            c.ProductEntitlementId == productEntitlementId,
            ct);

    public async Task AddAsync(TenantCapability capability, CancellationToken ct = default)
    {
        await _db.Capabilities.AddAsync(capability, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(TenantCapability capability, CancellationToken ct = default) =>
        await _db.SaveChangesAsync(ct);

    public async Task DeleteAsync(TenantCapability capability, CancellationToken ct = default)
    {
        _db.Capabilities.Remove(capability);
        await _db.SaveChangesAsync(ct);
    }
}
