using Xenia.Domain.Email;

namespace Xenia.Application.Email.Operations;

/// <summary>
/// Evaluates alert conditions and creates or increments operational alerts.
///
/// Rules:
/// - Tenant-scoped evaluation.
/// - Platform defaults with tenant overrides via IEmailOperationalSettingsService.
/// - No duplicate open alert for the same deduplication key.
/// - Existing open alert occurrence count increments on repeat condition.
/// - Alert auto-resolves when condition clears where appropriate.
/// - Notification adapter request emitted if tenant has notifications enabled.
/// - Notification failure does not lose the alert.
/// - Safe descriptions only — no raw cursors, credentials, or message bodies.
/// </summary>
public interface IAlertRuleEngine
{
    /// <summary>
    /// Evaluates source failure conditions and emits alerts as appropriate.
    /// Called after each ingestion run failure.
    /// </summary>
    Task EvaluateSourceFailureAsync(
        Guid tenantId,
        Guid emailSourceId,
        int consecutiveFailureCount,
        string safeErrorCategory,
        string? correlationId,
        CancellationToken ct = default);

    /// <summary>
    /// Evaluates whether a source has stalled (no successful sync within threshold).
    /// </summary>
    Task EvaluateSyncStalledAsync(
        Guid tenantId,
        Guid emailSourceId,
        DateTime lastSuccessfulSync,
        string? correlationId,
        CancellationToken ct = default);

    /// <summary>Emits a lock stale alert when a lock is held beyond the warning threshold.</summary>
    Task EvaluateLockStaleAsync(
        Guid tenantId,
        Guid emailSourceId,
        DateTime lockHeldSince,
        string safeOwnerId,
        string? correlationId,
        CancellationToken ct = default);

    /// <summary>Emits a cursor invalidated alert.</summary>
    Task EvaluateCursorInvalidatedAsync(
        Guid tenantId,
        Guid emailSourceId,
        string? correlationId,
        CancellationToken ct = default);

    /// <summary>Emits an attachment dispatch failure alert.</summary>
    Task EvaluateAttachmentFailureAsync(
        Guid tenantId,
        Guid emailSourceId,
        int attachmentFailureCount,
        string? correlationId,
        CancellationToken ct = default);

    /// <summary>Emits an alert when the audit adapter is unavailable.</summary>
    Task EvaluateAuditUnavailableAsync(
        Guid tenantId,
        string? correlationId,
        CancellationToken ct = default);

    /// <summary>Emits an alert when the documents adapter is unavailable.</summary>
    Task EvaluateDocumentsUnavailableAsync(
        Guid tenantId,
        string? correlationId,
        CancellationToken ct = default);
}
