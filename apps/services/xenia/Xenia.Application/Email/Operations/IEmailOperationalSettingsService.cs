using Xenia.Domain.Email;

namespace Xenia.Application.Email.Operations;

/// <summary>
/// Manages tenant-scoped operational settings for the Xenia Email module.
///
/// Creates settings with platform defaults on first access.
/// Concurrency protected via optimistic versioning.
/// </summary>
public interface IEmailOperationalSettingsService
{
    /// <summary>Returns existing settings or creates with defaults on first access.</summary>
    Task<EmailOperationalSettings> GetOrCreateAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>Updates settings. Throws if version conflicts (optimistic concurrency).</summary>
    Task<EmailOperationalSettings> UpdateAsync(
        Guid tenantId,
        UpdateOperationalSettingsRequest request,
        string? updatedBy,
        CancellationToken ct = default);
}

public sealed record UpdateOperationalSettingsRequest(
    int DefaultDashboardRangeDays,
    int SourceFailureAlertThreshold,
    int StaleSyncThresholdMinutes,
    int LockWarningThresholdMinutes,
    int MaximumRetryCount,
    int CancellationTimeoutSeconds,
    bool MetricsEnabled,
    bool NotificationAlertsEnabled,
    int DefaultRunPageSize,
    int DefaultMessagePageSize,
    int OperationalPollingIntervalSeconds,
    int MessageMetadataRetentionDays,
    int MessageBodyRetentionDays,
    int ValidationHistoryRetentionDays,
    int IngestionRunRetentionDays,
    int AlertRetentionDays,
    int AttachmentReferenceRetentionDays,
    int PurgeBatchSize,
    bool RetentionDryRunDefault,
    bool LegalHoldEnabled,
    bool RetentionEnabled,
    int ExpectedVersion);
