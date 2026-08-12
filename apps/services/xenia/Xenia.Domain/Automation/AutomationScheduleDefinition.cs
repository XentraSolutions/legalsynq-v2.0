namespace Xenia.Domain.Automation;

/// <summary>
/// Scheduling contract for an automation provider.
/// Phase 1: contracts + persistence only. Hosted scheduling is disabled by default.
/// </summary>
public sealed record AutomationScheduleDefinition
{
    public required string AutomationKey { get; init; }
    public required AutomationTriggerType TriggerType { get; init; }
    public string? IntervalExpression { get; init; }
    public string? CronExpression { get; init; }
    public string? EventFilter { get; init; }
    public bool IsEnabled { get; init; } = false;
    public DateTime? NextScheduledAt { get; init; }
    public DateTime? LastExecutedAt { get; init; }
    public string? IdempotencyKey { get; init; }
}
