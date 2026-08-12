using Microsoft.Extensions.Logging;
using Xenia.Application.Email.Operations;
using Xenia.Domain.Email;

namespace Xenia.Infrastructure.Email;

/// <summary>
/// Default alert rule engine implementation.
/// Evaluates operational conditions and opens or increments alerts via IAlertService.
/// Notification adapter requests are deferred — no direct email/SMS delivery.
/// </summary>
internal sealed class DefaultAlertRuleEngine : IAlertRuleEngine
{
    private readonly IAlertService _alertService;
    private readonly IEmailOperationalSettingsService _settingsService;
    private readonly ILogger<DefaultAlertRuleEngine> _logger;

    private const int DefaultSourceFailureThreshold   = 3;
    private const int DefaultStaleSyncThresholdMinutes = 120;
    private const int DefaultLockWarningMinutes        = 30;
    private const int DefaultAttachmentFailureThreshold= 5;

    public DefaultAlertRuleEngine(
        IAlertService alertService,
        IEmailOperationalSettingsService settingsService,
        ILogger<DefaultAlertRuleEngine> logger)
    {
        _alertService   = alertService;
        _settingsService= settingsService;
        _logger         = logger;
    }

    public async Task EvaluateSourceFailureAsync(
        Guid tenantId, Guid emailSourceId, int consecutiveFailureCount,
        string safeErrorCategory, string? correlationId, CancellationToken ct = default)
    {
        var settings  = await _settingsService.GetOrCreateAsync(tenantId, ct);
        var threshold = settings.SourceFailureAlertThreshold;

        if (consecutiveFailureCount < threshold) return;

        await _alertService.OpenOrIncrementAsync(
            tenantId,
            EmailAlertType.SourceRepeatedFailure,
            EmailAlertSeverity.Warning,
            title: "Email source repeated failures",
            safeDescription: $"Source has {consecutiveFailureCount} consecutive failures. Category: {safeErrorCategory}",
            emailSourceId: emailSourceId,
            correlationId: correlationId,
            ct: ct);
    }

    public async Task EvaluateSyncStalledAsync(
        Guid tenantId, Guid emailSourceId, DateTime lastSuccessfulSync,
        string? correlationId, CancellationToken ct = default)
    {
        var settings  = await _settingsService.GetOrCreateAsync(tenantId, ct);
        var threshold = settings.StaleSyncThresholdMinutes;
        var stallMins = (DateTime.UtcNow - lastSuccessfulSync).TotalMinutes;

        if (stallMins < threshold) return;

        await _alertService.OpenOrIncrementAsync(
            tenantId,
            EmailAlertType.SyncStalled,
            EmailAlertSeverity.Warning,
            title: "Email sync stalled",
            safeDescription: $"No successful sync in {(int)stallMins} minutes (threshold: {threshold}m)",
            emailSourceId: emailSourceId,
            correlationId: correlationId,
            ct: ct);
    }

    public async Task EvaluateLockStaleAsync(
        Guid tenantId, Guid emailSourceId, DateTime lockHeldSince,
        string safeOwnerId, string? correlationId, CancellationToken ct = default)
    {
        var settings     = await _settingsService.GetOrCreateAsync(tenantId, ct);
        var threshold    = settings.LockWarningThresholdMinutes;
        var lockAgeMinutes = (DateTime.UtcNow - lockHeldSince).TotalMinutes;

        if (lockAgeMinutes < threshold) return;

        await _alertService.OpenOrIncrementAsync(
            tenantId,
            EmailAlertType.LockStale,
            EmailAlertSeverity.Warning,
            title: "Email source sync lock held for extended duration",
            safeDescription: $"Lock held for {(int)lockAgeMinutes}m by {safeOwnerId} (threshold: {threshold}m)",
            emailSourceId: emailSourceId,
            correlationId: correlationId,
            ct: ct);
    }

    public async Task EvaluateCursorInvalidatedAsync(
        Guid tenantId, Guid emailSourceId, string? correlationId, CancellationToken ct = default)
    {
        await _alertService.OpenOrIncrementAsync(
            tenantId,
            EmailAlertType.CursorInvalidated,
            EmailAlertSeverity.Informational,
            title: "Email sync cursor invalidated",
            safeDescription: "Provider cursor was invalidated. A full re-sync will be required.",
            emailSourceId: emailSourceId,
            correlationId: correlationId,
            ct: ct);
    }

    public async Task EvaluateAttachmentFailureAsync(
        Guid tenantId, Guid emailSourceId, int attachmentFailureCount,
        string? correlationId, CancellationToken ct = default)
    {
        if (attachmentFailureCount < DefaultAttachmentFailureThreshold) return;

        await _alertService.OpenOrIncrementAsync(
            tenantId,
            EmailAlertType.AttachmentDispatchFailure,
            EmailAlertSeverity.Warning,
            title: "Attachment dispatch failures detected",
            safeDescription: $"Source has {attachmentFailureCount} attachment dispatch failures",
            emailSourceId: emailSourceId,
            correlationId: correlationId,
            ct: ct);
    }

    public async Task EvaluateAuditUnavailableAsync(
        Guid tenantId, string? correlationId, CancellationToken ct = default)
    {
        await _alertService.OpenOrIncrementAsync(
            tenantId,
            EmailAlertType.AuditUnavailable,
            EmailAlertSeverity.Warning,
            title: "Audit adapter unavailable",
            safeDescription: "Xenia cannot record audit events. Operating in degraded mode.",
            correlationId: correlationId,
            ct: ct);
    }

    public async Task EvaluateDocumentsUnavailableAsync(
        Guid tenantId, string? correlationId, CancellationToken ct = default)
    {
        await _alertService.OpenOrIncrementAsync(
            tenantId,
            EmailAlertType.DocumentsUnavailable,
            EmailAlertSeverity.Warning,
            title: "Documents adapter unavailable",
            safeDescription: "Xenia cannot dispatch attachments to the Documents service.",
            correlationId: correlationId,
            ct: ct);
    }
}
