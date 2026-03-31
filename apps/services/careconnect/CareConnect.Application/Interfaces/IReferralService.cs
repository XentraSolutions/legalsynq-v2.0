using CareConnect.Application.DTOs;

namespace CareConnect.Application.Interfaces;

public interface IReferralService
{
    Task<PagedResponse<ReferralResponse>> SearchAsync(Guid tenantId, GetReferralsQuery query, CancellationToken ct = default);
    Task<ReferralResponse> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<ReferralResponse> CreateAsync(Guid tenantId, Guid? userId, CreateReferralRequest request, CancellationToken ct = default);
    Task<ReferralResponse> UpdateAsync(Guid tenantId, Guid id, Guid? userId, UpdateReferralRequest request, CancellationToken ct = default);
    Task<List<ReferralStatusHistoryResponse>> GetHistoryAsync(Guid tenantId, Guid referralId, CancellationToken ct = default);

    // LSCC-005: Public token-based endpoints (no auth context)
    Task<ReferralViewTokenRouteResponse> ResolveViewTokenAsync(string token, CancellationToken ct = default);
    Task<ReferralResponse> AcceptByTokenAsync(Guid referralId, string token, CancellationToken ct = default);

    // LSCC-005-01: Hardening — resend, revoke, notification history
    Task<ReferralResponse> ResendEmailAsync(Guid tenantId, Guid referralId, CancellationToken ct = default);
    Task<ReferralResponse> RevokeTokenAsync(Guid tenantId, Guid referralId, CancellationToken ct = default);
    Task<List<ReferralNotificationResponse>> GetNotificationsAsync(Guid tenantId, Guid referralId, CancellationToken ct = default);

    // LSCC-005-02: Operational audit timeline (status history + notification events, chrono-ordered)
    Task<List<ReferralAuditEventResponse>> GetAuditTimelineAsync(Guid tenantId, Guid referralId, CancellationToken ct = default);
}
