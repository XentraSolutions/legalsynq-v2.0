using Liens.Application.Repositories;
using Liens.Domain.Entities;
using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Liens.Infrastructure.Repositories;

public sealed class LienStatusHistoryRepository : ILienStatusHistoryRepository
{
    private readonly LiensDbContext _db;

    public LienStatusHistoryRepository(LiensDbContext db) => _db = db;

    public Task<List<LienStatusHistory>> GetByCaseIdAsync(
        Guid tenantId,
        Guid caseId,
        CancellationToken ct = default) =>
        _db.LienStatusHistories
            .Where(item => item.TenantId == tenantId && item.CaseId == caseId)
            .OrderByDescending(item => item.ChangedAtUtc)
            .ToListAsync(ct);

    public async Task AddAsync(LienStatusHistory entity, CancellationToken ct = default)
    {
        await _db.LienStatusHistories.AddAsync(entity, ct);
        await _db.SaveChangesAsync(ct);
    }
}
