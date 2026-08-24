namespace Xenia.Domain.Email;

/// <summary>
/// Represents a tenant-scoped operational alert raised by the Xenia alert rule engine.
///
/// Deduplication: the engine will not open a second alert with the same DeduplicationKey while
/// one with status Open already exists — instead it increments OccurrenceCount.
///
/// Security:
/// - SafeDescription must never contain message bodies, raw headers, credentials, or raw cursors.
/// - TenantId is always populated — alerts are never cross-tenant.
/// </summary>
public sealed class EmailOperationalAlert
{
    public const int TitleMaxLength           = 200;
    public const int SafeDescriptionMaxLength = 1000;
    public const int DeduplicationKeyMaxLength= 300;
    public const int ResolutionReasonMaxLength= 500;
    public const int CorrelationIdMaxLength   = 200;

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }

    /// <summary>The email source this alert is related to (nullable — some alerts are provider-wide).</summary>
    public Guid? EmailSourceId { get; private set; }

    /// <summary>The provider type this alert is related to (nullable).</summary>
    public EmailProviderType? ProviderType { get; private set; }

    public EmailAlertType AlertType { get; private set; }
    public EmailAlertSeverity Severity { get; private set; }
    public EmailAlertStatus Status { get; private set; }

    /// <summary>
    /// Deduplication key: alerts with the same key and Open status will not create a new row.
    /// Format: "{AlertType}:{TenantId}[:{SourceId}]"
    /// </summary>
    public string DeduplicationKey { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;
    public string SafeDescription { get; private set; } = string.Empty;

    public DateTime FirstObservedAt { get; private set; }
    public DateTime LastObservedAt { get; private set; }
    public int OccurrenceCount { get; private set; }

    public DateTime? AcknowledgedAt { get; private set; }
    public Guid? AcknowledgedBy { get; private set; }

    public DateTime? ResolvedAt { get; private set; }
    public Guid? ResolvedBy { get; private set; }
    public string? ResolutionReason { get; private set; }

    /// <summary>If set, the alert is suppressed until this UTC time.</summary>
    public DateTime? SuppressedUntil { get; private set; }

    public string? CorrelationId { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    /// <summary>Optimistic concurrency version.</summary>
    public int Version { get; private set; }

    private EmailOperationalAlert() { }

    public static EmailOperationalAlert Create(
        Guid tenantId,
        EmailAlertType alertType,
        EmailAlertSeverity severity,
        string title,
        string safeDescription,
        string deduplicationKey,
        Guid? emailSourceId = null,
        EmailProviderType? providerType = null,
        string? correlationId = null)
    {
        var now = DateTime.UtcNow;
        return new EmailOperationalAlert
        {
            Id               = Guid.CreateVersion7(),
            TenantId         = tenantId,
            EmailSourceId    = emailSourceId,
            ProviderType     = providerType,
            AlertType        = alertType,
            Severity         = severity,
            Status           = EmailAlertStatus.Open,
            DeduplicationKey = deduplicationKey,
            Title            = title,
            SafeDescription  = safeDescription,
            FirstObservedAt  = now,
            LastObservedAt   = now,
            OccurrenceCount  = 1,
            CorrelationId    = correlationId,
            CreatedAt        = now,
            UpdatedAt        = now,
            Version          = 1,
        };
    }

    /// <summary>Increments occurrence count on a repeated condition (same deduplication key).</summary>
    public void IncrementOccurrence(string safeDescription, string? correlationId = null)
    {
        LastObservedAt  = DateTime.UtcNow;
        OccurrenceCount++;
        SafeDescription = safeDescription;
        if (correlationId is not null) CorrelationId = correlationId;
        UpdatedAt = DateTime.UtcNow;
        Version++;
    }

    /// <summary>Transitions the alert to Acknowledged.</summary>
    public void Acknowledge(Guid actorId)
    {
        if (Status == EmailAlertStatus.Resolved) return;
        Status         = EmailAlertStatus.Acknowledged;
        AcknowledgedAt = DateTime.UtcNow;
        AcknowledgedBy = actorId;
        UpdatedAt      = DateTime.UtcNow;
        Version++;
    }

    /// <summary>Transitions the alert to Resolved.</summary>
    public void Resolve(Guid actorId, string? reason = null)
    {
        Status           = EmailAlertStatus.Resolved;
        ResolvedAt       = DateTime.UtcNow;
        ResolvedBy       = actorId;
        ResolutionReason = reason;
        UpdatedAt        = DateTime.UtcNow;
        Version++;
    }

    /// <summary>Auto-resolves the alert (no actor — system-triggered).</summary>
    public void AutoResolve(string reason)
    {
        Status           = EmailAlertStatus.Resolved;
        ResolvedAt       = DateTime.UtcNow;
        ResolutionReason = reason;
        UpdatedAt        = DateTime.UtcNow;
        Version++;
    }

    /// <summary>Suppresses the alert until the given UTC time.</summary>
    public void Suppress(DateTime suppressedUntil, Guid actorId)
    {
        Status         = EmailAlertStatus.Suppressed;
        SuppressedUntil= suppressedUntil;
        AcknowledgedAt = DateTime.UtcNow;
        AcknowledgedBy = actorId;
        UpdatedAt      = DateTime.UtcNow;
        Version++;
    }

    /// <summary>Whether this alert is currently suppressed.</summary>
    public bool IsSuppressedNow => SuppressedUntil.HasValue && SuppressedUntil.Value > DateTime.UtcNow;
}
