using System.Security.Cryptography;
using System.Text;
using CareConnect.Application.DTOs;
using CareConnect.Application.Interfaces;
using CareConnect.Application.Repositories;
using CareConnect.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CareConnect.Application.Services;

/// <summary>
/// LSCC-005 / LSCC-005-01 / LSCC-005-02 / LSCC-01-002: Implements secure token generation and
/// email notification dispatch for the referral flow.
///
/// Token format (URL-safe Base64, LSCC-005-01):
///   {referralId}:{tokenVersion}:{expiryUnixSeconds}:{hmacHex}
///
/// The HMAC-SHA256 covers "{referralId}:{tokenVersion}:{expiryUnixSeconds}" using a
/// secret key from configuration key "ReferralToken:Secret". Falls back to a development
/// constant when the key is absent (NOT suitable for production).
///
/// Token revocation: incrementing a referral's TokenVersion invalidates all previously
/// issued tokens. ValidateViewToken returns the embedded version; callers must verify
/// it matches the referral's current TokenVersion.
///
/// Email strategy: a CareConnectNotification DB record is always written first
/// (Pending status). An SMTP send is then attempted; success → Sent (AttemptCount++),
/// failure → Failed (AttemptCount++, FailureReason stored). Never a silent failure.
/// </summary>
public class ReferralEmailService : IReferralEmailService
{
    private const int TokenExpiryDays      = 30;
    private const string DevFallbackSecret = "LEGALSYNQ-DEV-REFERRAL-TOKEN-SECRET-2026";

    private readonly INotificationRepository      _notifications;
    private readonly INotificationsProducer       _producer;
    private readonly ILogger<ReferralEmailService> _logger;
    private readonly string _tokenSecret;
    private readonly string _appBaseUrl;

    public ReferralEmailService(
        INotificationRepository  notifications,
        INotificationsProducer   producer,
        IConfiguration           configuration,
        ILogger<ReferralEmailService> logger)
    {
        _notifications = notifications;
        _producer      = producer;
        _logger        = logger;
        _appBaseUrl    = (configuration["AppBaseUrl"] ?? "http://localhost:3000").TrimEnd('/');

        // CC2-INT-B03: Hard enforcement — DevFallbackSecret is blocked outside Development.
        // IsNullOrWhiteSpace ensures a blank/whitespace-only value is treated the same as a missing secret.
        var secret      = configuration["ReferralToken:Secret"];
        var environment = configuration["ASPNETCORE_ENVIRONMENT"] ?? "Production";
        if (string.IsNullOrWhiteSpace(secret))
        {
            var isDevelopment = string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase);
            if (!isDevelopment)
                throw new InvalidOperationException(
                    "ReferralToken:Secret must be configured in non-Development environments. " +
                    "Set the 'ReferralToken:Secret' configuration key to a strong random value. " +
                    $"Current environment: '{environment}'.");

            _tokenSecret = DevFallbackSecret;
            _logger.LogWarning(
                "ReferralToken:Secret is not configured. Using development fallback — " +
                "DO NOT use this in production.");
        }
        else
        {
            _tokenSecret = secret;
        }
    }

    // ── Token helpers ─────────────────────────────────────────────────────────

    /// <summary>
    /// LSCC-005-01: Generates a 4-part HMAC-signed view token.
    /// Format: {referralId}:{tokenVersion}:{expiry}:{hmacHex}, Base64url-encoded.
    /// The version is embedded so revocation can be detected without a DB lookup
    /// at the HMAC validation step.
    /// </summary>
    public string GenerateViewToken(Guid referralId, int tokenVersion)
    {
        var expiry  = DateTimeOffset.UtcNow.AddDays(TokenExpiryDays).ToUnixTimeSeconds();
        var payload = $"{referralId}:{tokenVersion}:{expiry}";
        var sig     = ComputeHmac(payload);
        var raw     = $"{payload}:{sig}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(raw))
                      .TrimEnd('=')
                      .Replace('+', '-')
                      .Replace('/', '_');
    }

    /// <summary>
    /// LSCC-005-01: Validates a view token. Returns a ViewTokenValidationResult containing
    /// the referralId and the embedded tokenVersion. Returns null if the token is
    /// invalid, tampered with, or expired.
    ///
    /// The caller must also verify that result.TokenVersion matches the referral's
    /// current TokenVersion to detect revoked tokens.
    /// </summary>
    public ViewTokenValidationResult? ValidateViewToken(string token)
    {
        try
        {
            var padded  = token.Replace('-', '+').Replace('_', '/');
            var mod     = padded.Length % 4;
            if (mod != 0) padded += new string('=', 4 - mod);
            var raw     = Encoding.UTF8.GetString(Convert.FromBase64String(padded));
            var parts   = raw.Split(':');

            // LSCC-005-01: 4-part format: referralId:version:expiry:hmac
            if (parts.Length != 4) return null;

            var referralId   = Guid.Parse(parts[0]);
            var tokenVersion = int.Parse(parts[1]);
            var expiry       = long.Parse(parts[2]);
            var sig          = parts[3];

            if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expiry)
            {
                _logger.LogInformation("Referral view token expired for referral {ReferralId}", referralId);
                return null;
            }

            var expectedSig = ComputeHmac($"{referralId}:{tokenVersion}:{expiry}");
            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(sig),
                    Encoding.UTF8.GetBytes(expectedSig)))
            {
                _logger.LogWarning("Referral view token HMAC mismatch — possible tampering.");
                return null;
            }

            return new ViewTokenValidationResult(referralId, tokenVersion);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse referral view token.");
            return null;
        }
    }

    private string ComputeHmac(string payload)
    {
        var keyBytes     = Encoding.UTF8.GetBytes(_tokenSecret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        using var hmac   = new HMACSHA256(keyBytes);
        return Convert.ToHexString(hmac.ComputeHash(payloadBytes)).ToLowerInvariant();
    }

    // ── Email dispatch ────────────────────────────────────────────────────────

    public async Task SendNewReferralNotificationAsync(
        Referral referral,
        Provider provider,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(provider.Email))
        {
            _logger.LogWarning(
                "Cannot send new-referral notification: provider {ProviderId} has no email address.",
                provider.Id);
            return;
        }

        var dedupeKey = $"referral:{referral.Id}:created:provider";

        var token     = GenerateViewToken(referral.Id, referral.TokenVersion);
        var viewLink  = $"{_appBaseUrl}/referrals/view?token={token}";
        var subject   = $"New referral received — {referral.ClientFirstName} {referral.ClientLastName}";
        var body      = BuildNewReferralEmailHtml(referral, provider, viewLink);

        var notification = CareConnectNotification.Create(
            tenantId:          referral.TenantId,
            notificationType:  NotificationType.ReferralCreated,
            relatedEntityType: NotificationRelatedEntityType.Referral,
            relatedEntityId:   referral.Id,
            recipientType:     NotificationRecipientType.Provider,
            recipientAddress:  provider.Email,
            subject:           subject,
            message:           viewLink,
            scheduledForUtc:   null,
            createdByUserId:   referral.CreatedByUserId,
            triggerSource:     NotificationSource.Initial,
            dedupeKey:         dedupeKey);

        if (!await _notifications.TryAddWithDedupeAsync(notification, ct))
        {
            _logger.LogInformation("Duplicate new-referral notification skipped for referral {ReferralId}.", referral.Id);
            return;
        }

        // LSCC-005-02: schedule retry on failure (attempt 1 → retry after 5 min)
        await TrySendAndUpdateAsync(notification, provider.Email, subject, body, ct,
            nextRetryAfterUtcOnFailure: ReferralRetryPolicy.GetNextRetryAfter(1));
    }

    /// <summary>
    /// LSCC-005-01: Resends the provider notification email for an existing referral.
    /// Creates a new notification record (type: ReferralEmailResent) and sends the
    /// email with a fresh token using the referral's CURRENT TokenVersion.
    /// Old revoked tokens (lower version) cannot be reinstated by resend.
    /// </summary>
    public async Task ResendNewReferralNotificationAsync(
        Referral referral,
        Provider provider,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(provider.Email))
        {
            _logger.LogWarning(
                "Cannot resend referral notification: provider {ProviderId} has no email address.",
                provider.Id);
            return;
        }

        var token     = GenerateViewToken(referral.Id, referral.TokenVersion);
        var viewLink  = $"{_appBaseUrl}/referrals/view?token={token}";
        var subject   = $"Referral (resent) — {referral.ClientFirstName} {referral.ClientLastName}";
        var body      = BuildNewReferralEmailHtml(referral, provider, viewLink);

        var notification = CareConnectNotification.Create(
            tenantId:          referral.TenantId,
            notificationType:  NotificationType.ReferralEmailResent,
            relatedEntityType: NotificationRelatedEntityType.Referral,
            relatedEntityId:   referral.Id,
            recipientType:     NotificationRecipientType.Provider,
            recipientAddress:  provider.Email,
            subject:           subject,
            message:           viewLink,
            scheduledForUtc:   null,
            createdByUserId:   null,
            triggerSource:     NotificationSource.ManualResend);

        await _notifications.AddAsync(notification, ct);

        _logger.LogInformation(
            "Resending referral notification for referral {ReferralId} (TokenVersion={Version}) to {Email}.",
            referral.Id, referral.TokenVersion, provider.Email);

        await TrySendAndUpdateAsync(notification, provider.Email, subject, body, ct);
    }

    /// <summary>
    /// CC2-INT-B03: PROVIDER_ASSIGNED event — fires when a provider is explicitly assigned
    /// to an existing referral (e.g. re-assignment or delayed assignment after creation).
    /// Routes through the platform Notifications service exactly like all other events.
    /// </summary>
    public async Task SendProviderAssignedNotificationAsync(
        Referral referral,
        Provider provider,
        Guid?    actingUserId,
        string   dedupeKeySuffix = "",
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(provider.Email))
        {
            _logger.LogWarning(
                "Cannot send provider-assigned notification: provider {ProviderId} has no email address.",
                provider.Id);
            return;
        }

        var dedupeKey = $"referral:{referral.Id}:provider_assigned:{provider.Id}{dedupeKeySuffix}";
        var token     = GenerateViewToken(referral.Id, referral.TokenVersion);
        var viewLink  = $"{_appBaseUrl}/referrals/view?token={token}";
        var subject   = $"You have been assigned a referral — {referral.ClientFirstName} {referral.ClientLastName}";
        var body      = BuildProviderAssignedEmailHtml(referral, provider, viewLink);

        var notification = CareConnectNotification.Create(
            tenantId:          referral.TenantId,
            notificationType:  NotificationType.ReferralProviderAssigned,
            relatedEntityType: NotificationRelatedEntityType.Referral,
            relatedEntityId:   referral.Id,
            recipientType:     NotificationRecipientType.Provider,
            recipientAddress:  provider.Email,
            subject:           subject,
            message:           viewLink,
            scheduledForUtc:   null,
            createdByUserId:   actingUserId,
            triggerSource:     NotificationSource.Initial,
            dedupeKey:         dedupeKey);

        if (!await _notifications.TryAddWithDedupeAsync(notification, ct))
        {
            _logger.LogInformation(
                "Duplicate provider-assigned notification skipped for referral {ReferralId}.", referral.Id);
            return;
        }

        await TrySendAndUpdateAsync(notification, provider.Email, subject, body, ct,
            nextRetryAfterUtcOnFailure: ReferralRetryPolicy.GetNextRetryAfter(1));
    }

    public async Task SendAcceptanceConfirmationsAsync(
        Referral referral,
        Provider provider,
        CancellationToken ct = default)
    {
        var tasks = new List<Task>();
        var dedupePrefix = $"referral:{referral.Id}:accepted";

        if (!string.IsNullOrWhiteSpace(provider.Email))
        {
            var provDedupeKey = $"{dedupePrefix}:provider";
            var provSubject = $"Referral accepted — {referral.ClientFirstName} {referral.ClientLastName}";
            var provBody    = BuildProviderAcceptanceHtml(referral, provider);

            var provNotif = CareConnectNotification.Create(
                tenantId:          referral.TenantId,
                notificationType:  NotificationType.ReferralAcceptedProvider,
                relatedEntityType: NotificationRelatedEntityType.Referral,
                relatedEntityId:   referral.Id,
                recipientType:     NotificationRecipientType.Provider,
                recipientAddress:  provider.Email,
                subject:           provSubject,
                message:           null,
                scheduledForUtc:   null,
                createdByUserId:   null,
                triggerSource:     NotificationSource.Initial,
                dedupeKey:         provDedupeKey);

            if (await _notifications.TryAddWithDedupeAsync(provNotif, ct))
            {
                tasks.Add(TrySendAndUpdateAsync(provNotif, provider.Email, provSubject, provBody, ct,
                    nextRetryAfterUtcOnFailure: ReferralRetryPolicy.GetNextRetryAfter(1)));
            }
        }
        else
        {
            _logger.LogWarning(
                "Skipping provider acceptance email: provider {ProviderId} has no email address.",
                provider.Id);
        }

        if (!string.IsNullOrWhiteSpace(referral.ReferrerEmail))
        {
            var refDedupeKey = $"{dedupePrefix}:referrer";
            var refSubject = $"Your referral was accepted — {referral.ClientFirstName} {referral.ClientLastName}";
            var refBody    = BuildReferrerAcceptanceHtml(referral, provider);

            var refNotif = CareConnectNotification.Create(
                tenantId:          referral.TenantId,
                notificationType:  NotificationType.ReferralAcceptedReferrer,
                relatedEntityType: NotificationRelatedEntityType.Referral,
                relatedEntityId:   referral.Id,
                recipientType:     NotificationRecipientType.InternalUser,
                recipientAddress:  referral.ReferrerEmail,
                subject:           refSubject,
                message:           null,
                scheduledForUtc:   null,
                createdByUserId:   null,
                triggerSource:     NotificationSource.Initial,
                dedupeKey:         refDedupeKey);

            if (await _notifications.TryAddWithDedupeAsync(refNotif, ct))
            {
                tasks.Add(TrySendAndUpdateAsync(refNotif, referral.ReferrerEmail, refSubject, refBody, ct,
                    nextRetryAfterUtcOnFailure: ReferralRetryPolicy.GetNextRetryAfter(1)));
            }
        }
        else
        {
            _logger.LogWarning(
                "Skipping referrer acceptance email for referral {ReferralId}: no ReferrerEmail stored.",
                referral.Id);
        }

        if (!string.IsNullOrWhiteSpace(referral.ClientEmail))
        {
            var clientDedupeKey = $"{dedupePrefix}:client";
            var clientSubject = $"Your case has been accepted — {provider.OrganizationName ?? provider.Name}";
            var clientBody    = BuildClientAcceptanceHtml(referral, provider);

            var clientNotif = CareConnectNotification.Create(
                tenantId:          referral.TenantId,
                notificationType:  NotificationType.ReferralAcceptedClient,
                relatedEntityType: NotificationRelatedEntityType.Referral,
                relatedEntityId:   referral.Id,
                recipientType:     NotificationRecipientType.ClientEmail,
                recipientAddress:  referral.ClientEmail,
                subject:           clientSubject,
                message:           null,
                scheduledForUtc:   null,
                createdByUserId:   null,
                triggerSource:     NotificationSource.Initial,
                dedupeKey:         clientDedupeKey);

            if (await _notifications.TryAddWithDedupeAsync(clientNotif, ct))
            {
                tasks.Add(TrySendAndUpdateAsync(clientNotif, referral.ClientEmail, clientSubject, clientBody, ct,
                    nextRetryAfterUtcOnFailure: ReferralRetryPolicy.GetNextRetryAfter(1)));
            }
        }
        else
        {
            _logger.LogWarning(
                "Skipping client acceptance email for referral {ReferralId}: no ClientEmail stored. " +
                "Acceptance is not blocked — provider and referrer have been notified.",
                referral.Id);
        }

        await Task.WhenAll(tasks);
    }

    // ── CCX-002: Rejection notifications ─────────────────────────────────────

    public async Task SendRejectionNotificationsAsync(
        Referral referral,
        Provider provider,
        Guid? actingUserId,
        CancellationToken ct = default)
    {
        var tasks = new List<Task>();

        var dedupePrefix = $"referral:{referral.Id}:declined";

        if (!string.IsNullOrWhiteSpace(provider.Email))
        {
            var provDedupeKey = $"{dedupePrefix}:provider";
            var provSubject = $"Referral declined — {referral.ClientFirstName} {referral.ClientLastName}";
            var provBody    = BuildProviderRejectionHtml(referral, provider);

            var provNotif = CareConnectNotification.Create(
                tenantId:          referral.TenantId,
                notificationType:  NotificationType.ReferralRejectedProvider,
                relatedEntityType: NotificationRelatedEntityType.Referral,
                relatedEntityId:   referral.Id,
                recipientType:     NotificationRecipientType.Provider,
                recipientAddress:  provider.Email,
                subject:           provSubject,
                message:           null,
                scheduledForUtc:   null,
                createdByUserId:   actingUserId,
                triggerSource:     NotificationSource.Initial,
                dedupeKey:         provDedupeKey);

            if (await _notifications.TryAddWithDedupeAsync(provNotif, ct))
            {
                tasks.Add(TrySendAndUpdateAsync(provNotif, provider.Email, provSubject, provBody, ct,
                    nextRetryAfterUtcOnFailure: ReferralRetryPolicy.GetNextRetryAfter(1)));
            }
        }
        else
        {
            _logger.LogWarning(
                "Skipping provider rejection email for referral {ReferralId}: provider {ProviderId} has no email address.",
                referral.Id, provider.Id);
        }

        if (!string.IsNullOrWhiteSpace(referral.ReferrerEmail))
        {
            var refDedupeKey = $"{dedupePrefix}:referrer";
            var refSubject = $"Your referral was declined — {referral.ClientFirstName} {referral.ClientLastName}";
            var refBody    = BuildReferrerRejectionHtml(referral, provider);

            var refNotif = CareConnectNotification.Create(
                tenantId:          referral.TenantId,
                notificationType:  NotificationType.ReferralRejectedReferrer,
                relatedEntityType: NotificationRelatedEntityType.Referral,
                relatedEntityId:   referral.Id,
                recipientType:     NotificationRecipientType.InternalUser,
                recipientAddress:  referral.ReferrerEmail,
                subject:           refSubject,
                message:           null,
                scheduledForUtc:   null,
                createdByUserId:   actingUserId,
                triggerSource:     NotificationSource.Initial,
                dedupeKey:         refDedupeKey);

            if (await _notifications.TryAddWithDedupeAsync(refNotif, ct))
            {
                tasks.Add(TrySendAndUpdateAsync(refNotif, referral.ReferrerEmail, refSubject, refBody, ct,
                    nextRetryAfterUtcOnFailure: ReferralRetryPolicy.GetNextRetryAfter(1)));
            }
        }

        await Task.WhenAll(tasks);
    }

    // ── CCX-002: Cancellation notifications ───────────────────────────────────

    public async Task SendCancellationNotificationsAsync(
        Referral referral,
        Provider provider,
        Guid? actingUserId,
        CancellationToken ct = default)
    {
        var tasks = new List<Task>();

        var dedupePrefix = $"referral:{referral.Id}:cancelled";

        if (!string.IsNullOrWhiteSpace(provider.Email))
        {
            var provDedupeKey = $"{dedupePrefix}:provider";
            var provSubject = $"Referral cancelled — {referral.ClientFirstName} {referral.ClientLastName}";
            var provBody    = BuildProviderCancellationHtml(referral, provider);

            var provNotif = CareConnectNotification.Create(
                tenantId:          referral.TenantId,
                notificationType:  NotificationType.ReferralCancelledProvider,
                relatedEntityType: NotificationRelatedEntityType.Referral,
                relatedEntityId:   referral.Id,
                recipientType:     NotificationRecipientType.Provider,
                recipientAddress:  provider.Email,
                subject:           provSubject,
                message:           null,
                scheduledForUtc:   null,
                createdByUserId:   actingUserId,
                triggerSource:     NotificationSource.Initial,
                dedupeKey:         provDedupeKey);

            if (await _notifications.TryAddWithDedupeAsync(provNotif, ct))
            {
                tasks.Add(TrySendAndUpdateAsync(provNotif, provider.Email, provSubject, provBody, ct,
                    nextRetryAfterUtcOnFailure: ReferralRetryPolicy.GetNextRetryAfter(1)));
            }
        }
        else
        {
            _logger.LogWarning(
                "Skipping provider cancellation email for referral {ReferralId}: provider {ProviderId} has no email address.",
                referral.Id, provider.Id);
        }

        if (!string.IsNullOrWhiteSpace(referral.ReferrerEmail))
        {
            var refDedupeKey = $"{dedupePrefix}:referrer";
            var refSubject = $"Your referral was cancelled — {referral.ClientFirstName} {referral.ClientLastName}";
            var refBody    = BuildReferrerCancellationHtml(referral, provider);

            var refNotif = CareConnectNotification.Create(
                tenantId:          referral.TenantId,
                notificationType:  NotificationType.ReferralCancelledReferrer,
                relatedEntityType: NotificationRelatedEntityType.Referral,
                relatedEntityId:   referral.Id,
                recipientType:     NotificationRecipientType.InternalUser,
                recipientAddress:  referral.ReferrerEmail,
                subject:           refSubject,
                message:           null,
                scheduledForUtc:   null,
                createdByUserId:   actingUserId,
                triggerSource:     NotificationSource.Initial,
                dedupeKey:         refDedupeKey);

            if (await _notifications.TryAddWithDedupeAsync(refNotif, ct))
            {
                tasks.Add(TrySendAndUpdateAsync(refNotif, referral.ReferrerEmail, refSubject, refBody, ct,
                    nextRetryAfterUtcOnFailure: ReferralRetryPolicy.GetNextRetryAfter(1)));
            }
        }

        await Task.WhenAll(tasks);
    }

    // ── Retry (LSCC-005-02) ───────────────────────────────────────────────────

    /// <summary>
    /// LSCC-005-02: Re-attempts an email send for an existing failed notification record.
    /// Called exclusively by <c>ReferralEmailRetryWorker</c>.
    ///
    /// Rebuilds the email body from the current referral/provider state (ensuring any
    /// token revocation is reflected). Updates the same notification record in-place —
    /// no new record is created.
    ///
    /// On success: notification.Status → Sent, NextRetryAfterUtc cleared.
    /// On failure: notification.Status remains Failed, NextRetryAfterUtc updated if
    ///   further retries are available, or cleared if MaxAttempts is reached.
    /// </summary>
    public async Task RetryNotificationAsync(
        CareConnectNotification notification,
        Referral                referral,
        Provider                provider,
        CancellationToken       ct = default)
    {
        string subject, body, toAddress;

        switch (notification.NotificationType)
        {
            case NotificationType.ReferralCreated:
            case NotificationType.ReferralEmailAutoRetry:
            {
                if (string.IsNullOrWhiteSpace(provider.Email))
                {
                    _logger.LogWarning(
                        "RetryNotificationAsync: provider {ProviderId} has no email. Clearing retry for notification {Id}.",
                        provider.Id, notification.Id);
                    notification.ClearRetrySchedule();
                    await _notifications.UpdateAsync(notification, ct);
                    return;
                }
                var token    = GenerateViewToken(referral.Id, referral.TokenVersion);
                var viewLink = $"{_appBaseUrl}/referrals/view?token={token}";
                subject   = $"New referral received — {referral.ClientFirstName} {referral.ClientLastName}";
                body      = BuildNewReferralEmailHtml(referral, provider, viewLink);
                toAddress = provider.Email;
                break;
            }
            case NotificationType.ReferralAcceptedProvider:
            {
                if (string.IsNullOrWhiteSpace(provider.Email))
                {
                    notification.ClearRetrySchedule();
                    await _notifications.UpdateAsync(notification, ct);
                    return;
                }
                subject   = $"Referral accepted — {referral.ClientFirstName} {referral.ClientLastName}";
                body      = BuildProviderAcceptanceHtml(referral, provider);
                toAddress = provider.Email;
                break;
            }
            case NotificationType.ReferralAcceptedReferrer:
            {
                toAddress = notification.RecipientAddress ?? string.Empty;
                if (string.IsNullOrWhiteSpace(toAddress))
                {
                    notification.ClearRetrySchedule();
                    await _notifications.UpdateAsync(notification, ct);
                    return;
                }
                subject = $"Your referral was accepted — {referral.ClientFirstName} {referral.ClientLastName}";
                body    = BuildReferrerAcceptanceHtml(referral, provider);
                break;
            }
            // LSCC-01-002: client acceptance email retry
            case NotificationType.ReferralAcceptedClient:
            {
                toAddress = notification.RecipientAddress ?? string.Empty;
                if (string.IsNullOrWhiteSpace(toAddress))
                {
                    notification.ClearRetrySchedule();
                    await _notifications.UpdateAsync(notification, ct);
                    return;
                }
                subject = $"Your case has been accepted — {provider.OrganizationName ?? provider.Name}";
                body    = BuildClientAcceptanceHtml(referral, provider);
                break;
            }
            case NotificationType.ReferralRejectedProvider:
            {
                if (string.IsNullOrWhiteSpace(provider.Email))
                {
                    notification.ClearRetrySchedule();
                    await _notifications.UpdateAsync(notification, ct);
                    return;
                }
                subject   = $"Referral declined — {referral.ClientFirstName} {referral.ClientLastName}";
                body      = BuildProviderRejectionHtml(referral, provider);
                toAddress = provider.Email;
                break;
            }
            case NotificationType.ReferralRejectedReferrer:
            {
                toAddress = notification.RecipientAddress ?? string.Empty;
                if (string.IsNullOrWhiteSpace(toAddress))
                {
                    notification.ClearRetrySchedule();
                    await _notifications.UpdateAsync(notification, ct);
                    return;
                }
                subject = $"Your referral was declined — {referral.ClientFirstName} {referral.ClientLastName}";
                body    = BuildReferrerRejectionHtml(referral, provider);
                break;
            }
            case NotificationType.ReferralCancelledProvider:
            {
                if (string.IsNullOrWhiteSpace(provider.Email))
                {
                    notification.ClearRetrySchedule();
                    await _notifications.UpdateAsync(notification, ct);
                    return;
                }
                subject   = $"Referral cancelled — {referral.ClientFirstName} {referral.ClientLastName}";
                body      = BuildProviderCancellationHtml(referral, provider);
                toAddress = provider.Email;
                break;
            }
            case NotificationType.ReferralCancelledReferrer:
            {
                toAddress = notification.RecipientAddress ?? string.Empty;
                if (string.IsNullOrWhiteSpace(toAddress))
                {
                    notification.ClearRetrySchedule();
                    await _notifications.UpdateAsync(notification, ct);
                    return;
                }
                subject = $"Your referral was cancelled — {referral.ClientFirstName} {referral.ClientLastName}";
                body    = BuildReferrerCancellationHtml(referral, provider);
                break;
            }
            case NotificationType.ReferralProviderAssigned:
            {
                if (string.IsNullOrWhiteSpace(provider.Email))
                {
                    notification.ClearRetrySchedule();
                    await _notifications.UpdateAsync(notification, ct);
                    return;
                }
                var token    = GenerateViewToken(referral.Id, referral.TokenVersion);
                var viewLink = $"{_appBaseUrl}/referrals/view?token={token}";
                subject   = $"You have been assigned a referral — {referral.ClientFirstName} {referral.ClientLastName}";
                body      = BuildProviderAssignedEmailHtml(referral, provider, viewLink);
                toAddress = provider.Email;
                break;
            }
            default:
                _logger.LogWarning(
                    "RetryNotificationAsync: unsupported type '{Type}' for notification {Id}. Clearing retry.",
                    notification.NotificationType, notification.Id);
                notification.ClearRetrySchedule();
                await _notifications.UpdateAsync(notification, ct);
                return;
        }

        // The next-retry time is calculated AFTER this attempt succeeds/fails.
        // AttemptCount will be incremented by MarkSent/MarkFailed, so we calculate
        // GetNextRetryAfter for (currentAttemptCount + 1) which is what it will be post-failure.
        var nextRetryAfterUtcOnFailure = ReferralRetryPolicy.GetNextRetryAfter(notification.AttemptCount + 1);

        _logger.LogInformation(
            "RetryNotificationAsync: notification {Id} (type={Type}, attempt={Attempt}/{Max}) for referral {ReferralId}.",
            notification.Id, notification.NotificationType,
            notification.AttemptCount + 1, ReferralRetryPolicy.MaxAttempts, referral.Id);

        await TrySendAndUpdateAsync(notification, toAddress, subject, body, ct, nextRetryAfterUtcOnFailure);
    }

    // ── Internal: submit to Notifications service + update domain status ─────

    private async Task TrySendAndUpdateAsync(
        CareConnectNotification notification,
        string toAddress,
        string subject,
        string body,
        CancellationToken ct,
        DateTime? nextRetryAfterUtcOnFailure = null)
    {
        var eventKey       = NotificationTypeToEventKey(notification.NotificationType);
        var idempotencyKey = notification.DedupeKey ?? notification.Id.ToString();
        var correlationId  = notification.RelatedEntityId.ToString();

        try
        {
            // LS-NOTIF-CORE-023: route through platform Notifications service.
            // Submission success → mark domain record Sent.
            // Actual delivery and per-delivery retry are owned by Notifications service.
            await _producer.SubmitAsync(
                tenantId:      notification.TenantId,
                eventKey:      eventKey,
                toAddress:     toAddress,
                subject:       subject,
                htmlBody:      body,
                idempotencyKey: idempotencyKey,
                correlationId:  correlationId,
                ct:            ct);

            notification.MarkSent();
            await _notifications.UpdateAsync(notification, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Notification submission failed for {NotificationId} (event={EventKey}) to {Recipient}. " +
                "Domain record marked Failed; ReferralEmailRetryWorker will re-submit.",
                notification.Id, eventKey, toAddress);
            // LSCC-005-02: pass nextRetryAfterUtc so the retry worker knows when to re-submit
            notification.MarkFailed(ex.Message, nextRetryAfterUtcOnFailure);
            try { await _notifications.UpdateAsync(notification, ct); }
            catch (Exception saveEx)
            {
                _logger.LogError(saveEx,
                    "Also failed to persist failure state for notification {NotificationId}.",
                    notification.Id);
            }
        }
    }

    /// <summary>
    /// LS-NOTIF-CORE-023: Maps a CareConnect domain NotificationType constant to the
    /// canonical eventKey used in the platform Notifications service producer contract.
    /// </summary>
    private static string NotificationTypeToEventKey(string notificationType) =>
        notificationType switch
        {
            NotificationType.ReferralCreated           => "referral.created",
            NotificationType.ReferralProviderAssigned  => "referral.provider_assigned",
            NotificationType.ReferralEmailResent       => "referral.invite.resent",
            NotificationType.ReferralEmailAutoRetry    => "referral.invite.retry",
            NotificationType.ReferralAcceptedProvider => "referral.accepted.provider",
            NotificationType.ReferralAcceptedReferrer => "referral.accepted.referrer",
            NotificationType.ReferralAcceptedClient   => "referral.accepted.client",
            NotificationType.ReferralRejectedProvider => "referral.declined.provider",
            NotificationType.ReferralRejectedReferrer => "referral.declined.referrer",
            NotificationType.ReferralCancelledProvider => "referral.cancelled.provider",
            NotificationType.ReferralCancelledReferrer => "referral.cancelled.referrer",
            _                                          => "careconnect.notification",
        };

    // ── HTML email templates ──────────────────────────────────────────────────

    private static string BuildNewReferralEmailHtml(Referral r, Provider p, string viewLink)
    {
        var provName = string.IsNullOrWhiteSpace(p.OrganizationName) ? p.Name : p.OrganizationName;
        return $"""
            <!DOCTYPE html>
            <html>
            <body style="font-family:Arial,sans-serif;color:#111;max-width:600px;margin:auto;padding:24px">
              <h2 style="color:#1a56db">New Referral Received</h2>
              <p>Hello{(!string.IsNullOrWhiteSpace(provName) ? $" {provName}" : "")},</p>
              <p>
                A new referral has been sent to you for
                <strong>{r.ClientFirstName} {r.ClientLastName}</strong>
                requesting <strong>{r.RequestedService}</strong>.
              </p>
              <table style="border-collapse:collapse;width:100%;margin:16px 0">
                <tr><td style="padding:6px 0;color:#555;width:140px">Urgency</td><td><strong>{r.Urgency}</strong></td></tr>
                {(r.CaseNumber is not null ? $"<tr><td style='padding:6px 0;color:#555'>Case #</td><td>{r.CaseNumber}</td></tr>" : "")}
                {(r.Notes is not null ? $"<tr><td style='padding:6px 0;color:#555'>Notes</td><td>{r.Notes}</td></tr>" : "")}
              </table>
              <p style="margin-top:24px">
                <a href="{viewLink}"
                   style="background:#1a56db;color:#fff;padding:12px 24px;border-radius:6px;text-decoration:none;font-weight:bold">
                  View Referral
                </a>
              </p>
              <p style="margin-top:32px;font-size:12px;color:#888">
                This link expires in 30 days. If it has expired, please contact the referring party.
              </p>
            </body>
            </html>
            """;
    }

    private static string BuildProviderAssignedEmailHtml(Referral r, Provider p, string viewLink)
    {
        var provName = string.IsNullOrWhiteSpace(p.OrganizationName) ? p.Name : p.OrganizationName;
        return $"""
            <!DOCTYPE html>
            <html>
            <body style="font-family:Arial,sans-serif;color:#111;max-width:600px;margin:auto;padding:24px">
              <h2 style="color:#1a56db">Referral Assigned to You</h2>
              <p>Hello{(!string.IsNullOrWhiteSpace(provName) ? $" {provName}" : "")},</p>
              <p>
                A referral for <strong>{r.ClientFirstName} {r.ClientLastName}</strong>
                requesting <strong>{r.RequestedService}</strong> has been assigned to you.
              </p>
              <table style="border-collapse:collapse;width:100%;margin:16px 0">
                <tr><td style="padding:6px 0;color:#555;width:140px">Urgency</td><td><strong>{r.Urgency}</strong></td></tr>
                {(r.CaseNumber is not null ? $"<tr><td style='padding:6px 0;color:#555'>Case #</td><td>{r.CaseNumber}</td></tr>" : "")}
                {(r.Notes is not null ? $"<tr><td style='padding:6px 0;color:#555'>Notes</td><td>{r.Notes}</td></tr>" : "")}
              </table>
              <p style="margin-top:24px">
                <a href="{viewLink}"
                   style="background:#1a56db;color:#fff;padding:12px 24px;border-radius:6px;text-decoration:none;font-weight:bold">
                  View Referral
                </a>
              </p>
              <p style="margin-top:32px;font-size:12px;color:#888">
                This link expires in 30 days. If it has expired, please contact the referring party.
              </p>
            </body>
            </html>
            """;
    }

    private static string BuildProviderAcceptanceHtml(Referral r, Provider p)
    {
        var provName = string.IsNullOrWhiteSpace(p.OrganizationName) ? p.Name : p.OrganizationName;
        return $"""
            <!DOCTYPE html>
            <html>
            <body style="font-family:Arial,sans-serif;color:#111;max-width:600px;margin:auto;padding:24px">
              <h2 style="color:#057a55">Referral Accepted</h2>
              <p>Hello{(!string.IsNullOrWhiteSpace(provName) ? $" {provName}" : "")},</p>
              <p>
                You have successfully accepted the referral for
                <strong>{r.ClientFirstName} {r.ClientLastName}</strong>
                requesting <strong>{r.RequestedService}</strong>.
              </p>
              <p>
                The referring party has been notified. You can now begin coordinating care
                for this client directly.
              </p>
            </body>
            </html>
            """;
    }

    private static string BuildReferrerAcceptanceHtml(Referral r, Provider p)
    {
        var provName = string.IsNullOrWhiteSpace(p.OrganizationName) ? p.Name : p.OrganizationName;
        var recipientGreeting = r.ReferrerName is { Length: > 0 } n ? $" {n}" : "";
        return $"""
            <!DOCTYPE html>
            <html>
            <body style="font-family:Arial,sans-serif;color:#111;max-width:600px;margin:auto;padding:24px">
              <h2 style="color:#057a55">Your Referral Was Accepted</h2>
              <p>Hello{recipientGreeting},</p>
              <p>
                Your referral for <strong>{r.ClientFirstName} {r.ClientLastName}</strong>
                requesting <strong>{r.RequestedService}</strong> has been accepted by
                <strong>{provName}</strong>.
              </p>
              <p>
                <strong>{provName}</strong> will be in touch with your client to
                continue coordinating care.
              </p>
            </body>
            </html>
            """;
    }

    private static string BuildClientAcceptanceHtml(Referral r, Provider p)
    {
        var provName = string.IsNullOrWhiteSpace(p.OrganizationName) ? p.Name : p.OrganizationName;
        return $"""
            <!DOCTYPE html>
            <html>
            <body style="font-family:Arial,sans-serif;color:#111;max-width:600px;margin:auto;padding:24px">
              <h2 style="color:#057a55">Your Case Has Been Accepted</h2>
              <p>Hello {r.ClientFirstName},</p>
              <p>
                We wanted to let you know that your case for
                <strong>{r.RequestedService}</strong> has been accepted by
                <strong>{provName}</strong>.
              </p>
              <p>
                <strong>{provName}</strong> will be reaching out to you directly to
                discuss next steps for your care.
              </p>
              <p style="margin-top:32px;font-size:12px;color:#888">
                If you have any questions in the meantime, please contact the party
                who referred you.
              </p>
            </body>
            </html>
            """;
    }

    private static string BuildProviderRejectionHtml(Referral r, Provider p)
    {
        var provName = string.IsNullOrWhiteSpace(p.OrganizationName) ? p.Name : p.OrganizationName;
        return $"""
            <!DOCTYPE html>
            <html>
            <body style="font-family:Arial,sans-serif;color:#111;max-width:600px;margin:auto;padding:24px">
              <h2 style="color:#c81e1e">Referral Declined</h2>
              <p>Hello{(!string.IsNullOrWhiteSpace(provName) ? $" {provName}" : "")},</p>
              <p>
                You have declined the referral for
                <strong>{r.ClientFirstName} {r.ClientLastName}</strong>
                requesting <strong>{r.RequestedService}</strong>.
              </p>
              <p>The referring party has been notified of your decision.</p>
            </body>
            </html>
            """;
    }

    private static string BuildReferrerRejectionHtml(Referral r, Provider p)
    {
        var provName = string.IsNullOrWhiteSpace(p.OrganizationName) ? p.Name : p.OrganizationName;
        var recipientGreeting = r.ReferrerName is { Length: > 0 } n ? $" {n}" : "";
        return $"""
            <!DOCTYPE html>
            <html>
            <body style="font-family:Arial,sans-serif;color:#111;max-width:600px;margin:auto;padding:24px">
              <h2 style="color:#c81e1e">Your Referral Was Declined</h2>
              <p>Hello{recipientGreeting},</p>
              <p>
                Your referral for <strong>{r.ClientFirstName} {r.ClientLastName}</strong>
                requesting <strong>{r.RequestedService}</strong> has been declined by
                <strong>{provName}</strong>.
              </p>
              <p>
                You may wish to search for an alternative provider or contact
                <strong>{provName}</strong> for more information.
              </p>
            </body>
            </html>
            """;
    }

    private static string BuildProviderCancellationHtml(Referral r, Provider p)
    {
        var provName = string.IsNullOrWhiteSpace(p.OrganizationName) ? p.Name : p.OrganizationName;
        return $"""
            <!DOCTYPE html>
            <html>
            <body style="font-family:Arial,sans-serif;color:#111;max-width:600px;margin:auto;padding:24px">
              <h2 style="color:#9b1c1c">Referral Cancelled</h2>
              <p>Hello{(!string.IsNullOrWhiteSpace(provName) ? $" {provName}" : "")},</p>
              <p>
                The referral for <strong>{r.ClientFirstName} {r.ClientLastName}</strong>
                requesting <strong>{r.RequestedService}</strong> has been cancelled.
              </p>
              <p>No further action is required on your part.</p>
            </body>
            </html>
            """;
    }

    private static string BuildReferrerCancellationHtml(Referral r, Provider p)
    {
        var provName = string.IsNullOrWhiteSpace(p.OrganizationName) ? p.Name : p.OrganizationName;
        var recipientGreeting = r.ReferrerName is { Length: > 0 } n ? $" {n}" : "";
        return $"""
            <!DOCTYPE html>
            <html>
            <body style="font-family:Arial,sans-serif;color:#111;max-width:600px;margin:auto;padding:24px">
              <h2 style="color:#9b1c1c">Your Referral Was Cancelled</h2>
              <p>Hello{recipientGreeting},</p>
              <p>
                Your referral for <strong>{r.ClientFirstName} {r.ClientLastName}</strong>
                requesting <strong>{r.RequestedService}</strong> to
                <strong>{provName}</strong> has been cancelled.
              </p>
              <p>
                If this was not expected, please contact the involved parties for
                more information.
              </p>
            </body>
            </html>
            """;
    }
}
