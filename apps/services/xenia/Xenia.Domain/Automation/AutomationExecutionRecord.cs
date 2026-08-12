using Xenia.Domain.Common;

namespace Xenia.Domain.Automation;

/// <summary>
/// Durable execution history record for one automation invocation.
///
/// Rules:
/// - No raw business payload stored.
/// - No message body stored.
/// - No attachment binary stored.
/// - SafeResultSummary and SafeErrorSummary are bounded (≤500 chars) and pre-sanitized.
/// - ExecutionId is unique across all tenants.
/// - Idempotency key is per-tenant per-automation unique when populated.
/// </summary>
public sealed class AutomationExecutionRecord : AuditableEntityBase
{
    public const int AutomationKeyMaxLength    = 200;
    public const int VersionMaxLength          = 50;
    public const int TriggerTypeMaxLength      = 50;
    public const int StatusMaxLength           = 50;
    public const int IdempotencyKeyMaxLength   = 200;
    public const int ActorIdMaxLength          = 200;
    public const int SafeSummaryMaxLength      = 500;
    public const int ErrorCategoryMaxLength    = 100;
    public const int WorkerInstanceIdMaxLength = 200;

    private AutomationExecutionRecord() { }

    public Guid Id { get; private set; }

    /// <summary>Globally unique execution identifier (UUIDv7).</summary>
    public Guid ExecutionId { get; private set; }

    public Guid TenantId { get; private set; }
    public string AutomationKey { get; private set; } = string.Empty;
    public string AutomationVersion { get; private set; } = string.Empty;

    public AutomationTriggerType TriggerType { get; private set; }

    public AutomationExecutionStatus Status { get; private set; }

    /// <summary>Client-supplied idempotency key. Null if not provided.</summary>
    public string? IdempotencyKey { get; private set; }

    public Guid? CorrelationId { get; private set; }

    /// <summary>User or service that triggered the execution.</summary>
    public string? ActorId { get; private set; }

    public DateTime QueuedAt { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    /// <summary>Number of times this execution has been retried.</summary>
    public int RetryCount { get; private set; }

    /// <summary>Parent execution ID for retry-linked executions.</summary>
    public Guid? ParentExecutionId { get; private set; }

    /// <summary>Dead-letter record ID if this execution was dead-lettered.</summary>
    public Guid? DeadLetterId { get; private set; }

    /// <summary>Safe, bounded summary of a successful result. No business payload.</summary>
    public string? SafeResultSummary { get; private set; }

    public string? SafeErrorCategory { get; private set; }

    /// <summary>Safe, bounded error summary. No raw exception text.</summary>
    public string? SafeErrorSummary { get; private set; }

    public string? WorkerInstanceId { get; private set; }

    public uint RowVersion { get; private set; }

    public static AutomationExecutionRecord Create(
        Guid tenantId,
        string automationKey,
        string automationVersion,
        AutomationTriggerType triggerType,
        string? idempotencyKey = null,
        Guid? correlationId = null,
        string? actorId = null,
        Guid? parentExecutionId = null)
    {
        return new AutomationExecutionRecord
        {
            Id                = Guid.CreateVersion7(),
            ExecutionId       = Guid.CreateVersion7(),
            TenantId          = tenantId,
            AutomationKey     = automationKey,
            AutomationVersion = automationVersion,
            TriggerType       = triggerType,
            Status            = AutomationExecutionStatus.Queued,
            IdempotencyKey    = idempotencyKey,
            CorrelationId     = correlationId,
            ActorId           = actorId,
            QueuedAt          = DateTime.UtcNow,
            ParentExecutionId = parentExecutionId,
            RetryCount        = 0,
            RowVersion        = 0,
        };
    }

    public void MarkRunning(string? workerInstanceId = null)
    {
        Status           = AutomationExecutionStatus.Running;
        StartedAt        = DateTime.UtcNow;
        WorkerInstanceId = workerInstanceId;
        RowVersion++;
    }

    public void MarkCompleted(string? safeResultSummary = null)
    {
        Status            = AutomationExecutionStatus.Completed;
        CompletedAt       = DateTime.UtcNow;
        SafeResultSummary = safeResultSummary;
        RowVersion++;
    }

    public void MarkCompletedWithErrors(string? safeErrorCategory, string? safeErrorSummary)
    {
        Status            = AutomationExecutionStatus.CompletedWithErrors;
        CompletedAt       = DateTime.UtcNow;
        SafeErrorCategory = safeErrorCategory;
        SafeErrorSummary  = safeErrorSummary;
        RowVersion++;
    }

    public void MarkFailed(string? safeErrorCategory, string? safeErrorSummary)
    {
        Status            = AutomationExecutionStatus.Failed;
        CompletedAt       = DateTime.UtcNow;
        SafeErrorCategory = safeErrorCategory;
        SafeErrorSummary  = safeErrorSummary;
        RowVersion++;
    }

    public void MarkCancelled()
    {
        Status      = AutomationExecutionStatus.Cancelled;
        CompletedAt = DateTime.UtcNow;
        RowVersion++;
    }

    public void MarkDeadLettered(Guid deadLetterId)
    {
        Status       = AutomationExecutionStatus.DeadLettered;
        CompletedAt  = DateTime.UtcNow;
        DeadLetterId = deadLetterId;
        RowVersion++;
    }

    public void IncrementRetry()
    {
        RetryCount++;
        RowVersion++;
    }
}
