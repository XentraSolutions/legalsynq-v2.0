using Liens.Domain.Entities;

namespace Liens.Application.Repositories;

public interface IDIYReportConfigRepository
{
    Task<DIYReportConfig?>      GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<List<DIYReportConfig>> GetByUserAsync(Guid tenantId, Guid userId, CancellationToken ct = default);
    Task AddAsync(DIYReportConfig config, CancellationToken ct = default);
    Task UpdateAsync(DIYReportConfig config, CancellationToken ct = default);
}
