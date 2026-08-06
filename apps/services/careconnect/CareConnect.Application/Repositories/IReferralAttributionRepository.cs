using CareConnect.Domain;

namespace CareConnect.Application.Repositories;

public interface IReferralAttributionRepository
{
    Task<List<ReferralAttribution>> ListByTenantAsync(Guid tenantId, bool? activeOnly, CancellationToken ct = default);
    Task<ReferralAttribution?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<ReferralAttribution?> GetByCodeAsync(Guid tenantId, string normalizedCode, CancellationToken ct = default);
    Task AddAsync(ReferralAttribution attribution, CancellationToken ct = default);
    Task UpdateAsync(ReferralAttribution attribution, CancellationToken ct = default);
    Task<bool> IsUsedByAnyReferralAsync(Guid tenantId, Guid attributionId, CancellationToken ct = default);
}
