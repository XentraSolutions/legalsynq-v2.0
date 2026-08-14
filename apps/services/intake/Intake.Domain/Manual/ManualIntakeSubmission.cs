namespace Intake.Domain.Manual;

public sealed class ManualIntakeSubmission
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? OrgId { get; set; }
    public Guid? TenantIntakeSourceId { get; set; }
    public string SourceType { get; set; } = "MANUAL";
    public string Purpose { get; set; } = string.Empty;
    public string ProcessingProfileCode { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? ExternalReference { get; set; }
    public string? Notes { get; set; }
    public string? ClientRequestId { get; set; }
    public Guid? SubmittedBy { get; set; }
    public DateTimeOffset SubmittedAt { get; set; }
    public string Status { get; set; } = ManualIntakeSubmissionStatuses.Processing;
    public string? FailureMessage { get; set; }
    public int ConfigurationVersion { get; set; }
    public int ProfileConfigurationVersion { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public int Version { get; set; } = 1;
}

public static class ManualIntakeSubmissionStatuses
{
    public const string Processing = "PROCESSING";
    public const string Completed = "COMPLETED";
    public const string Partial = "PARTIAL";
    public const string Failed = "FAILED";
    public const string Cancelled = "CANCELLED";
}