namespace Intake.Domain.Snapshot;

public static class ApprovedSnapshotSchemaCodes
{
    public const string LienIntakeApprovedSnapshotV1 = "LIEN_INTAKE_APPROVED_SNAPSHOT_V1";
}

public static class ApprovedSnapshotStatuses
{
    public const string Creating = "CREATING";
    public const string Ready = "READY";
    public const string Failed = "FAILED";
    public const string Superseded = "SUPERSEDED";
}

public static class ApprovedSnapshotFailureCodes
{
    public const string ReviewRequired = "SNAPSHOT_REVIEW_REQUIRED";
    public const string ReviewNotApproved = "SNAPSHOT_REVIEW_NOT_APPROVED";
    public const string ReviewStale = "SNAPSHOT_REVIEW_STALE";
    public const string ReprocessingRequired = "SNAPSHOT_REPROCESSING_REQUIRED";
    public const string SchemaUnavailable = "SNAPSHOT_SCHEMA_UNAVAILABLE";
    public const string PayloadInvalid = "SNAPSHOT_PAYLOAD_INVALID";
    public const string HashFailed = "SNAPSHOT_HASH_FAILED";
    public const string CreationFailed = "SNAPSHOT_CREATION_FAILED";
    public const string TenantContextInvalid = "SNAPSHOT_TENANT_CONTEXT_INVALID";
    public const string NotFound = "SNAPSHOT_NOT_FOUND";
    public const string ConcurrencyConflict = "SNAPSHOT_CONCURRENCY_CONFLICT";
}

public static class IntakeAdapterCodes
{
    public const string NoopV1 = "NOOP_V1";
    public const string SynqLienV1 = "SYNQLIEN_V1";
}

public static class IntakeAdapterExecutionStatuses
{
    public const string Pending = "PENDING";
    public const string Processing = "PROCESSING";
    public const string Succeeded = "SUCCEEDED";
    public const string Failed = "FAILED";
    public const string Retryable = "RETRYABLE";
    public const string Cancelled = "CANCELLED";
}

public static class IntakeAdapterFailureCodes
{
    public const string NotConfigured = "ADAPTER_NOT_CONFIGURED";
    public const string Disabled = "ADAPTER_DISABLED";
    public const string Unavailable = "ADAPTER_UNAVAILABLE";
    public const string ValidationFailed = "ADAPTER_VALIDATION_FAILED";
    public const string ExecutionFailed = "ADAPTER_EXECUTION_FAILED";
    public const string Timeout = "ADAPTER_TIMEOUT";
    public const string RetryExhausted = "ADAPTER_RETRY_EXHAUSTED";
    public const string ResultInvalid = "ADAPTER_RESULT_INVALID";
    public const string TenantContextInvalid = "ADAPTER_TENANT_CONTEXT_INVALID";
    public const string NotFound = "ADAPTER_EXECUTION_NOT_FOUND";
    public const string ConcurrencyConflict = "ADAPTER_CONCURRENCY_CONFLICT";
}