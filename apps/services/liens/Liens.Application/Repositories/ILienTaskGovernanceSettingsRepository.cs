using Liens.Domain.Entities;

namespace Liens.Application.Repositories;

public interface ILienTaskGovernanceSettingsRepository
{
    Task<LienTaskGovernanceSettings?> GetByTenantProductAsync(
        Guid tenantId, string productCode, CancellationToken ct = default);

    /// <summary>
    /// TASK-MIG-01 — returns all governance settings rows across all tenants.
    /// Used only by the startup migration service.
    /// </summary>
    Task<IReadOnlyList<LienTaskGovernanceSettings>> GetAllAsync(CancellationToken ct = default);

    Task AddAsync(LienTaskGovernanceSettings entity, CancellationToken ct = default);
    Task UpdateAsync(LienTaskGovernanceSettings entity, CancellationToken ct = default);
}
