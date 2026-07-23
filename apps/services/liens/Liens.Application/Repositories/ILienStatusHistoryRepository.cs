using Liens.Domain.Entities;

namespace Liens.Application.Repositories;

public interface ILienStatusHistoryRepository
{
    Task<List<LienStatusHistory>> GetByCaseIdAsync(Guid tenantId, Guid caseId, CancellationToken ct = default);
    Task AddAsync(LienStatusHistory entity, CancellationToken ct = default);
}
