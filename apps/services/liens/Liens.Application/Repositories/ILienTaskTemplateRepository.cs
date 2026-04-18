using Liens.Domain.Entities;

namespace Liens.Application.Repositories;

public interface ILienTaskTemplateRepository
{
    Task<List<LienTaskTemplate>> GetByTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<List<LienTaskTemplate>> GetActiveByTenantAsync(Guid tenantId, string? contextType, Guid? workflowStageId, CancellationToken ct = default);
    Task<LienTaskTemplate?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task AddAsync(LienTaskTemplate entity, CancellationToken ct = default);
    Task UpdateAsync(LienTaskTemplate entity, CancellationToken ct = default);
}
