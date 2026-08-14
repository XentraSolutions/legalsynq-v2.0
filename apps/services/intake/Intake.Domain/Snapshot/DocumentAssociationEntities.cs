namespace Intake.Domain.Snapshot;

public static class DocumentAssociationExecutionStatuses
{
    public const string Pending = "PENDING";
    public const string Processing = "PROCESSING";
    public const string Succeeded = "SUCCEEDED";
    public const string PartiallySucceeded = "PARTIALLY_SUCCEEDED";
    public const string Retryable = "RETRYABLE";
    public const string Failed = "FAILED";
    public const string Cancelled = "CANCELLED";
}

public static class DocumentAssociationItemStatuses
{
    public const string Pending = "PENDING";
    public const string Processing = "PROCESSING";
    public const string Succeeded = "SUCCEEDED";
    public const string Skipped = "SKIPPED";
    public const string Retryable = "RETRYABLE";
    public const string Failed = "FAILED";
}

public sealed class DocumentAssociationExecution
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid SnapshotId { get; set; }
    public Guid? AdapterExecutionId { get; set; }
    public string PolicyCode { get; set; } = string.Empty;
    public int PolicyVersion { get; set; }
    public string ExecutionKey { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string Status { get; set; } = DocumentAssociationExecutionStatuses.Pending;
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
    public ICollection<DocumentAssociationItem> Items { get; set; } = [];
}

public sealed class DocumentAssociationItem
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ExecutionId { get; set; }
    public Guid ArtifactId { get; set; }
    public Guid? DocumentId { get; set; }
    public string DocumentReference { get; set; } = string.Empty;
    public string DocumentRole { get; set; } = string.Empty;
    public string TargetType { get; set; } = string.Empty;
    public Guid TargetId { get; set; }
    public Guid? RelatedCaseId { get; set; }
    public string ItemKey { get; set; } = string.Empty;
    public bool Required { get; set; }
    public string Status { get; set; } = DocumentAssociationItemStatuses.Pending;
    public int AttemptNumber { get; set; }
    public string? FailureCode { get; set; }
    public string? FailureMessage { get; set; }
    public string? DestinationReference { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DocumentAssociationExecution? Execution { get; set; }
}