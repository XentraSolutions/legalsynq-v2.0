using CareConnect.Application.DTOs;

namespace CareConnect.Application.Interfaces;

public interface IReferralService
{
    Task<PagedResponse<ReferralResponse>> SearchAsync(Guid tenantId, GetReferralsQuery query, CancellationToken ct = default);
    Task<ReferralResponse> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<ReferralResponse> CreateAsync(Guid tenantId, Guid? userId, CreateReferralRequest request, CancellationToken ct = default);
    Task<ReferralResponse> UpdateAsync(Guid tenantId, Guid id, Guid? userId, UpdateReferralRequest request, CancellationToken ct = default);
    Task<List<ReferralStatusHistoryResponse>> GetHistoryAsync(Guid tenantId, Guid referralId, CancellationToken ct = default);
}
