using CareConnect.Domain;

namespace CareConnect.Application.Repositories;

public interface IReferralAttributionAccessCodeRepository
{
    Task<List<ReferralAttributionAccessCode>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<ReferralAttributionAccessCode?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<ReferralAttributionAccessCode?> GetByHashAsync(Guid tenantId, string codeHash, CancellationToken ct = default);

    Task<int> CountActiveAsync(Guid tenantId, Guid referralAttributionId, CancellationToken ct = default);

    /// <summary>
    /// The single active (non-revoked) code for this attribution, if one exists. Attributions
    /// are limited to exactly one active code at a time — see
    /// ReferralAttributionAccessCodeService.GenerateAsync's conflict check.
    /// </summary>
    Task<ReferralAttributionAccessCode?> GetActiveByAttributionAsync(Guid tenantId, Guid referralAttributionId, CancellationToken ct = default);

    Task AddAsync(ReferralAttributionAccessCode code, CancellationToken ct = default);
    Task UpdateAsync(ReferralAttributionAccessCode code, CancellationToken ct = default);
}
