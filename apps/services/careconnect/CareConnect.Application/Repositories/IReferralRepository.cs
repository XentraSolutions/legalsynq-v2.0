using CareConnect.Application.DTOs;
using CareConnect.Domain;

namespace CareConnect.Application.Repositories;

public interface IReferralRepository
{
    Task<(List<Referral> Items, int TotalCount)> SearchAsync(Guid tenantId, GetReferralsQuery query, CancellationToken ct = default);
    Task<Referral?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    /// <summary>
    /// Referral Representative visibility scope: loads a referral only when it belongs to the
    /// given tenant AND its ReferralAttributionId is in <paramref name="allowedAttributionIds"/>.
    /// Returns null for every unauthorized case (not found, wrong tenant, unattributed, attributed
    /// to a different source) — the caller cannot distinguish "doesn't exist" from "not authorized",
    /// by design (BuildingBlocks §16: never expose whether an unauthorized referral exists).
    /// </summary>
    Task<Referral?> GetByIdForAttributionsAsync(Guid tenantId, Guid id, IReadOnlyList<Guid> allowedAttributionIds, CancellationToken ct = default);
    /// <summary>
    /// LSCC-005: Loads a referral by ID without tenant scoping.
    /// Used only by public token-based endpoints where no tenant context is available.
    /// </summary>
    Task<Referral?> GetByIdGlobalAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(Referral referral, CancellationToken ct = default);
    Task UpdateAsync(Referral referral, ReferralStatusHistory? history = null, ReferralProviderReassignment? providerReassignment = null, CancellationToken ct = default);
    Task<int> BackfillReferringOrganizationByEmailAsync(Guid tenantId, string referrerEmail, Guid organizationId, CancellationToken ct = default);
    Task<int> BackfillReceivingOrganizationAsync(Guid tenantId, Guid providerId, Guid organizationId, CancellationToken ct = default);
    Task<List<ReferralStatusHistory>> GetHistoryByReferralAsync(Guid tenantId, Guid referralId, CancellationToken ct = default);
    Task AddProviderReassignmentAsync(ReferralProviderReassignment reassignment, CancellationToken ct = default);
    Task<List<ReferralProviderReassignment>> GetProviderReassignmentsByReferralAsync(Guid tenantId, Guid referralId, CancellationToken ct = default);
    /// <summary>Returns a map of ProviderId → first network name for the given provider IDs.</summary>
    Task<Dictionary<Guid, string>> GetProviderNetworkNamesAsync(IEnumerable<Guid> providerIds, CancellationToken ct = default);
    /// <summary>Looks up the display name of a treatment type by ID. Returns null when not found.</summary>
    Task<string?> GetTreatmentTypeNameAsync(Guid id, CancellationToken ct = default);
}
