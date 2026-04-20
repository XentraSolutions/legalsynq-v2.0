using Liens.Application.DTOs;

namespace Liens.Application.Interfaces;

public interface ILienTaskGovernanceService
{
    Task<TaskGovernanceSettingsResponse> GetOrCreateAsync(
        Guid tenantId, Guid actingUserId, string updateSource, CancellationToken ct = default);

    Task<TaskGovernanceSettingsResponse> UpdateAsync(
        Guid tenantId, Guid actingUserId,
        UpdateTaskGovernanceSettingsRequest request, CancellationToken ct = default);
}
