using Identity.Application;
using Identity.Domain;
using Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Repositories;

public class TenantRepository : ITenantRepository
{
    private readonly IdentityDbContext _db;

    public TenantRepository(IdentityDbContext db) => _db = db;

    public Task<Tenant?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _db.Tenants.FirstOrDefaultAsync(t => t.Id == id, ct);

    public Task<Tenant?> GetByCodeAsync(string code, CancellationToken ct = default) =>
        _db.Tenants.FirstOrDefaultAsync(t => t.Code == code, ct);
}
