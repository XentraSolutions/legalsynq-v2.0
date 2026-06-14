using CareConnect.Application.DTOs;
using CareConnect.Application.Interfaces;
using CareConnect.Application.Repositories;
using CareConnect.Application.Authorization;
using CareConnect.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CareConnect.Application.Services;

public class ReferralThreadService : IReferralThreadService
{
    private readonly IReferralRepository _referrals;
    private readonly IReferralCommentRepository _comments;
    private readonly IReferralAttachmentRepository _attachments;
    private readonly IReferralEmailService _emailService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReferralThreadService> _logger;

    public ReferralThreadService(
        IReferralRepository referrals,
        IReferralCommentRepository comments,
        IReferralAttachmentRepository attachments,
        IReferralEmailService emailService,
        IServiceScopeFactory scopeFactory,
        ILogger<ReferralThreadService> logger)
    {
        _referrals = referrals;
        _comments = comments;
        _attachments = attachments;
        _emailService = emailService;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<PublicReferralAccessResult<PublicReferralThreadResponse>> GetPublicThreadAccessAsync(string token, CancellationToken ct = default)
    {
        var access = await GetReferralByTokenAsync(token, ct);
        if (access.Referral is null)
            return PublicReferralAccessResult<PublicReferralThreadResponse>.Failure(
                access.FailureReason ?? ReferralTokenFailureReasons.Malformed);

        var referral = access.Referral;

        var comments = await _comments.GetByReferralAsync(referral.TenantId, referral.Id, ct);
        var attachments = await _attachments.GetByReferralAsync(referral.TenantId, referral.Id, ct);
        var treatmentTypeName = referral.TreatmentTypeId.HasValue
            ? await _referrals.GetTreatmentTypeNameAsync(referral.TreatmentTypeId.Value, ct)
            : null;

        var providerName = BuildProviderName(referral);
        return PublicReferralAccessResult<PublicReferralThreadResponse>.Success(new PublicReferralThreadResponse
        {
            ReferralId = referral.Id,
            TenantId = referral.TenantId,
            ProviderId = referral.Provider?.Id ?? referral.ProviderId,
            Status = referral.Status,
            ClientName = $"{referral.ClientFirstName} {referral.ClientLastName}".Trim(),
            ClientPhone = referral.ClientPhone,
            ClientEmail = referral.ClientEmail,
            ClientDob = referral.ClientDob.HasValue
                ? referral.ClientDob.Value.ToString("MM/dd/yyyy")
                : null,
            CaseNumber = referral.CaseNumber,
            Service = referral.RequestedService,
            Urgency = referral.Urgency,
            Notes = referral.Notes,
            ProviderName = providerName,
            ProviderEmail = referral.Provider?.Email ?? string.Empty,
            ProviderPhone = referral.Provider?.Phone ?? string.Empty,
            ProviderAddressLine1 = referral.Provider?.AddressLine1 ?? string.Empty,
            ProviderCity = referral.Provider?.City ?? string.Empty,
            ProviderState = referral.Provider?.State ?? string.Empty,
            ProviderPostalCode = referral.Provider?.PostalCode ?? string.Empty,
            ReferrerName = referral.ReferrerName,
            ReferrerEmail = referral.ReferrerEmail,
            CreatedAt = referral.CreatedAtUtc,
            TreatmentTypeId   = referral.TreatmentTypeId,
            TreatmentTypeName = treatmentTypeName,
            ProviderHasAccount = ProviderHasPortalAccount(referral.Provider),
            Comments = comments.Select(MapComment).ToList(),
            Attachments = attachments
                .OrderBy(a => a.CreatedAtUtc)
                .Select(a => new ReferralThreadAttachmentResponse
                {
                    Id = a.Id,
                    FileName = a.FileName,
                    ContentType = a.ContentType,
                    FileSizeBytes = a.FileSizeBytes,
                })
                .ToList(),
        });
    }

    public async Task<PublicReferralThreadResponse?> GetPublicThreadAsync(string token, CancellationToken ct = default)
    {
        var result = await GetPublicThreadAccessAsync(token, ct);
        return result.Data;
    }

    public async Task<ReferralCommentResponse?> PostPublicCommentAsync(
        string token,
        string senderType,
        string message,
        CancellationToken ct = default)
    {
        var access = await GetReferralByTokenAsync(token, ct);
        if (access.Referral is null)
            return null;
        var referral = access.Referral;

        var resolvedSenderName = senderType == "provider"
            ? referral.Provider?.Name ?? "Provider"
            : referral.ReferrerName ?? "Referrer";

        var comment = new ReferralComment
        {
            Id = Guid.CreateVersion7(),
            TenantId = referral.TenantId,
            ReferralId = referral.Id,
            SenderType = senderType.Trim(),
            SenderName = resolvedSenderName,
            Message = message.Trim(),
            CreatedAt = DateTime.UtcNow,
        };

        await _comments.AddAsync(comment, ct);
        QueueCommentNotification(referral, comment);
        return MapComment(comment);
    }

    public async Task<IReadOnlyList<ReferralCommentResponse>?> GetAuthenticatedCommentsAsync(
        Guid tenantId,
        Guid referralId,
        Guid? callerOrganizationId,
        string? callerEmail,
        bool useGlobalLookup,
        bool bypassParticipantCheck,
        CancellationToken ct = default)
    {
        var referral = await LoadAuthenticatedReferralAsync(
            tenantId,
            referralId,
            callerOrganizationId,
            callerEmail,
            useGlobalLookup,
            bypassParticipantCheck,
            ct);
        if (referral is null)
            return null;

        var comments = await _comments.GetByReferralAsync(referral.TenantId, referral.Id, ct);
        return comments.Select(MapComment).ToList();
    }

    public async Task<ReferralCommentResponse?> PostAuthenticatedCommentAsync(
        Guid tenantId,
        Guid referralId,
        Guid? callerOrganizationId,
        string? callerEmail,
        string senderName,
        string message,
        bool useGlobalLookup,
        CancellationToken ct = default)
    {
        var participant = await LoadAuthenticatedCommentParticipantAsync(
            tenantId,
            referralId,
            callerOrganizationId,
            callerEmail,
            useGlobalLookup,
            ct);
        if (participant is null)
            return null;

        var comment = new ReferralComment
        {
            Id = Guid.CreateVersion7(),
            TenantId = participant.Referral.TenantId,
            ReferralId = participant.Referral.Id,
            SenderType = participant.SenderType,
            SenderName = senderName.Trim(),
            Message = message.Trim(),
            CreatedAt = DateTime.UtcNow,
        };

        await _comments.AddAsync(comment, ct);
        QueueCommentNotification(participant.Referral, comment);
        return MapComment(comment);
    }

    private async Task<TokenScopedReferralResult> GetReferralByTokenAsync(string token, CancellationToken ct)
    {
        var tokenValidation = _emailService.ValidateViewTokenDetailed(token);
        if (!tokenValidation.IsValid)
        {
            LogPublicTokenFailure("thread", tokenValidation, null);
            return new TokenScopedReferralResult(null, tokenValidation.FailureReason);
        }

        var referral = await _referrals.GetByIdGlobalAsync(tokenValidation.ReferralId!.Value, ct);
        if (referral is null)
        {
            LogPublicTokenFailure(
                "thread",
                tokenValidation,
                tokenValidation.ReferralId,
                failureReasonOverride: ReferralTokenFailureReasons.ReferralNotFound);
            return new TokenScopedReferralResult(null, ReferralTokenFailureReasons.ReferralNotFound);
        }

        if (referral.TokenVersion != tokenValidation.TokenVersion)
        {
            LogPublicTokenFailure(
                "thread",
                tokenValidation,
                referral.Id,
                currentReferralTokenVersion: referral.TokenVersion,
                failureReasonOverride: ReferralTokenFailureReasons.Revoked);
            return new TokenScopedReferralResult(null, ReferralTokenFailureReasons.Revoked);
        }

        return new TokenScopedReferralResult(referral, null);
    }

    private void LogPublicTokenFailure(
        string surface,
        ReferralTokenValidationOutcome tokenValidation,
        Guid? requestedReferralId,
        int? currentReferralTokenVersion = null,
        string? failureReasonOverride = null)
    {
        var failureReason = failureReasonOverride ?? tokenValidation.FailureReason ?? ReferralTokenFailureReasons.Malformed;
        _logger.LogWarning(
            "Public referral token rejected on surface {Surface}. FailureReason={FailureReason} RequestedReferralId={RequestedReferralId} TokenReferralId={TokenReferralId} TokenVersion={TokenVersion} CurrentReferralTokenVersion={CurrentReferralTokenVersion}",
            surface,
            failureReason,
            requestedReferralId,
            tokenValidation.ReferralId,
            tokenValidation.TokenVersion,
            currentReferralTokenVersion);
    }

    private async Task<Referral?> LoadAuthenticatedReferralAsync(
        Guid tenantId,
        Guid referralId,
        Guid? callerOrganizationId,
        string? callerEmail,
        bool useGlobalLookup,
        bool bypassParticipantCheck,
        CancellationToken ct)
    {
        var referral = useGlobalLookup
            ? await _referrals.GetByIdGlobalAsync(referralId, ct)
            : await _referrals.GetByIdAsync(tenantId, referralId, ct);
        if (referral is null)
            return null;

        if (bypassParticipantCheck)
            return referral;

        return IsAuthenticatedParticipant(referral, callerOrganizationId, callerEmail)
            ? referral
            : null;
    }

    private async Task<AuthenticatedCommentParticipant?> LoadAuthenticatedCommentParticipantAsync(
        Guid tenantId,
        Guid referralId,
        Guid? callerOrganizationId,
        string? callerEmail,
        bool useGlobalLookup,
        CancellationToken ct)
    {
        var referral = await LoadAuthenticatedReferralAsync(
            tenantId,
            referralId,
            callerOrganizationId,
            callerEmail,
            useGlobalLookup,
            bypassParticipantCheck: false,
            ct);
        if (referral is null)
            return null;

        var senderType = ResolveSenderType(referral, callerOrganizationId, callerEmail);
        return senderType is null
            ? null
            : new AuthenticatedCommentParticipant(referral, senderType);
    }

    private void QueueCommentNotification(Referral referral, ReferralComment comment)
    {
        var referralId = referral.Id;
        _logger.LogInformation(
            "ReferralThread: comment posted on referral {ReferralId} by {SenderType} '{SenderName}'.",
            referralId,
            comment.SenderType,
            comment.SenderName);

        _ = Task.Run(async () =>
        {
            using var scope = _scopeFactory.CreateScope();
            var emailService = scope.ServiceProvider.GetRequiredService<IReferralEmailService>();
            try
            {
                await emailService.SendCommentNotificationAsync(referral, comment, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "ReferralThread: failed to send comment notification for referral {ReferralId}.",
                    referralId);
            }
        }, CancellationToken.None);
    }

    private static ReferralCommentResponse MapComment(ReferralComment comment) => new()
    {
        Id = comment.Id,
        SenderType = comment.SenderType,
        SenderName = comment.SenderName,
        Message = comment.Message,
        CreatedAt = comment.CreatedAt,
    };

    private static string BuildProviderName(Referral referral)
    {
        if (referral.Provider is null)
            return "Provider";

        return string.IsNullOrWhiteSpace(referral.Provider.OrganizationName)
            ? referral.Provider.Name
            : referral.Provider.OrganizationName;
    }

    private static bool ProviderHasPortalAccount(Provider? provider) =>
        provider is not null && (
            ProviderAccessStage.IsAtLeast(provider.AccessStage, ProviderAccessStage.CommonPortal) ||
            provider.OrganizationId.HasValue ||
            provider.IdentityUserId.HasValue);

    private static bool IsAuthenticatedParticipant(Referral referral, Guid? callerOrganizationId, string? callerEmail)
    {
        if (CareConnectParticipantHelper.IsReferralParticipant(referral, callerOrganizationId))
            return true;

        return referral.ReferringOrganizationId is null
            && !string.IsNullOrWhiteSpace(callerEmail)
            && string.Equals(referral.ReferrerEmail, callerEmail, StringComparison.OrdinalIgnoreCase);
    }

    private static string? ResolveSenderType(Referral referral, Guid? callerOrganizationId, string? callerEmail)
    {
        if (callerOrganizationId.HasValue && referral.ReceivingOrganizationId == callerOrganizationId)
            return "provider";

        if (callerOrganizationId.HasValue && referral.ReferringOrganizationId == callerOrganizationId)
            return "referrer";

        if (referral.ReferringOrganizationId is null
            && !string.IsNullOrWhiteSpace(callerEmail)
            && string.Equals(referral.ReferrerEmail, callerEmail, StringComparison.OrdinalIgnoreCase))
        {
            return "referrer";
        }

        return null;
    }

    private sealed record AuthenticatedCommentParticipant(Referral Referral, string SenderType);
    private sealed record TokenScopedReferralResult(Referral? Referral, string? FailureReason);
}
