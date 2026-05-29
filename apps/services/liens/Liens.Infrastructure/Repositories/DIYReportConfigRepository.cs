using Liens.Application.Repositories;
using Liens.Domain.Entities;
using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Liens.Infrastructure.Repositories;

public class DIYReportConfigRepository : IDIYReportConfigRepository
{
    private readonly LiensDbContext _db;
    public DIYReportConfigRepository(LiensDbContext db) => _db = db;

    public Task<DIYReportConfig?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default) =>
        _db.DIYReportConfigs.FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == id && !r.IsDeleted, ct);

    public Task<List<DIYReportConfig>> GetByUserAsync(Guid tenantId, Guid userId, CancellationToken ct = default) =>
        _db.DIYReportConfigs
            .Where(r => r.TenantId == tenantId && r.UserId == userId && !r.IsDeleted)
            .OrderByDescending(r => r.UpdatedAtUtc)
            .ToListAsync(ct);

    public async Task AddAsync(DIYReportConfig config, CancellationToken ct = default)
    {
        _db.DIYReportConfigs.Add(config);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(DIYReportConfig config, CancellationToken ct = default)
    {
        _db.DIYReportConfigs.Update(config);
        await _db.SaveChangesAsync(ct);
    }
}
