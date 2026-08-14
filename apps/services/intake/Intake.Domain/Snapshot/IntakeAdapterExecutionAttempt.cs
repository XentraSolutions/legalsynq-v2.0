namespace Intake.Domain.Snapshot;

public sealed class IntakeAdapterExecutionAttempt
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid AdapterExecutionId { get; set; }
    public int AttemptNumber { get; set; }
    public string Status { get; set; } = IntakeAdapterExecutionStatuses.Processing;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? FailureCode { get; set; }
    public string? FailureMessage { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}