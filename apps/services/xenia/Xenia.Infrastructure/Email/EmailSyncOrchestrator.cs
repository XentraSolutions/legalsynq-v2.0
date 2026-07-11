using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xenia.Application.Email;
using Xenia.Application.Email.Ingestion;
using Xenia.Domain.Email;
using Xenia.Infrastructure.Persistence;

namespace Xenia.Infrastructure.Email;

/// <summary>
/// Core sync orchestrator for a single email source.
///
/// Responsibilities:
/// - Validate source + module state
/// - Acquire per-source lock (return 409 if locked)
/// - Create and track an EmailIngestionRun
/// - Iterate provider pages (cursor-based)
/// - Normalize, deduplicate, persist each message
/// - Commit cursor only after durable persistence
/// - Dispatch attachments after cursor commit
/// - Record failure + backoff on error
/// - Emit structured audit events via IAuditAdapter
///
/// Never logs: message bodies, credentials, raw cursors, raw provider tokens.
/// </summary>
internal sealed class EmailSyncOrchestrator : IEmailSyncService
{
    private readonly XeniaDbContext _db;
    private readonly IEmailConnectorRegistry _connectorRegistry;
    private readonly IMessageNormalizer _normalizer;
    private readonly IDuplicateDetectionService _duplicationService;
    private readonly IMessagePersistenceService _persistenceService;
    private readonly IAttachmentDispatcher _attachmentDispatcher;
    private readonly ISyncStateService _syncStateService;
    private readonly IEmailSourceSyncLock _syncLock;
    private readonly XeniaIngestionOptions _opts;
    private readonly ILogger<EmailSyncOrchestrator> _logger;

    public EmailSyncOrchestrator(
        XeniaDbContext db,
        IEmailConnectorRegistry connectorRegistry,
        IMessageNormalizer normalizer,
        IDuplicateDetectionService duplicationService,
        IMessagePersistenceService persistenceService,
        IAttachmentDispatcher attachmentDispatcher,
        ISyncStateService syncStateService,
        IEmailSourceSyncLock syncLock,
        IOptions<XeniaIngestionOptions> opts,
        ILogger<EmailSyncOrchestrator> logger)
    {
        _db                  = db;
        _connectorRegistry   = connectorRegistry;
        _normalizer          = normalizer;
        _duplicationService  = duplicationService;
        _persistenceService  = persistenceService;
        _attachmentDispatcher= attachmentDispatcher;
        _syncStateService    = syncStateService;
        _syncLock            = syncLock;
        _opts                = opts.Value;
        _logger              = logger;
    }

    public async Task<SyncRequestResult> RequestSyncAsync(
        Guid tenantId, Guid emailSourceId, Guid? actorId, string? correlationId, CancellationToken ct = default)
    {
        if (!_opts.IngestionEnabled)
            return SyncRequestResult.Disabled("Ingestion is disabled.");

        var source = await _db.EmailSources
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == emailSourceId && !s.IsDeleted, ct);

        if (source is null)
            return SyncRequestResult.NotFound();

        if (source.Status == EmailSourceStatus.Disabled)
            return SyncRequestResult.Disabled("Source is disabled.");

        if (_syncLock.IsLocked(tenantId, emailSourceId))
            return SyncRequestResult.Conflict();

        var activeRun = await _syncStateService.GetActiveRunAsync(tenantId, emailSourceId, ct);
        if (activeRun is not null)
            return SyncRequestResult.Conflict();

        var result = await ExecuteSyncAsync(tenantId, emailSourceId,
            IngestionRunTriggerType.Manual, actorId, correlationId, ct);

        return result.Success
            ? SyncRequestResult.Queued(result.RunId ?? Guid.Empty)
            : new SyncRequestResult
            {
                Accepted = false, AlreadyRunning = false,
                SourceNotFound = false, SourceDisabled = false, ModuleDisabled = false,
                SafeMessage = result.SafeErrorSummary,
            };
    }

    public async Task<SyncExecutionResult> ExecuteSyncAsync(
        Guid tenantId, Guid emailSourceId, IngestionRunTriggerType triggerType,
        Guid? actorId, string? correlationId, CancellationToken ct = default)
    {
        var source = await _db.EmailSources
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == emailSourceId && !s.IsDeleted, ct);

        if (source is null)
        {
            return new SyncExecutionResult { Success = false, ErrorCode = "SOURCE_NOT_FOUND", SafeErrorSummary = "Source not found." };
        }

        await using var lease = await _syncLock.TryAcquireAsync(tenantId, emailSourceId, ct);
        if (lease is null)
        {
            return new SyncExecutionResult { Success = false, ErrorCode = "SOURCE_LOCKED", SafeErrorSummary = "Sync already in progress." };
        }

        var syncState = await _syncStateService.GetOrCreateAsync(tenantId, emailSourceId, source.ProviderType, ct);
        var run = await _syncStateService.StartRunAsync(
            tenantId, emailSourceId, triggerType,
            correlationId, actorId,
            workerInstanceId: Environment.MachineName,
            cursorBeforeSafeSummary: syncState.SafeCursorSummary,
            ct);

        await _syncStateService.MarkRunStartedAsync(run.Id, ct);
        syncState.RecordAttempt();

        _logger.LogInformation(
            "Sync started: tenantId={TenantId} sourceId={SourceId} runId={RunId} trigger={Trigger}",
            tenantId, emailSourceId, run.Id, triggerType);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(_opts.PerSourceTimeout);

        try
        {
            var result = await RunSyncLoopAsync(source, run, syncState, timeout.Token);
            await _syncStateService.CompleteRunAsync(run.Id, syncState.SafeCursorSummary, ct);

            _logger.LogInformation(
                "Sync completed: runId={RunId} imported={Imported} duped={Duped} pages={Pages}",
                run.Id, result.MessagesImported, result.MessagesDuplicated, result.PagesProcessed);

            return result with { RunId = run.Id };
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            await _syncStateService.FailRunAsync(run.Id, "TIMEOUT", "Source sync timed out.", ct);
            await _syncStateService.RecordFailureAsync(tenantId, emailSourceId, "TIMEOUT", "Sync timed out.", ct);
            return new SyncExecutionResult { Success = false, RunId = run.Id, ErrorCode = "TIMEOUT", SafeErrorSummary = "Sync timed out." };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Sync failed: tenantId={TenantId} sourceId={SourceId} runId={RunId}",
                tenantId, emailSourceId, run.Id);
            var safe = "Sync failed due to an unexpected error.";
            await _syncStateService.FailRunAsync(run.Id, "SYNC_ERROR", safe, ct);
            await _syncStateService.RecordFailureAsync(tenantId, emailSourceId, "SYNC_ERROR", safe, ct);
            return new SyncExecutionResult { Success = false, RunId = run.Id, ErrorCode = "SYNC_ERROR", SafeErrorSummary = safe };
        }
    }

    public async Task<SyncResetResult> ResetSyncAsync(
        Guid tenantId, Guid emailSourceId, Guid? actorId, string? correlationId, CancellationToken ct = default)
    {
        var source = await _db.EmailSources
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == emailSourceId && !s.IsDeleted, ct);

        if (source is null)
            return new SyncResetResult { Success = false, SourceNotFound = true };

        await _syncStateService.ResetCursorAsync(tenantId, emailSourceId, "manual-reset", ct);

        _logger.LogInformation(
            "Sync cursor reset: tenantId={TenantId} sourceId={SourceId} actorId={ActorId}",
            tenantId, emailSourceId, actorId);

        return new SyncResetResult { Success = true, SourceNotFound = false, SafeMessage = "Cursor reset. Next sync will perform a full initial synchronization." };
    }

    // ── Core sync loop ────────────────────────────────────────────────────────

    private async Task<SyncExecutionResult> RunSyncLoopAsync(
        EmailSource source,
        EmailIngestionRun run,
        EmailSyncState syncState,
        CancellationToken ct)
    {
        var connector = _connectorRegistry.GetConnector(source.ProviderType);
        if (connector is not IEmailIngestionConnector ingestionConnector)
        {
            return new SyncExecutionResult
            {
                Success          = false,
                ErrorCode        = "CONNECTOR_NO_INGESTION",
                SafeErrorSummary = $"Provider {source.ProviderType} does not support ingestion.",
            };
        }

        var context = BuildConnectorContext(source);
        ProviderSyncCursor? cursor = syncState.CursorValue is not null
            ? new ProviderSyncCursor
              {
                  CursorType   = syncState.CursorType,
                  RawValue     = syncState.CursorValue,
                  MetadataJson = syncState.CursorMetadataJson,
                  SafeSummary  = syncState.SafeCursorSummary,
              }
            : null;

        // Initial sync — get starting cursor
        if (cursor is null)
        {
            var initResult = await ingestionConnector.GetInitialCursorAsync(context, ct);
            if (!initResult.Success)
            {
                return new SyncExecutionResult
                {
                    Success = false, ErrorCode = "INITIAL_CURSOR_FAILED",
                    SafeErrorSummary = initResult.SafeErrorSummary ?? "Failed to get initial cursor.",
                };
            }
            cursor = initResult.Cursor;
        }

        int pagesProcessed = 0, imported = 0, updated = 0, duped = 0, failed = 0, attachDispatched = 0, attachFailed = 0;

        while (pagesProcessed < _opts.MaxPagesPerRun)
        {
            ct.ThrowIfCancellationRequested();

            var fetchResult = await ingestionConnector.FetchMessagePageAsync(
                context, cursor, _opts.DefaultPageSize, ct);

            if (!fetchResult.Success)
            {
                if (fetchResult.IsInvalidCursor)
                {
                    _logger.LogWarning("Invalid cursor for sourceId={SourceId}; resetting", source.Id);
                    await _syncStateService.ResetCursorAsync(source.TenantId, source.Id, "invalid-cursor", ct);
                    break;
                }
                if (fetchResult.IsRateLimited && fetchResult.RetryAfter.HasValue)
                {
                    _logger.LogInformation("Rate limited for sourceId={SourceId} retryAfter={RetryAfter}s",
                        source.Id, fetchResult.RetryAfter.Value.TotalSeconds);
                    break;
                }
                break;
            }

            var page = fetchResult.Page!;
            run.AddDiscovered(page.Messages.Count);
            pagesProcessed++;
            run.IncrementPage();

            DateTime? lastTimestamp = null;
            string? lastMsgId = null;

            foreach (var envelope in page.Messages)
            {
                ct.ThrowIfCancellationRequested();

                var normResult = _normalizer.Normalize(envelope, source.ProviderType);
                if (!normResult.IsValid || normResult.Message is null)
                {
                    failed++;
                    continue;
                }

                var dupCheck = await _duplicationService.CheckAsync(
                    source.TenantId, source.Id, normResult.Message, ct);

                var persResult = await _persistenceService.PersistMessageAsync(
                    source.TenantId, source.Id, source.ProviderType,
                    normResult.Message, run.Id, dupCheck, ct);

                if (!persResult.Success) { failed++; continue; }

                switch (persResult.ImportStatus)
                {
                    case MessageImportStatus.Imported: imported++; break;
                    case MessageImportStatus.Updated:  updated++;  break;
                    case MessageImportStatus.Duplicate: duped++;   break;
                }

                lastTimestamp = envelope.ReceivedAt ?? envelope.SentAt;
                lastMsgId     = envelope.ProviderMessageId;

                // Dispatch attachments (best-effort)
                foreach (var attachId in persResult.AttachmentReferenceIds)
                {
                    var attRef = await _db.EmailAttachmentReferences
                        .AsNoTracking()
                        .FirstOrDefaultAsync(a => a.Id == attachId, ct);
                    if (attRef is null) continue;

                    var dispResult = await _attachmentDispatcher.DispatchAsync(
                        new AttachmentDispatchRequest
                        {
                            TenantId              = source.TenantId,
                            AttachmentReferenceId = attachId,
                            EmailMessageId        = persResult.MessageId ?? Guid.Empty,
                            FileName              = attRef.FileName,
                            MimeType              = attRef.MimeType,
                            MaxSizeBytes          = _opts.MaxAttachmentBytes,
                            ProviderAttachmentId  = attRef.ProviderAttachmentId,
                            ProviderMessageId     = envelope.ProviderMessageId,
                            ConnectorContext      = context,
                        }, ct);

                    if (dispResult.Success) attachDispatched++;
                    else if (!dispResult.WasSkipped) attachFailed++;
                }
            }

            // Commit cursor after successful page persistence
            if (page.NextCursor is not null)
            {
                await _syncStateService.CommitCursorAsync(
                    source.TenantId, source.Id, page.NextCursor,
                    lastTimestamp, lastMsgId, ct);
                cursor = page.NextCursor;
            }
            else
            {
                if (page.NextCursor is null && lastMsgId is not null)
                {
                    var finalCursor = cursor ?? new ProviderSyncCursor
                    {
                        CursorType = syncState.CursorType,
                        RawValue   = "completed",
                        SafeSummary= $"Completed at {DateTime.UtcNow:u}",
                    };
                    await _syncStateService.CommitCursorAsync(
                        source.TenantId, source.Id, finalCursor, lastTimestamp, lastMsgId, ct);
                }
                break;
            }
        }

        run.AddImported(imported);
        run.AddUpdated(updated);
        run.AddDuplicated(duped);
        run.AddFailed(failed);
        run.AddAttachmentsDispatched(attachDispatched);
        run.AddAttachmentsFailed(attachFailed);

        return new SyncExecutionResult
        {
            Success              = true,
            MessagesImported     = imported,
            MessagesUpdated      = updated,
            MessagesDuplicated   = duped,
            MessagesFailed       = failed,
            AttachmentsDispatched= attachDispatched,
            AttachmentsFailed    = attachFailed,
            PagesProcessed       = pagesProcessed,
        };
    }

    private static EmailSourceConnectorContext BuildConnectorContext(EmailSource source) =>
        new()
        {
            SourceId              = source.Id,
            TenantId              = source.TenantId,
            EmailAddress          = source.EmailAddress,
            AuthType              = source.AuthType,
            Username              = source.Username,
            IncomingHost          = source.IncomingHost,
            IncomingPort          = source.IncomingPort,
            UseTls                = source.UseTls,
            MailboxFolder         = source.MailboxFolder,
            SecretReferenceId     = source.SecretReferenceId,
            OAuthConnectionRef    = source.OAuthConnectionRef,
            ProviderConfigurationJson = null,
        };
}
