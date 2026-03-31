using CareConnect.Application.DTOs;
using CareConnect.Domain;

namespace CareConnect.Application.Interfaces;

/// <summary>
/// LSCC-005: Handles secure token generation and email notification dispatch for the referral flow.
///
/// Token strategy: HMAC-SHA256 signed token encoding referralId + expiry (30 days).
/// No extra DB table required — validated purely with the shared secret.
///
/// Email strategy: notification records are always created in the DB (Pending state).
/// An SMTP send is then attempted immediately. If SMTP is not configured or fails,
/// the record remains Pending for future retry — never a silent failure.
/// </summary>
public interface IReferralEmailService
{
    /// <summary>
    /// Generates a time-bound HMAC-signed view token for the given referral.
    /// The token encodes the referral ID and an expiry timestamp.
    /// </summary>
    string GenerateViewToken(Guid referralId);

    /// <summary>
    /// Validates a view token. Returns the embedded referral ID on success,
    /// or null if the token is invalid, tampered with, or expired.
    /// </summary>
    Guid? ValidateViewToken(string token);

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
    /// Queues acceptance confirmation notifications for both the provider and the referrer,
    /// and attempts immediate SMTP sends.
    /// Called after the referral is accepted (fire-and-observe — never gates acceptance).
    /// </summary>
    Task SendAcceptanceConfirmationsAsync(
        Referral referral,
        Provider provider,
        CancellationToken ct = default);
}
