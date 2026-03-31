namespace CareConnect.Application.DTOs;

/// <summary>
/// LSCC-005-01: Lightweight DTO representing a referral-related notification record.
/// Used to surface email delivery status and history on the referral detail view.
/// </summary>
public class ReferralNotificationResponse
{
    public Guid     Id               { get; set; }
    public string   NotificationType { get; set; } = string.Empty;
    public string   RecipientType    { get; set; } = string.Empty;
    public string?  RecipientAddress { get; set; }
    public string   Status           { get; set; } = string.Empty;
    public int      AttemptCount     { get; set; }
    public string?  FailureReason    { get; set; }
    public DateTime? SentAtUtc       { get; set; }
    public DateTime? FailedAtUtc     { get; set; }
    public DateTime? LastAttemptAtUtc { get; set; }
    public DateTime  CreatedAtUtc    { get; set; }
}
