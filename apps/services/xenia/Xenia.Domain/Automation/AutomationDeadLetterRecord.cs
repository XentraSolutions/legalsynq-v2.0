using Xenia.Domain.Common;

namespace Xenia.Domain.Automation;

/// <summary>
/// Durable dead-letter record for a failed automation execution.
///
/// Rules:
/// - No raw payload, credentials, raw headers, raw cursors, or message bodies.
/// - SafeErrorSummary is bounded (≤500 chars) and pre-sanitized.
/// - Unique creation is idempotent at the service layer.
/// - Two instances retrying the same item must be safe (use RowVersion concurrency).
/// </summary>
public sealed class AutomationDeadLetterRecord : AuditableEntityBase
{
    public const int AutomationKeyMaxLength     = 200;
    public const int VersionMaxLength           = 50;
    public const int TriggerTypeMaxLength       = 50;
    public const int FailureCategoryMaxLength   = 100;
    public const int SafeErrorSummaryMaxLength  = 500;
    public const int StatusMaxLength            = 50;
    public const int ResolutionMaxLength        = 500;

    private AutomationDeadLetterRecord() { }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string AutomationKey { get; private set; } = string.Empty;
    public string AutomationVersion { get; private set; } = string.Empty;

    /// <summary>Execution that was dead-lettered.</summary>
    public Guid? ExecutionId { get; private set; }

    public AutomationTriggerType TriggerType { get; private set; }

    public string FailureCategory { get; private set; } = string.Empty;

    /// <summary>Safe, bounded error summary. No raw exception text.</summary>
    public string? SafeErrorSummary { get; private set; }

    public int RetryCount { get; private set; }

    /// <summary>Number of replay attempts from the dead-letter queue.</summary>
    public int ReplayCount { get; private set; }

    public DateTime FirstFailedAt { get; private set; }
    public DateTime LastFailedAt { get; private set; }
    public DateTime? NextEligibleRetryAt { get; private set; }

    public AutomationDeadLetterStatus Status { get; private set; }

    public string? Resolution { get; private set; }

    public Guid? CorrelationId { get; private set; }

    public uint RowVersion { get; private set; }

    public static AutomationDeadLetterRecord Create(
        Guid tenantId,
        string automationKey,
        string automationVersion,
        AutomationTriggerType triggerType,
        string failureCategory,
        string? safeErrorSummary,
        int retryCount,
        Guid? executionId = null,
        Guid? correlationId = null)
    {
        var now = DateTime.UtcNow;
        return new AutomationDeadLetterRecord
        {
            Id               = Guid.CreateVersion7(),
            TenantId         = tenantId,
            AutomationKey    = automationKey,
            AutomationVersion = automationVersion,
            ExecutionId      = executionId,
            TriggerType      = triggerType,
            FailureCategory  = failureCategory,
            SafeErrorSummary = safeErrorSummary,
            RetryCount       = retryCount,
            ReplayCount      = 0,
            FirstFailedAt    = now,
            LastFailedAt     = now,
            Status           = AutomationDeadLetterStatus.Open,
            CorrelationId    = correlationId,
            RowVersion       = 0,
        };
    }

    /// <summary>
    /// Atomically acquire this dead letter for retry.
    /// Throws if status is not Open (another instance already acquired it).
    /// </summary>
    public void AcquireForRetry(DateTime? nextEligibleRetryAt = null)
    {
        if (Status != AutomationDeadLetterStatus.Open)
            throw new InvalidOperationException(
                $"Dead letter {Id} cannot be acquired for retry: current status is {Status}.");

        Status               = AutomationDeadLetterStatus.Retrying;
        ReplayCount++;
        NextEligibleRetryAt  = nextEligibleRetryAt;
        RowVersion++;
    }

    public void MarkResolved(string? resolution = null)
    {
        Status     = AutomationDeadLetterStatus.Resolved;
        Resolution = resolution;
        RowVersion++;
    }

    public void MarkAbandoned(string? resolution = null)
    {
        Status     = AutomationDeadLetterStatus.Abandoned;
        Resolution = resolution;
        RowVersion++;
    }

    public void ReturnToOpen(DateTime lastFailedAt)
    {
        Status       = AutomationDeadLetterStatus.Open;
        LastFailedAt = lastFailedAt;
        RowVersion++;
    }
}
