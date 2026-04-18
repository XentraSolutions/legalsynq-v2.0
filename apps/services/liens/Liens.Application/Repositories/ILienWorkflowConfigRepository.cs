using Liens.Domain.Entities;

namespace Liens.Application.Repositories;

public interface ILienWorkflowConfigRepository
{
    Task<LienWorkflowConfig?> GetByTenantProductAsync(Guid tenantId, string productCode, CancellationToken ct = default);
    Task<LienWorkflowConfig?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<LienWorkflowStage?> GetStageByIdAsync(Guid configId, Guid stageId, CancellationToken ct = default);
    Task AddAsync(LienWorkflowConfig entity, CancellationToken ct = default);
    Task UpdateAsync(LienWorkflowConfig entity, CancellationToken ct = default);
    Task AddStageAsync(LienWorkflowStage stage, CancellationToken ct = default);
    Task UpdateStageAsync(LienWorkflowStage stage, CancellationToken ct = default);
    Task RemoveStageAsync(LienWorkflowStage stage, CancellationToken ct = default);
}
