using CareConnect.Application.DTOs;
using CareConnect.Domain;

namespace CareConnect.Application.Interfaces;

/// <summary>
/// LSCC-005 / LSCC-005-01: Handles secure token generation and email notification dispatch
/// for the referral flow.
///
/// Token strategy (LSCC-005-01): HMAC-SHA256 signed token encoding referralId + tokenVersion + expiry (30 days).
/// Token format: {referralId}:{tokenVersion}:{expiryUnixSeconds}:{hmacHex}, Base64url-encoded.
/// Incrementing a referral's TokenVersion invalidates all previously issued tokens.
///
/// Email strategy: notification records are always created in the DB (Pending state).
/// An SMTP send is then attempted immediately. On success → Sent; on failure → Failed.
/// AttemptCount and LastAttemptAtUtc are updated on each attempt. Never a silent failure.
/// </summary>
public interface IReferralEmailService
{
    /// <summary>
    /// LSCC-005-01: Generates a time-bound HMAC-signed view token for the given referral.
    /// The token embeds referralId, tokenVersion, and an expiry timestamp (30 days).
    /// Use the referral's current TokenVersion so token revocation works correctly.
    /// </summary>
    string GenerateViewToken(Guid referralId, int tokenVersion);

    /// <summary>
    /// LSCC-005-01: Validates a view token. Returns a ViewTokenValidationResult containing
    /// both the ReferralId and the TokenVersion embedded in the token on success.
    /// Returns null if the token is invalid, tampered with, or expired.
    ///
    /// IMPORTANT: The caller must also verify that result.TokenVersion matches the referral's
    /// current TokenVersion to detect revoked tokens.
    /// </summary>
    ViewTokenValidationResult? ValidateViewToken(string token);

    /// <summary>
    /// Queues a "new referral received" notification to the provider's email address
    /// and attempts an immediate SMTP send.
    /// Called after referral creation (fire-and-observe pattern — never gates creation).
    /// </summary>
    Task SendNewReferralNotificationAsync(
        Referral referral,
        Provider provider,
        CancellationToken ct = default);

    /// <summary>
    /// LSCC-005-01: Queues a resend of the "new referral received" notification.
    /// Creates a new notification record (type: ReferralEmailResent) and attempts SMTP send.
    /// The new token uses the referral's current TokenVersion, so revoked tokens stay revoked.
    /// </summary>
    Task ResendNewReferralNotificationAsync(
        Referral referral,
        Provider provider,
        CancellationToken ct = default);

    /// <summary>
    /// Queues acceptance confirmation notifications for both the provider and the referrer,
    /// and attempts immediate SMTP sends.
    /// Called after the referral is accepted (fire-and-observe — never gates acceptance).
    /// </summary>
    Task SendAcceptanceConfirmationsAsync(
        Referral referral,
        Provider provider,
        CancellationToken ct = default);

    /// <summary>
    /// LSCC-005-02: Re-attempts an email send for an existing failed notification record.
    /// Called exclusively by <c>ReferralEmailRetryWorker</c> — not for manual operator resend.
    /// Rebuilds the email body from current referral/provider state. Updates the same
    /// notification record in-place; no new record is created.
    /// </summary>
    Task RetryNotificationAsync(
        CareConnectNotification notification,
        Referral                referral,
        Provider                provider,
        CancellationToken       ct = default);
}
