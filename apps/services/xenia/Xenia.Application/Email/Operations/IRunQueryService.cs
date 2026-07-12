using Xenia.Domain.Email;

namespace Xenia.Application.Email.Operations;

/// <summary>
/// Provides paginated, filtered queries over email ingestion runs.
///
/// Security: no raw cursors, credentials, or message bodies in results.
/// All queries are tenant-scoped unless the caller is PlatformAdmin.
/// </summary>
public interface IRunQueryService
{
    Task<RunPageResult> ListAsync(RunListQuery query, CancellationToken ct = default);
    Task<RunDetailResult?> GetDetailAsync(Guid tenantId, Guid runId, CancellationToken ct = default);

    /// <summary>
    /// Requests retry of a failed or completed-with-errors run.
    /// Creates a new run linked to the original.
    /// Returns the new run ID.
    /// </summary>
    Task<RunRetryResult> RetryAsync(Guid tenantId, Guid runId, Guid? actorId, string? correlationId, CancellationToken ct = default);

    /// <summary>
    /// Requests cancellation of a queued or active run.
    /// </summary>
    Task<RunCancellationResult> CancelAsync(Guid tenantId, Guid runId, Guid? actorId, string? correlationId, CancellationToken ct = default);
}

public sealed record RunListQuery(
    Guid TenantId,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    Guid? SourceId = null,
    EmailProviderType? Provider = null,
    IngestionRunTriggerType? Trigger = null,
    IngestionRunStatus? Status = null,
    bool? HasErrors = null,
    string? CorrelationId = null,
    string? WorkerInstanceId = null,
    int Page = 1,
    int PageSize = 50);

public sealed record RunPageResult(
    IReadOnlyList<EmailIngestionRun> Items,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record RunDetailResult(
    EmailIngestionRun Run,
    string? SafeCursorBeforeSummary,
    string? SafeCursorAfterSummary);

public sealed record RunRetryResult(
    bool Success,
    Guid? NewRunId,
    string? ErrorCode,
    string? SafeMessage);

public sealed record RunCancellationResult(
    string State,
    string? SafeMessage);
