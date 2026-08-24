using Xenia.Domain.Email;

namespace Xenia.Application.Email.Ingestion;

/// <summary>
/// Entry-point for triggering email synchronization operations.
/// </summary>
public interface IEmailSyncService
{
    /// <summary>
    /// Queues a sync for the specified source, triggered manually.
    /// Returns 202 Accepted if queued; 409 if already running; 400/404 for invalid/unknown source.
    /// </summary>
    Task<SyncRequestResult> RequestSyncAsync(
        Guid tenantId,
        Guid emailSourceId,
        Guid? actorId,
        string? correlationId,
        CancellationToken ct = default);

    /// <summary>
    /// Executes synchronization for a source synchronously.
    /// Used by the manual sync request path (runs in-process, not background).
    /// </summary>
    Task<SyncExecutionResult> ExecuteSyncAsync(
        Guid tenantId,
        Guid emailSourceId,
        IngestionRunTriggerType triggerType,
        Guid? actorId,
        string? correlationId,
        CancellationToken ct = default);

    /// <summary>
    /// Resets the cursor for a source.
    /// Requires EmailManage permission (enforced at API layer).
    /// The next sync will perform a full initial synchronization.
    /// </summary>
    Task<SyncResetResult> ResetSyncAsync(
        Guid tenantId,
        Guid emailSourceId,
        Guid? actorId,
        string? correlationId,
        CancellationToken ct = default);
}

public sealed record SyncRequestResult
{
    public required bool Accepted { get; init; }
    public required bool AlreadyRunning { get; init; }
    public required bool SourceNotFound { get; init; }
    public required bool SourceDisabled { get; init; }
    public required bool ModuleDisabled { get; init; }
    public Guid? RunId { get; init; }
    public string? SafeMessage { get; init; }

    public static SyncRequestResult Queued(Guid runId) =>
        new() { Accepted = true, AlreadyRunning = false, SourceNotFound = false, SourceDisabled = false, ModuleDisabled = false, RunId = runId };

    public static SyncRequestResult Conflict() =>
        new() { Accepted = false, AlreadyRunning = true, SourceNotFound = false, SourceDisabled = false, ModuleDisabled = false, SafeMessage = "Sync already in progress for this source." };

    public static SyncRequestResult NotFound() =>
        new() { Accepted = false, AlreadyRunning = false, SourceNotFound = true, SourceDisabled = false, ModuleDisabled = false };

    public static SyncRequestResult Disabled(string reason) =>
        new() { Accepted = false, AlreadyRunning = false, SourceNotFound = false, SourceDisabled = true, ModuleDisabled = false, SafeMessage = reason };
}

public sealed record SyncExecutionResult
{
    public required bool Success { get; init; }
    public Guid? RunId { get; init; }
    public string? ErrorCode { get; init; }
    public string? SafeErrorSummary { get; init; }

    // Counters
    public int MessagesImported { get; init; }
    public int MessagesUpdated { get; init; }
    public int MessagesDuplicated { get; init; }
    public int MessagesFailed { get; init; }
    public int AttachmentsDispatched { get; init; }
    public int AttachmentsFailed { get; init; }
    public int PagesProcessed { get; init; }
}

public sealed record SyncResetResult
{
    public required bool Success { get; init; }
    public required bool SourceNotFound { get; init; }
    public string? SafeMessage { get; init; }
}
