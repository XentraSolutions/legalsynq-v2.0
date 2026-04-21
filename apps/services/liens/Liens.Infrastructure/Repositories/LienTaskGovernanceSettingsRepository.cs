using Liens.Application.Repositories;
using Liens.Domain.Entities;
using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Liens.Infrastructure.Repositories;

public sealed class LienTaskGovernanceSettingsRepository : ILienTaskGovernanceSettingsRepository
{
    private readonly LiensDbContext _db;

    public LienTaskGovernanceSettingsRepository(LiensDbContext db) => _db = db;

    public Task<LienTaskGovernanceSettings?> GetByTenantProductAsync(
        Guid tenantId, string productCode, CancellationToken ct = default) =>
        _db.LienTaskGovernanceSettings
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.ProductCode == productCode, ct);

    public async Task<IReadOnlyList<LienTaskGovernanceSettings>> GetAllAsync(
        CancellationToken ct = default) =>
        await _db.LienTaskGovernanceSettings.ToListAsync(ct);

    public async Task AddAsync(LienTaskGovernanceSettings entity, CancellationToken ct = default)
    {
        _db.LienTaskGovernanceSettings.Add(entity);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(LienTaskGovernanceSettings entity, CancellationToken ct = default)
    {
        _db.LienTaskGovernanceSettings.Update(entity);
        await _db.SaveChangesAsync(ct);
    }
}
