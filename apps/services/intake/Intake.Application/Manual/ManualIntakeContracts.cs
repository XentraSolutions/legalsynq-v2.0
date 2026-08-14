using Intake.Application.Artifacts;

namespace Intake.Application.Manual;

public sealed record ManualIntakeFile(
    string FileName,
    string ContentType,
    byte[] Content);

public sealed class CreateManualIntakeRequest
{
    public string Purpose { get; init; } = string.Empty;
    public string? ProcessingProfileCode { get; init; }
    public string? Title { get; init; }
    public string? ExternalReference { get; init; }
    public string? Notes { get; init; }
    public string? ClientRequestId { get; init; }
    public IReadOnlyList<ManualIntakeFile> Files { get; init; } = [];
}

public sealed class ManualIntakeListQuery
{
    public string? Status { get; init; }
    public string? Purpose { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 25;
}

public sealed record ManualIntakeSubmissionResponse(
    Guid Id,
    Guid TenantId,
    Guid? OrgId,
    Guid? TenantIntakeSourceId,
    string SourceType,
    string Purpose,
    string ProcessingProfileCode,
    string? Title,
    string? ExternalReference,
    string? Notes,
    string? ClientRequestId,
    Guid? SubmittedBy,
    DateTimeOffset SubmittedAt,
    string Status,
    string? FailureMessage,
    int ConfigurationVersion,
    int ProfileConfigurationVersion,
    int Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyList<IntakeArtifactResponse> Artifacts);

public sealed record ManualIntakeListResponse(
    IReadOnlyList<ManualIntakeSubmissionResponse> Items,
    int Page,
    int PageSize,
    long TotalCount);

public sealed record ManualIntakeAnalyticsResponse(
    Guid TenantId,
    long TotalSubmissions,
    long CompletedSubmissions,
    long PartialSubmissions,
    long FailedSubmissions,
    long CancelledSubmissions,
    long TotalArtifacts,
    long CompletedArtifacts,
    long FailedArtifacts,
    long TotalBytes,
    long UploadedBytes);