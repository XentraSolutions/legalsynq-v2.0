using Liens.Application.Repositories;
using Liens.Domain.Entities;
using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Liens.Infrastructure.Repositories;

public sealed class LienWorkflowConfigRepository : ILienWorkflowConfigRepository
{
    private readonly LiensDbContext _db;

    public LienWorkflowConfigRepository(LiensDbContext db) => _db = db;

    public async Task<LienWorkflowConfig?> GetByTenantProductAsync(
        Guid tenantId, string productCode, CancellationToken ct = default)
    {
        return await _db.LienWorkflowConfigs
            .Include(w => w.Stages)
            .Where(w => w.TenantId == tenantId && w.ProductCode == productCode)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<LienWorkflowConfig?> GetByIdAsync(
        Guid tenantId, Guid id, CancellationToken ct = default)
    {
        return await _db.LienWorkflowConfigs
            .Include(w => w.Stages)
            .Where(w => w.TenantId == tenantId && w.Id == id)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<LienWorkflowStage?> GetStageByIdAsync(
        Guid configId, Guid stageId, CancellationToken ct = default)
    {
        return await _db.LienWorkflowStages
            .Where(s => s.WorkflowConfigId == configId && s.Id == stageId)
            .FirstOrDefaultAsync(ct);
    }

    public async Task AddAsync(LienWorkflowConfig entity, CancellationToken ct = default)
    {
        await _db.LienWorkflowConfigs.AddAsync(entity, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(LienWorkflowConfig entity, CancellationToken ct = default)
    {
        _db.LienWorkflowConfigs.Update(entity);
        await _db.SaveChangesAsync(ct);
    }

    public async Task AddStageAsync(LienWorkflowStage stage, CancellationToken ct = default)
    {
        await _db.LienWorkflowStages.AddAsync(stage, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateStageAsync(LienWorkflowStage stage, CancellationToken ct = default)
    {
        _db.LienWorkflowStages.Update(stage);
        await _db.SaveChangesAsync(ct);
    }

    public async Task RemoveStageAsync(LienWorkflowStage stage, CancellationToken ct = default)
    {
        _db.LienWorkflowStages.Remove(stage);
        await _db.SaveChangesAsync(ct);
    }
}
