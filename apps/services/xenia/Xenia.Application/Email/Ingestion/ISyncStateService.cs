using Xenia.Domain.Email;

namespace Xenia.Application.Email.Ingestion;

/// <summary>
/// Manages the durable sync state (cursor + run history) for Email sources.
/// </summary>
public interface ISyncStateService
{
    /// <summary>Gets the current sync state for a source, or creates one if it doesn't exist.</summary>
    Task<EmailSyncState> GetOrCreateAsync(Guid tenantId, Guid emailSourceId, EmailProviderType providerType, CancellationToken ct = default);

    /// <summary>Records the start of a new ingestion run.</summary>
    Task<EmailIngestionRun> StartRunAsync(
        Guid tenantId,
        Guid emailSourceId,
        IngestionRunTriggerType triggerType,
        string? correlationId,
        Guid? actorId,
        string? workerInstanceId,
        string? cursorBeforeSafeSummary,
        CancellationToken ct = default);

    /// <summary>Updates the run status to Running.</summary>
    Task MarkRunStartedAsync(Guid runId, CancellationToken ct = default);

    /// <summary>
    /// Commits the cursor after a page of messages has been durably persisted.
    /// Uses optimistic concurrency on StateVersion to prevent conflicting updates.
    /// </summary>
    Task CommitCursorAsync(
        Guid tenantId,
        Guid emailSourceId,
        ProviderSyncCursor cursor,
        DateTime? lastProcessedTimestamp,
        string? lastProcessedMessageId,
        CancellationToken ct = default);

    /// <summary>Records failure and computes backoff for next eligible sync.</summary>
    Task RecordFailureAsync(Guid tenantId, Guid emailSourceId, string errorCode, string safeErrorSummary, CancellationToken ct = default);

    /// <summary>Completes an ingestion run with final counters.</summary>
    Task CompleteRunAsync(Guid runId, string? cursorAfterSafeSummary, CancellationToken ct = default);

    /// <summary>Fails an ingestion run.</summary>
    Task FailRunAsync(Guid runId, string errorCode, string safeErrorSummary, CancellationToken ct = default);

    /// <summary>Resets the cursor for a source. Requires auditing by the caller.</summary>
    Task ResetCursorAsync(Guid tenantId, Guid emailSourceId, string reason, CancellationToken ct = default);

    /// <summary>Gets the sync state for a source.</summary>
    Task<EmailSyncState?> GetSyncStateAsync(Guid tenantId, Guid emailSourceId, CancellationToken ct = default);

    /// <summary>Gets paginated ingestion history for a source.</summary>
    Task<IReadOnlyList<EmailIngestionRun>> GetIngestionHistoryAsync(Guid tenantId, Guid emailSourceId, int pageSize, int pageOffset, CancellationToken ct = default);

    /// <summary>Gets a single run.</summary>
    Task<EmailIngestionRun?> GetRunAsync(Guid tenantId, Guid runId, CancellationToken ct = default);

    /// <summary>Returns whether a run is currently active for the given source.</summary>
    Task<EmailIngestionRun?> GetActiveRunAsync(Guid tenantId, Guid emailSourceId, CancellationToken ct = default);

    /// <summary>Updates run counters atomically.</summary>
    Task UpdateRunCountersAsync(Guid runId, EmailIngestionRun counters, CancellationToken ct = default);
}
