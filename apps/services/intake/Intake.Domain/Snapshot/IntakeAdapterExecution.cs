namespace Intake.Domain.Snapshot;

public sealed class IntakeAdapterExecution
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid SnapshotId { get; set; }
    public string AdapterCode { get; set; } = string.Empty;
    public string AdapterVersion { get; set; } = string.Empty;
    public string ExecutionKey { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string ClaimToken { get; set; } = string.Empty;
    public string Status { get; set; } = IntakeAdapterExecutionStatuses.Pending;
    public int AttemptNumber { get; set; }
    public Guid RequestedByUserId { get; set; }
    public DateTimeOffset RequestedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? FailureCode { get; set; }
    public string? FailureMessage { get; set; }
    public string ResultJson { get; set; } = "{}";
    public int Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public ICollection<IntakeAdapterExecutionAttempt> Attempts { get; set; } = [];
    public ICollection<IntakeAdapterExternalReference> ExternalReferences { get; set; } = [];
}