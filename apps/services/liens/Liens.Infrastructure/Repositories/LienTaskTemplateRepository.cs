using Liens.Application.Repositories;
using Liens.Domain.Entities;
using Liens.Domain.Enums;
using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Liens.Infrastructure.Repositories;

public sealed class LienTaskTemplateRepository : ILienTaskTemplateRepository
{
    private readonly LiensDbContext _db;

    public LienTaskTemplateRepository(LiensDbContext db) => _db = db;

    public async Task<List<LienTaskTemplate>> GetByTenantAsync(
        Guid tenantId, CancellationToken ct = default)
    {
        return await _db.LienTaskTemplates
            .Where(t => t.TenantId == tenantId)
            .OrderByDescending(t => t.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task<List<LienTaskTemplate>> GetActiveByTenantAsync(
        Guid tenantId,
        string? contextType,
        Guid?   workflowStageId,
        CancellationToken ct = default)
    {
        var query = _db.LienTaskTemplates
            .Where(t => t.TenantId == tenantId && t.IsActive);

        if (!string.IsNullOrWhiteSpace(contextType))
        {
            query = query.Where(t =>
                t.ContextType == TaskTemplateContextType.General ||
                t.ContextType == contextType ||
                (t.ContextType == TaskTemplateContextType.Stage &&
                 workflowStageId.HasValue &&
                 t.ApplicableWorkflowStageId == workflowStageId));
        }

        return await query
            .OrderBy(t => t.ContextType == TaskTemplateContextType.Stage ? 0 :
                          t.ContextType == contextType               ? 1 : 2)
            .ThenBy(t => t.Name)
            .ToListAsync(ct);
    }

    public async Task<LienTaskTemplate?> GetByIdAsync(
        Guid tenantId, Guid id, CancellationToken ct = default)
    {
        return await _db.LienTaskTemplates
            .Where(t => t.TenantId == tenantId && t.Id == id)
            .FirstOrDefaultAsync(ct);
    }

    public async Task AddAsync(LienTaskTemplate entity, CancellationToken ct = default)
    {
        await _db.LienTaskTemplates.AddAsync(entity, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(LienTaskTemplate entity, CancellationToken ct = default)
    {
        _db.LienTaskTemplates.Update(entity);
        await _db.SaveChangesAsync(ct);
    }
}
