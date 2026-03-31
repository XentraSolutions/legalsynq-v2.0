using System.Security.Cryptography;
using System.Text;
using CareConnect.Application.Interfaces;
using CareConnect.Application.Repositories;
using CareConnect.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CareConnect.Application.Services;

/// <summary>
/// LSCC-005: Implements secure token generation and email notification dispatch
/// for the referral flow.
///
/// Token format (URL-safe Base64):
///   {referralId}:{expiryUnixSeconds}:{hmacHex}
///
/// The HMAC-SHA256 covers "{referralId}:{expiryUnixSeconds}" using a secret key
/// from configuration key "ReferralToken:Secret". Falls back to a development
/// constant when the key is absent (NOT suitable for production).
///
/// Email strategy: a CareConnectNotification DB record is always written first
/// (Pending status). An SMTP send is then attempted; success → Sent, failure →
/// the record remains Pending. Failure is logged at Warning level — never silent.
/// </summary>
public class ReferralEmailService : IReferralEmailService
{
    private const int TokenExpiryDays      = 30;
    private const string DevFallbackSecret = "LEGALSYNQ-DEV-REFERRAL-TOKEN-SECRET-2026";

    private readonly INotificationRepository _notifications;
    private readonly ISmtpEmailSender        _smtp;
    private readonly ILogger<ReferralEmailService> _logger;
    private readonly string _tokenSecret;
    private readonly string _appBaseUrl;

    public ReferralEmailService(
        INotificationRepository notifications,
        ISmtpEmailSender smtp,
        IConfiguration configuration,
        ILogger<ReferralEmailService> logger)
    {
        _notifications = notifications;
        _smtp          = smtp;
        _logger        = logger;
        _tokenSecret   = configuration["ReferralToken:Secret"] ?? DevFallbackSecret;
        _appBaseUrl    = (configuration["AppBaseUrl"] ?? "http://localhost:3000").TrimEnd('/');

        if (configuration["ReferralToken:Secret"] is null)
            _logger.LogWarning(
                "ReferralToken:Secret is not configured. Using development fallback — " +
                "DO NOT use this in production.");
    }

    // ── Token helpers ─────────────────────────────────────────────────────────

    public string GenerateViewToken(Guid referralId)
    {
        var expiry  = DateTimeOffset.UtcNow.AddDays(TokenExpiryDays).ToUnixTimeSeconds();
        var payload = $"{referralId}:{expiry}";
        var sig     = ComputeHmac(payload);
        var raw     = $"{payload}:{sig}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(raw))
                      .TrimEnd('=')
                      .Replace('+', '-')
                      .Replace('/', '_');
    }

    public Guid? ValidateViewToken(string token)
    {
        try
        {
            var padded  = token.Replace('-', '+').Replace('_', '/');
            var mod     = padded.Length % 4;
            if (mod != 0) padded += new string('=', 4 - mod);
            var raw     = Encoding.UTF8.GetString(Convert.FromBase64String(padded));
            var parts   = raw.Split(':');
            if (parts.Length != 3) return null;

            var referralId = Guid.Parse(parts[0]);
            var expiry     = long.Parse(parts[1]);
            var sig        = parts[2];

            if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expiry)
            {
                _logger.LogInformation("Referral view token expired for referral {ReferralId}", referralId);
                return null;
            }

            var expectedSig = ComputeHmac($"{referralId}:{expiry}");
            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(sig),
                    Encoding.UTF8.GetBytes(expectedSig)))
            {
                _logger.LogWarning("Referral view token HMAC mismatch — possible tampering.");
                return null;
            }

            return referralId;
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

        var token     = GenerateViewToken(referral.Id);
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
            createdByUserId:   referral.CreatedByUserId);

        await _notifications.AddAsync(notification, ct);

        await TrySendAndUpdateAsync(notification, provider.Email, subject, body, ct);
    }

    public async Task SendAcceptanceConfirmationsAsync(
        Referral referral,
        Provider provider,
        CancellationToken ct = default)
    {
        var tasks = new List<Task>();

        // 1. Confirmation to the provider
        if (!string.IsNullOrWhiteSpace(provider.Email))
        {
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
                createdByUserId:   null);

            await _notifications.AddAsync(provNotif, ct);
            tasks.Add(TrySendAndUpdateAsync(provNotif, provider.Email, provSubject, provBody, ct));
        }
        else
        {
            _logger.LogWarning(
                "Skipping provider acceptance email: provider {ProviderId} has no email address.",
                provider.Id);
        }

        // 2. Confirmation to the referrer (if email was captured at creation)
        if (!string.IsNullOrWhiteSpace(referral.ReferrerEmail))
        {
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
                createdByUserId:   null);

            await _notifications.AddAsync(refNotif, ct);
            tasks.Add(TrySendAndUpdateAsync(refNotif, referral.ReferrerEmail, refSubject, refBody, ct));
        }
        else
        {
            _logger.LogWarning(
                "Skipping referrer acceptance email for referral {ReferralId}: no ReferrerEmail stored.",
                referral.Id);
        }

        await Task.WhenAll(tasks);
    }

    // ── Internal: send + update notification status ───────────────────────────

    private async Task TrySendAndUpdateAsync(
        CareConnectNotification notification,
        string toAddress,
        string subject,
        string body,
        CancellationToken ct)
    {
        try
        {
            await _smtp.SendAsync(toAddress, subject, body, ct);
            notification.MarkSent();
            await _notifications.UpdateAsync(notification, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to send email notification {NotificationId} to {Recipient}. " +
                "Record remains Pending for retry.",
                notification.Id, toAddress);
            notification.MarkFailed(ex.Message);
            try { await _notifications.UpdateAsync(notification, ct); }
            catch (Exception saveEx)
            {
                _logger.LogError(saveEx,
                    "Also failed to persist failure state for notification {NotificationId}.",
                    notification.Id);
            }
        }
    }

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
              <p>The referring party has been notified. The next step is to schedule an appointment.</p>
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
              <p>They will be in touch with your client to schedule an appointment.</p>
            </body>
            </html>
            """;
    }
}
