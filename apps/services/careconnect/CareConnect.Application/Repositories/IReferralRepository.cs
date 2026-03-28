using CareConnect.Domain;

namespace CareConnect.Application.Repositories;

public interface IReferralRepository
{
    Task<List<Referral>> GetAllByTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<Referral?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task AddAsync(Referral referral, CancellationToken ct = default);
    Task UpdateAsync(Referral referral, CancellationToken ct = default);
}
