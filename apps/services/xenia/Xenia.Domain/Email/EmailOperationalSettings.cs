namespace Xenia.Domain.Email;

/// <summary>
/// Tenant-scoped operational settings for the Xenia Email module.
///
/// One row per tenant. Created with platform defaults on first access.
///
/// Security:
/// - Never stores credentials, provider tokens, raw cursors, or connection strings.
/// - Version field provides optimistic concurrency protection.
/// </summary>
public sealed class EmailOperationalSettings
{
    public const int UpdatedByMaxLength = 200;

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }

    // ── Dashboard ─────────────────────────────────────────────────────────────
    public int DefaultDashboardRangeDays { get; private set; } = 7;

    // ── Alert thresholds ──────────────────────────────────────────────────────
    public int SourceFailureAlertThreshold { get; private set; } = 3;
    public int StaleSyncThresholdMinutes { get; private set; } = 120;
    public int LockWarningThresholdMinutes { get; private set; } = 30;

    // ── Retry / cancellation ──────────────────────────────────────────────────
    public int MaximumRetryCount { get; private set; } = 5;
    public int CancellationTimeoutSeconds { get; private set; } = 60;

    // ── Feature flags ─────────────────────────────────────────────────────────
    public bool MetricsEnabled { get; private set; } = true;
    public bool NotificationAlertsEnabled { get; private set; } = false;

    // ── Pagination defaults ───────────────────────────────────────────────────
    public int DefaultRunPageSize { get; private set; } = 50;
    public int DefaultMessagePageSize { get; private set; } = 50;

    // ── Polling ───────────────────────────────────────────────────────────────
    public int OperationalPollingIntervalSeconds { get; private set; } = 30;

    // ── Retention ─────────────────────────────────────────────────────────────
    public int MessageMetadataRetentionDays { get; private set; } = 365;
    public int MessageBodyRetentionDays { get; private set; } = 90;
    public int ValidationHistoryRetentionDays { get; private set; } = 90;
    public int IngestionRunRetentionDays { get; private set; } = 180;
    public int AlertRetentionDays { get; private set; } = 90;
    public int AttachmentReferenceRetentionDays { get; private set; } = 365;
    public int PurgeBatchSize { get; private set; } = 500;
    public bool RetentionDryRunDefault { get; private set; } = true;
    public bool LegalHoldEnabled { get; private set; } = false;
    public bool RetentionEnabled { get; private set; } = false;

    // ── Audit ─────────────────────────────────────────────────────────────────
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public string? UpdatedBy { get; private set; }

    /// <summary>Optimistic concurrency version.</summary>
    public int Version { get; private set; }

    private EmailOperationalSettings() { }

    /// <summary>Creates settings for a tenant with platform defaults.</summary>
    public static EmailOperationalSettings CreateDefaults(Guid tenantId)
    {
        var now = DateTime.UtcNow;
        return new EmailOperationalSettings
        {
            Id        = Guid.CreateVersion7(),
            TenantId  = tenantId,
            CreatedAt = now,
            UpdatedAt = now,
            Version   = 1,
        };
    }

    public void Update(
        int defaultDashboardRangeDays,
        int sourceFailureAlertThreshold,
        int staleSyncThresholdMinutes,
        int lockWarningThresholdMinutes,
        int maximumRetryCount,
        int cancellationTimeoutSeconds,
        bool metricsEnabled,
        bool notificationAlertsEnabled,
        int defaultRunPageSize,
        int defaultMessagePageSize,
        int operationalPollingIntervalSeconds,
        int messageMetadataRetentionDays,
        int messageBodyRetentionDays,
        int validationHistoryRetentionDays,
        int ingestionRunRetentionDays,
        int alertRetentionDays,
        int attachmentReferenceRetentionDays,
        int purgeBatchSize,
        bool retentionDryRunDefault,
        bool legalHoldEnabled,
        bool retentionEnabled,
        string? updatedBy)
    {
        DefaultDashboardRangeDays          = defaultDashboardRangeDays;
        SourceFailureAlertThreshold        = sourceFailureAlertThreshold;
        StaleSyncThresholdMinutes          = staleSyncThresholdMinutes;
        LockWarningThresholdMinutes        = lockWarningThresholdMinutes;
        MaximumRetryCount                  = maximumRetryCount;
        CancellationTimeoutSeconds         = cancellationTimeoutSeconds;
        MetricsEnabled                     = metricsEnabled;
        NotificationAlertsEnabled          = notificationAlertsEnabled;
        DefaultRunPageSize                 = defaultRunPageSize;
        DefaultMessagePageSize             = defaultMessagePageSize;
        OperationalPollingIntervalSeconds  = operationalPollingIntervalSeconds;
        MessageMetadataRetentionDays       = messageMetadataRetentionDays;
        MessageBodyRetentionDays           = messageBodyRetentionDays;
        ValidationHistoryRetentionDays     = validationHistoryRetentionDays;
        IngestionRunRetentionDays          = ingestionRunRetentionDays;
        AlertRetentionDays                 = alertRetentionDays;
        AttachmentReferenceRetentionDays   = attachmentReferenceRetentionDays;
        PurgeBatchSize                     = purgeBatchSize;
        RetentionDryRunDefault             = retentionDryRunDefault;
        LegalHoldEnabled                   = legalHoldEnabled;
        RetentionEnabled                   = retentionEnabled;
        UpdatedBy                          = updatedBy;
        UpdatedAt                          = DateTime.UtcNow;
        Version++;
    }
}
