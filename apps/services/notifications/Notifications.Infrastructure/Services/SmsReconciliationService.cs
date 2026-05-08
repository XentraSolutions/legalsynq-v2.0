using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Notifications.Application.Interfaces;
using LegalSynq.AuditClient;
using LegalSynq.AuditClient.DTOs;

namespace Notifications.Infrastructure.Services;

/// <summary>
/// Reconciles outbound SMS delivery state by querying the SMS provider (Twilio) for
/// the current message status. Acts as a backstop for missed or delayed webhook events.
///
/// Key invariants:
///   - Never sends or resends SMS.
///   - Reuses DeliveryStatusService for status updates (inherits terminal-state protection).
///   - Supports only providers implementing ISmsProviderStatusLookup.
///   - All audit calls are best-effort (wrapped in try/catch).
///   - Provider credentials are never logged.
/// </summary>
public class SmsReconciliationService : ISmsReconciliationService
{
    private readonly INotificationAttemptRepository _attemptRepo;
    private readonly IDeliveryStatusService _deliveryStatusSvc;
    private readonly ISmsProviderAdapter _smsAdapter;
    private readonly IAuditEventClient _auditClient;
    private readonly ILogger<SmsReconciliationService> _logger;

    // Non-terminal attempt statuses eligible for vendor reconciliation.
    private static readonly IReadOnlyCollection<string> StaleCandidateStatuses =
        new[] { "pending", "sending", "sent", "queued", "processing", "retrying" };

    // Maps normalized vendor status → normalized event type for DeliveryStatusService.
    // DeliveryStatusService already maps normalized events → attempt/notification statuses.
    private static readonly Dictionary<string, string> NormalizedStatusToEvent = new()
    {
        ["queued"]     = "queued",
        ["processing"] = "queued",
        ["sent"]       = "sent",
        ["delivered"]  = "delivered",
        ["failed"]     = "failed",
    };

    public SmsReconciliationService(
        INotificationAttemptRepository attemptRepo,
        IDeliveryStatusService deliveryStatusSvc,
        ISmsProviderAdapter smsAdapter,
        IAuditEventClient auditClient,
        ILogger<SmsReconciliationService> logger)
    {
        _attemptRepo       = attemptRepo;
        _deliveryStatusSvc = deliveryStatusSvc;
        _smsAdapter        = smsAdapter;
        _auditClient       = auditClient;
        _logger            = logger;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public async Task<SmsReconciliationResult> ReconcileByAttemptIdAsync(Guid attemptId, CancellationToken ct = default)
    {
        var attempt = await _attemptRepo.GetByIdAsync(attemptId);
        if (attempt == null)
        {
            _logger.LogWarning("SMS reconciliation: attempt not found {AttemptId}", attemptId);
            return Skipped(SmsReconciliationResult.OutcomeAttemptNotFound, null, null, null);
        }
        return await ReconcileAttemptAsync(attempt, "manual", ct);
    }

    public async Task<SmsReconciliationResult> ReconcileByProviderMessageIdAsync(string providerMessageId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(providerMessageId))
            return Skipped(SmsReconciliationResult.OutcomeSkippedMissingProviderId, null, null, null);

        var attempt = await _attemptRepo.FindByProviderMessageIdAsync(providerMessageId);
        if (attempt == null)
        {
            _logger.LogWarning("SMS reconciliation: no attempt found for SID={Sid}", providerMessageId);
            return Skipped(SmsReconciliationResult.OutcomeAttemptNotFound, null, null, providerMessageId);
        }
        return await ReconcileAttemptAsync(attempt, "manual", ct);
    }

    public async Task<SmsReconciliationBatchResult> ReconcileStalePendingAsync(int limit, TimeSpan olderThan, CancellationToken ct = default)
    {
        var sw      = Stopwatch.StartNew();
        var safeLimit = Math.Min(Math.Max(limit, 1), 200);
        var cutoff    = DateTime.UtcNow - olderThan;

        var staleAttempts = await _attemptRepo.GetStaleSmsAttemptsAsync(safeLimit, cutoff, StaleCandidateStatuses, ct);

        _logger.LogInformation(
            "SMS reconciliation batch: found {Count} stale attempts (limit={Limit}, olderThan={OlderThan}m)",
            staleAttempts.Count, safeLimit, (int)olderThan.TotalMinutes);

        var results = new List<SmsReconciliationResult>(staleAttempts.Count);
        int updated = 0, noChange = 0, skipped = 0, failed = 0;

        foreach (var attempt in staleAttempts)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                var result = await ReconcileAttemptAsync(attempt, "batch", ct);
                results.Add(result);
                switch (result.Outcome)
                {
                    case SmsReconciliationResult.OutcomeUpdated:      updated++;  break;
                    case SmsReconciliationResult.OutcomeNoChange:      noChange++; break;
                    case SmsReconciliationResult.OutcomeVendorLookupFailed: failed++; break;
                    default:                                           skipped++;  break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SMS reconciliation batch: unexpected error for attempt {AttemptId}", attempt.Id);
                failed++;
            }
        }

        sw.Stop();

        // Batch audit event.
        try
        {
            await _auditClient.IngestAsync(new IngestAuditEventRequest
            {
                EventType    = "sms.reconciliation.batch_completed",
                Action       = "sms.reconciliation.batch_completed",
                SourceSystem = "notifications",
                Outcome      = "success",
                Description  = $"SMS reconciliation batch completed: {updated} updated, {noChange} no-change, {skipped} skipped, {failed} failed",
                Metadata     = JsonSerializer.Serialize(new
                {
                    total    = staleAttempts.Count,
                    updated,
                    no_change = noChange,
                    skipped,
                    failed,
                    duration_ms  = (int)sw.Elapsed.TotalMilliseconds,
                    source       = "batch",
                    older_than_m = (int)olderThan.TotalMinutes,
                    limit        = safeLimit,
                }),
            });
        }
        catch (Exception ex) { _logger.LogError(ex, "Failed to audit SMS reconciliation batch"); }

        return new SmsReconciliationBatchResult
        {
            Total    = staleAttempts.Count,
            Updated  = updated,
            NoChange = noChange,
            Skipped  = skipped,
            Failed   = failed,
            Duration = sw.Elapsed,
            Results  = results,
        };
    }

    // ── Core reconciliation logic ─────────────────────────────────────────────

    private async Task<SmsReconciliationResult> ReconcileAttemptAsync(
        Domain.NotificationAttempt attempt,
        string source,
        CancellationToken ct)
    {
        // Guard: only SMS attempts.
        if (!string.Equals(attempt.Channel, "sms", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug("SMS reconciliation: skipping non-SMS attempt {AttemptId} (channel={Channel})", attempt.Id, attempt.Channel);
            return Skipped(SmsReconciliationResult.OutcomeSkippedNotSms, attempt.Id, attempt.NotificationId, attempt.ProviderMessageId);
        }

        // Guard: must have a provider message ID to query vendor.
        if (string.IsNullOrWhiteSpace(attempt.ProviderMessageId))
        {
            _logger.LogDebug("SMS reconciliation: skipping attempt {AttemptId} — no ProviderMessageId", attempt.Id);
            return Skipped(SmsReconciliationResult.OutcomeSkippedMissingProviderId, attempt.Id, attempt.NotificationId, null);
        }

        // Guard: provider adapter must support status lookup.
        if (_smsAdapter is not ISmsProviderStatusLookup statusLookup)
        {
            _logger.LogDebug("SMS reconciliation: provider {Provider} does not support status lookup", _smsAdapter.ProviderType);
            return Skipped(SmsReconciliationResult.OutcomeSkippedUnsupportedProvider, attempt.Id, attempt.NotificationId, attempt.ProviderMessageId);
        }

        // Vendor lookup.
        SmsMessageStatusResult vendorResult;
        try
        {
            vendorResult = await statusLookup.GetMessageStatusAsync(attempt.ProviderMessageId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SMS reconciliation: vendor lookup threw for SID={Sid}", attempt.ProviderMessageId);
            await AuditAsync("sms.reconciliation.lookup_failed", attempt, null, null, attempt.Status,
                "unexpected_error", ex.Message, false, source);
            return new SmsReconciliationResult
            {
                Success           = false,
                Outcome           = SmsReconciliationResult.OutcomeVendorLookupFailed,
                AttemptId         = attempt.Id,
                NotificationId    = attempt.NotificationId,
                Provider          = attempt.Provider,
                ProviderMessageId = attempt.ProviderMessageId,
                PreviousStatus    = attempt.Status,
                ErrorCode         = "unexpected_error",
                ErrorMessage      = ex.Message,
                Retryable         = true,
            };
        }

        if (!vendorResult.Success)
        {
            if (vendorResult.ErrorCode == "message_not_found")
            {
                _logger.LogWarning("SMS reconciliation: SID={Sid} not found at provider", attempt.ProviderMessageId);
                await AuditAsync("sms.reconciliation.skipped", attempt, null, vendorResult.ProviderStatus, attempt.Status,
                    vendorResult.ErrorCode, vendorResult.ErrorMessage, false, source);
                return new SmsReconciliationResult
                {
                    Success           = false,
                    Outcome           = SmsReconciliationResult.OutcomeProviderMessageNotFound,
                    AttemptId         = attempt.Id,
                    NotificationId    = attempt.NotificationId,
                    Provider          = attempt.Provider,
                    ProviderMessageId = attempt.ProviderMessageId,
                    PreviousStatus    = attempt.Status,
                    VendorStatus      = vendorResult.ProviderStatus,
                    ErrorCode         = vendorResult.ErrorCode,
                    ErrorMessage      = vendorResult.ErrorMessage,
                };
            }

            _logger.LogWarning(
                "SMS reconciliation: vendor lookup failed for SID={Sid}: {Code} (retryable={R})",
                attempt.ProviderMessageId, vendorResult.ErrorCode, vendorResult.Retryable);

            await AuditAsync("sms.reconciliation.lookup_failed", attempt, null, vendorResult.ProviderStatus, attempt.Status,
                vendorResult.ErrorCode, vendorResult.ErrorMessage, vendorResult.Retryable, source);

            return new SmsReconciliationResult
            {
                Success           = false,
                Outcome           = SmsReconciliationResult.OutcomeVendorLookupFailed,
                AttemptId         = attempt.Id,
                NotificationId    = attempt.NotificationId,
                Provider          = attempt.Provider,
                ProviderMessageId = attempt.ProviderMessageId,
                PreviousStatus    = attempt.Status,
                VendorStatus      = vendorResult.ProviderStatus,
                NormalizedVendorStatus = vendorResult.NormalizedStatus,
                ErrorCode         = vendorResult.ErrorCode,
                ErrorMessage      = vendorResult.ErrorMessage,
                Retryable         = vendorResult.Retryable,
            };
        }

        // Map normalized vendor status → normalized event type for DeliveryStatusService.
        var normalizedStatus = vendorResult.NormalizedStatus ?? "";
        if (!NormalizedStatusToEvent.TryGetValue(normalizedStatus, out var eventType))
        {
            _logger.LogWarning(
                "SMS reconciliation: unknown normalized status '{Status}' for SID={Sid} — skipping update",
                normalizedStatus, attempt.ProviderMessageId);
            await AuditAsync("sms.reconciliation.skipped", attempt, normalizedStatus, vendorResult.ProviderStatus, attempt.Status,
                "unknown_normalized_status", null, false, source);
            return Skipped(SmsReconciliationResult.OutcomeNoChange, attempt.Id, attempt.NotificationId, attempt.ProviderMessageId);
        }

        var previousStatus = attempt.Status;

        // Reload attempt to capture latest status before update (handles concurrent webhook updates).
        var freshAttempt = await _attemptRepo.GetByIdAsync(attempt.Id);
        if (freshAttempt != null) previousStatus = freshAttempt.Status;

        // Delegate to DeliveryStatusService — inherits terminal-state protection.
        await _deliveryStatusSvc.UpdateAttemptFromEventAsync(attempt.Id, eventType);
        await _deliveryStatusSvc.UpdateNotificationFromEventAsync(attempt.NotificationId, eventType);

        // Check if anything actually changed.
        var afterAttempt = await _attemptRepo.GetByIdAsync(attempt.Id);
        var newStatus    = afterAttempt?.Status ?? previousStatus;
        var didUpdate    = !string.Equals(previousStatus, newStatus, StringComparison.OrdinalIgnoreCase);

        var outcome = didUpdate
            ? SmsReconciliationResult.OutcomeUpdated
            : SmsReconciliationResult.OutcomeNoChange;

        var auditEvent = didUpdate
            ? "sms.reconciliation.updated"
            : "sms.reconciliation.no_change";

        _logger.LogInformation(
            "SMS reconciliation: SID={Sid} → outcome={Outcome}, prev={Prev}, vendor={Vendor}, new={New}",
            attempt.ProviderMessageId, outcome, previousStatus, vendorResult.ProviderStatus, newStatus);

        await AuditAsync(auditEvent, attempt, normalizedStatus, vendorResult.ProviderStatus, newStatus,
            null, null, false, source, previousStatus);

        return new SmsReconciliationResult
        {
            Success                = true,
            Updated                = didUpdate,
            Outcome                = outcome,
            AttemptId              = attempt.Id,
            NotificationId         = attempt.NotificationId,
            Provider               = attempt.Provider,
            ProviderMessageId      = attempt.ProviderMessageId,
            PreviousStatus         = previousStatus,
            VendorStatus           = vendorResult.ProviderStatus,
            NormalizedVendorStatus = normalizedStatus,
            NewStatus              = newStatus,
        };
    }

    // ── Audit ─────────────────────────────────────────────────────────────────

    private async Task AuditAsync(
        string eventType,
        Domain.NotificationAttempt attempt,
        string? normalizedVendorStatus,
        string? rawVendorStatus,
        string? newStatus,
        string? errorCode,
        string? errorMessage,
        bool retryable,
        string source,
        string? previousStatus = null)
    {
        try
        {
            await _auditClient.IngestAsync(new IngestAuditEventRequest
            {
                EventType    = eventType,
                Action       = eventType,
                SourceSystem = "notifications",
                Outcome      = eventType.Contains("failed") ? "failure" : "success",
                Description  = $"SMS reconciliation: {eventType} for attempt {attempt.Id}",
                Scope        = new AuditEventScopeDto { TenantId = attempt.TenantId.HasValue ? attempt.TenantId.Value.ToString() : string.Empty },
                Entity       = new AuditEventEntityDto { Type = "NOTIFICATION_ATTEMPT", Id = attempt.Id.ToString() },
                Metadata     = JsonSerializer.Serialize(new
                {
                    provider             = attempt.Provider,
                    provider_message_id  = attempt.ProviderMessageId,
                    notification_id      = attempt.NotificationId,
                    attempt_id           = attempt.Id,
                    previous_status      = previousStatus ?? attempt.Status,
                    vendor_status        = rawVendorStatus,
                    normalized_vendor_status = normalizedVendorStatus,
                    new_status           = newStatus,
                    error_code           = errorCode,
                    error_message        = errorMessage,
                    retryable,
                    source,
                }),
            });
        }
        catch (Exception ex) { _logger.LogError(ex, "Failed to audit SMS reconciliation event {EventType}", eventType); }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static SmsReconciliationResult Skipped(string outcome, Guid? attemptId, Guid? notificationId, string? providerMessageId)
        => new()
        {
            Success           = true,
            Updated           = false,
            Outcome           = outcome,
            AttemptId         = attemptId,
            NotificationId    = notificationId,
            ProviderMessageId = providerMessageId,
        };
}
