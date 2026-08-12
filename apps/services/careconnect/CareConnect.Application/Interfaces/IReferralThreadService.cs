using CareConnect.Application.DTOs;

namespace CareConnect.Application.Interfaces;

public interface IReferralThreadService
{
    Task<PublicReferralAccessResult<PublicReferralThreadResponse>> GetPublicThreadAccessAsync(string token, CancellationToken ct = default);
    Task<PublicReferralThreadResponse?> GetPublicThreadAsync(string token, CancellationToken ct = default);
    Task<ReferralCommentResponse?> PostPublicCommentAsync(
        string token,
        string senderType,
        string message,
        CancellationToken ct = default);

    Task<ReferralCommentResponse?> PostPublicCommentWithAttachmentsAsync(
        string token,
        string senderType,
        string message,
        IReadOnlyList<ReferralMessageAttachmentUpload> attachments,
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

    Task<ReferralCommentResponse?> PostAuthenticatedCommentWithAttachmentsAsync(
        Guid tenantId,
        Guid referralId,
        Guid? callerOrganizationId,
        string? callerEmail,
        string senderName,
        string message,
        bool useGlobalLookup,
        IReadOnlyList<ReferralMessageAttachmentUpload> attachments,
        Guid? createdByUserId = null,
        CancellationToken ct = default);
}
