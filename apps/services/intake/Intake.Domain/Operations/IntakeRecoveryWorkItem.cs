namespace Intake.Domain.Operations;

public static class IntakeRecoveryStages
{
    public const string EmailCapture = "EMAIL_CAPTURE";
    public const string ArtifactProcessing = "ARTIFACT_PROCESSING";
    public const string Classification = "CLASSIFICATION";
    public const string Extraction = "EXTRACTION";
    public const string Normalization = "NORMALIZATION";
    public const string Matching = "MATCHING";
    public const string Policy = "POLICY";
    public const string Review = "REVIEW";
    public const string Snapshot = "SNAPSHOT";
    public const string AdapterExecution = "ADAPTER_EXECUTION";
    public const string DocumentAssociation = "DOCUMENT_ASSOCIATION";
}

public static class IntakeRecoveryStatuses
{
    public const string Pending = "PENDING";
    public const string Stale = "STALE";
    public const string Processing = "PROCESSING";
    public const string Recovered = "RECOVERED";
    public const string Retryable = "RETRYABLE";
    public const string Failed = "FAILED";
    public const string Exhausted = "REQUIRES_ATTENTION";
    public const string Cancelled = "CANCELLED";
    public const string Acknowledged = "ACKNOWLEDGED";
}

public static class IntakeFailureCategories
{
    public const string Validation = "VALIDATION";
    public const string Authorization = "AUTHORIZATION";
    public const string TenantIsolation = "TENANT_ISOLATION";
    public const string Dependency = "DEPENDENCY";
    public const string Timeout = "TIMEOUT";
    public const string RateLimit = "RATE_LIMIT";
    public const string Configuration = "CONFIGURATION";
    public const string Concurrency = "CONCURRENCY";
    public const string Integrity = "INTEGRITY";
    public const string Data = "DATA";
    public const string Unknown = "UNKNOWN";
}

public sealed class IntakeRecoveryWorkItem
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Stage { get; set; } = string.Empty;
    public Guid ObjectId { get; set; }
    public string DomainStatus { get; set; } = string.Empty;
    public string RecoveryStatus { get; set; } = IntakeRecoveryStatuses.Pending;
    public bool Retryable { get; set; }
    public int AttemptCount { get; set; }
    public DateTimeOffset? LastRecoveryAttemptAt { get; set; }
    public DateTimeOffset? NextRetryAt { get; set; }
    public string? LastFailureCode { get; set; }
    public string? LastSafeMessage { get; set; }
    public string? FailureCategory { get; set; }
    public string RecoverySource { get; set; } = "AUTOMATIC";
    public DateTimeOffset? ExhaustedAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public Guid? CancelledByUserId { get; set; }
    public DateTimeOffset? StaleSince { get; set; }
    public DateTimeOffset? ClaimedAt { get; set; }
    public string? ClaimToken { get; set; }
    public string? CorrelationId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public int Version { get; set; } = 1;
    public ICollection<IntakeRecoveryAttempt> Attempts { get; set; } = [];
}

public sealed class IntakeRecoveryAttempt
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid WorkItemId { get; set; }
    public int AttemptNumber { get; set; }
    public string Status { get; set; } = IntakeRecoveryStatuses.Processing;
    public string? FailureCode { get; set; }
    public string? SafeMessage { get; set; }
    public string? FailureCategory { get; set; }
    public string RecoverySource { get; set; } = "AUTOMATIC";
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}