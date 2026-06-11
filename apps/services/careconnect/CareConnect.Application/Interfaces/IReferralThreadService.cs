using CareConnect.Application.DTOs;

namespace CareConnect.Application.Interfaces;

public interface IReferralThreadService
{
    Task<PublicReferralAccessResult<PublicReferralThreadResponse>> GetPublicThreadAccessAsync(string token, CancellationToken ct = default);
    Task<PublicReferralThreadResponse?> GetPublicThreadAsync(string token, CancellationToken ct = default);
    Task<ReferralCommentResponse?> PostPublicCommentAsync(
        string token,
        string senderType,
        string senderName,
        string message,
        CancellationToken ct = default);

    Task<IReadOnlyList<ReferralCommentResponse>?> GetAuthenticatedCommentsAsync(
        Guid tenantId,
        Guid referralId,
        Guid? callerOrganizationId,
        string? callerEmail,
        bool useGlobalLookup,
        bool bypassParticipantCheck,
        CancellationToken ct = default);

    Task<ReferralCommentResponse?> PostAuthenticatedCommentAsync(
        Guid tenantId,
        Guid referralId,
        Guid? callerOrganizationId,
        string? callerEmail,
        string senderName,
        string message,
        bool useGlobalLookup,
        CancellationToken ct = default);
}
