using Xenia.Domain.Automation;

namespace Xenia.Application.Automation.Models;

public sealed record AutomationContext
{
    public required Guid? TenantId { get; init; }
    public required Guid? ActorId { get; init; }
    public required string? CorrelationId { get; init; }
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();
}

public sealed record AutomationExecutionRequest
{
    public required string AutomationKey { get; init; }
    public required string? AutomationVersion { get; init; }
    public required AutomationContext Context { get; init; }
    public required AutomationTriggerType TriggerType { get; init; }
    public required string IdempotencyKey { get; init; }
    public IReadOnlyDictionary<string, string> Parameters { get; init; } = new Dictionary<string, string>();
    public int MaxRetryAttempts { get; init; } = 3;
    public TimeSpan? Timeout { get; init; }
}

public sealed record AutomationExecutionResult
{
    public required Guid ExecutionId { get; init; }
    public required string AutomationKey { get; init; }
    public required string AutomationVersion { get; init; }
    public required AutomationExecutionStatus Status { get; init; }
    public required DateTime StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public TimeSpan? Duration => CompletedAt.HasValue ? CompletedAt.Value - StartedAt : null;
    public int RetryCount { get; init; }
    public string? SafeErrorSummary { get; init; }
    public string? FailureCategory { get; init; }
    public IReadOnlyDictionary<string, string> SafeMetadata { get; init; } = new Dictionary<string, string>();
    public bool IsSuccess => Status == AutomationExecutionStatus.Completed;
}

public sealed record AutomationExecutionMetadata
{
    public required Guid ExecutionId { get; init; }
    public required string AutomationKey { get; init; }
    public required string AutomationVersion { get; init; }
    public required Guid? TenantId { get; init; }
    public required AutomationTriggerType TriggerType { get; init; }
    public required AutomationExecutionStatus Status { get; init; }
    public required DateTime QueuedAt { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public int RetryCount { get; init; }
    public string? CorrelationId { get; init; }
    public string? SafeErrorSummary { get; init; }
    public string? FailureCategory { get; init; }
    public bool IsDeadLettered { get; init; }
}

public sealed record AutomationExecutionError
{
    public required string FailureCategory { get; init; }
    public required string SafeErrorSummary { get; init; }
    public bool IsRetryable { get; init; }
    public bool ShouldDeadLetter { get; init; }
}

public sealed record AutomationTrigger
{
    public required AutomationTriggerType Type { get; init; }
    public string? Source { get; init; }
    public string? EventType { get; init; }
    public string? IdempotencyKey { get; init; }
}
