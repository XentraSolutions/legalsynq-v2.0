using CareConnect.Application.DTOs;

namespace CareConnect.Application.Interfaces;

/// <summary>
/// Anonymous, read-only referral access for the Representative Portal. Every method takes
/// an already-verified (tenantId, referralAttributionId) pair — callers must resolve and
/// re-verify the access code themselves (see PublicRepresentativeEndpoints) before calling
/// in; this service trusts that pairing and never re-checks the code itself.
/// </summary>
public interface IRepresentativeReferralService
{
    Task<PagedResponse<RepresentativeReferralListItem>> SearchAsync(
        Guid tenantId, Guid referralAttributionId, GetRepresentativeReferralsQuery query, CancellationToken ct = default);

    /// <summary>Returns null when the referral doesn't exist or isn't attributed to this attribution — caller must respond with a generic 404.</summary>
    Task<RepresentativeReferralDetailResponse?> GetByIdAsync(
        Guid tenantId, Guid referralAttributionId, Guid referralId, CancellationToken ct = default);

    Task<RepresentativeReferralMetricsResponse> GetMetricsAsync(
        Guid tenantId, Guid referralAttributionId, DateTime? from, DateTime? to, CancellationToken ct = default);
}
