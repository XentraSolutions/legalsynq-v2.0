using BuildingBlocks.Domain;

namespace CareConnect.Domain;

public class CareConnectNotification : AuditableEntity
{
    public Guid    Id                  { get; private set; }
    public Guid    TenantId            { get; private set; }
    public string  NotificationType    { get; private set; } = string.Empty;
    public string  RelatedEntityType   { get; private set; } = string.Empty;
    public Guid    RelatedEntityId     { get; private set; }
    public string  RecipientType       { get; private set; } = string.Empty;
    public string? RecipientAddress    { get; private set; }
    public string? Subject             { get; private set; }
    public string? Message             { get; private set; }
    public string  Status              { get; private set; } = NotificationStatus.Pending;
    public DateTime? ScheduledForUtc  { get; private set; }
    public DateTime? SentAtUtc        { get; private set; }
    public DateTime? FailedAtUtc      { get; private set; }
    public string? FailureReason      { get; private set; }

    // LSCC-005-01: delivery tracking fields
    public int       AttemptCount      { get; private set; }
    public DateTime? LastAttemptAtUtc  { get; private set; }

    private CareConnectNotification() { }

    /// <summary>
    /// Marks the notification as successfully sent.
    /// Increments AttemptCount and records LastAttemptAtUtc.
    /// </summary>
    public void MarkSent()
    {
        AttemptCount     += 1;
        LastAttemptAtUtc  = DateTime.UtcNow;
        Status            = NotificationStatus.Sent;
        SentAtUtc         = DateTime.UtcNow;
        UpdatedAtUtc      = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks the notification as failed, storing the failure reason.
    /// Increments AttemptCount and records LastAttemptAtUtc.
    /// </summary>
    public void MarkFailed(string reason)
    {
        AttemptCount     += 1;
        LastAttemptAtUtc  = DateTime.UtcNow;
        Status            = NotificationStatus.Failed;
        FailedAtUtc       = DateTime.UtcNow;
        FailureReason     = reason?.Length > 2000 ? reason[..2000] : reason;
        UpdatedAtUtc      = DateTime.UtcNow;
    }

    public static CareConnectNotification Create(
        Guid    tenantId,
        string  notificationType,
        string  relatedEntityType,
        Guid    relatedEntityId,
        string  recipientType,
        string? recipientAddress,
        string? subject,
        string? message,
        DateTime? scheduledForUtc,
        Guid?   createdByUserId)
    {
        return new CareConnectNotification
        {
            Id                = Guid.NewGuid(),
            TenantId          = tenantId,
            NotificationType  = notificationType,
            RelatedEntityType = relatedEntityType,
            RelatedEntityId   = relatedEntityId,
            RecipientType     = recipientType,
            RecipientAddress  = recipientAddress,
            Subject           = subject,
            Message           = message,
            Status            = NotificationStatus.Pending,
            ScheduledForUtc   = scheduledForUtc,
            AttemptCount      = 0,
            LastAttemptAtUtc  = null,
            CreatedByUserId   = createdByUserId,
            UpdatedByUserId   = createdByUserId,
            CreatedAtUtc      = DateTime.UtcNow,
            UpdatedAtUtc      = DateTime.UtcNow
        };
    }
}
