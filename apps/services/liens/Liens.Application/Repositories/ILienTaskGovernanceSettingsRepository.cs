using Liens.Domain.Entities;

namespace Liens.Application.Repositories;

public interface ILienTaskGovernanceSettingsRepository
{
    Task<LienTaskGovernanceSettings?> GetByTenantProductAsync(
        Guid tenantId, string productCode, CancellationToken ct = default);

    Task AddAsync(LienTaskGovernanceSettings entity, CancellationToken ct = default);
    Task UpdateAsync(LienTaskGovernanceSettings entity, CancellationToken ct = default);
}
