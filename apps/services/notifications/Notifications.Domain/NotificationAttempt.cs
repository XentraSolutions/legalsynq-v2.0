namespace Notifications.Domain;

public class NotificationAttempt
{
    public Guid Id { get; set; }
    public Guid? TenantId { get; set; }
    public Guid NotificationId { get; set; }
    public string Channel { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
    public int AttemptNumber { get; set; } = 1;
    public string? ProviderMessageId { get; set; }
    public string? ProviderOwnershipMode { get; set; }
    public Guid? ProviderConfigId { get; set; }
    public string? FailureCategory { get; set; }
    public string? ErrorMessage { get; set; }
    public bool IsFailover { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // ── LS-NOTIF-SMS-007: Persisted reconciliation tracking ───────────────────
    // Updated by SmsReconciliationService after each pull-based reconciliation
    // attempt. Never contains credentials, raw provider payloads, or phone numbers.
    // Webhook-driven delivery status updates (via DeliveryStatusService) remain
    // authoritative and are NOT affected by these tracking fields.

    /// <summary>
    /// Outcome of the most recent reconciliation attempt.
    /// Values match SmsReconciliationResult.Outcome constants.
    /// Null if the attempt has never been reconciled.
    /// </summary>
    public string? LastReconciliationOutcome { get; set; }

    /// <summary>UTC timestamp of the most recent reconciliation attempt.</summary>
    public DateTime? LastReconciledAt { get; set; }

    /// <summary>Error code from the most recent reconciliation, if applicable.</summary>
    public string? LastReconciliationErrorCode { get; set; }

    /// <summary>
    /// Raw provider status string returned by the vendor during the most recent
    /// successful vendor lookup (e.g. "delivered", "failed").
    /// Safe operational metadata — not a credential or raw payload.
    /// </summary>
    public string? LastReconciliationProviderStatus { get; set; }

    /// <summary>Normalized status from the most recent successful vendor lookup.</summary>
    public string? LastReconciliationNormalizedStatus { get; set; }

    /// <summary>
    /// Total number of times this attempt has been reconciled (manual or batch).
    /// Defaults to 0 for all pre-existing attempts.
    /// </summary>
    public int ReconciliationAttemptCount { get; set; } = 0;
}
