namespace Xenia.Domain.Email;

/// <summary>
/// Records a single execution of the ingestion engine for one Email source.
///
/// Tracks all counters, timing, and result metadata.
/// NEVER stores: message bodies, raw credentials, raw tokens, raw provider exceptions,
/// full sensitive cursors.
/// </summary>
public sealed class EmailIngestionRun
{
    public const int CorrelationIdMaxLength   = 200;
    public const int WorkerInstanceIdMaxLength= 200;
    public const int ErrorCodeMaxLength       = 100;
    public const int SafeErrorSummaryMaxLength= 500;
    public const int CursorSummaryMaxLength   = 200;

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid EmailSourceId { get; private set; }

    public IngestionRunTriggerType TriggerType { get; private set; }
    public IngestionRunStatus Status { get; private set; }

    public DateTime StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public long? DurationMs { get; private set; }

    public string? CorrelationId { get; private set; }

    /// <summary>Actor who triggered this run (null for scheduled/background).</summary>
    public Guid? ActorId { get; private set; }

    /// <summary>Identity of the worker/process instance that executed this run.</summary>
    public string? WorkerInstanceId { get; private set; }

    // ── Counters ──────────────────────────────────────────────────────────────

    public int MessagesDiscovered  { get; private set; }
    public int MessagesImported    { get; private set; }
    public int MessagesUpdated     { get; private set; }
    public int MessagesDuplicated  { get; private set; }
    public int MessagesFailed      { get; private set; }

    public int AttachmentsDiscovered { get; private set; }
    public int AttachmentsDispatched { get; private set; }
    public int AttachmentsFailed     { get; private set; }

    public int PagesProcessed { get; private set; }
    public int RetryCount     { get; private set; }

    // ── Cursor summaries (safe — no raw tokens) ───────────────────────────────

    public string? CursorBeforeSafeSummary { get; private set; }
    public string? CursorAfterSafeSummary  { get; private set; }

    // ── Error ─────────────────────────────────────────────────────────────────

    public string? ErrorCode { get; private set; }
    public string? SafeErrorSummary { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    /// <summary>Run ID of the original run this is a retry of (null if not a retry).</summary>
    public Guid? RetryOfRunId { get; private set; }

    private EmailIngestionRun() { }

    /// <summary>
    /// Creates a retry run for a previously failed or completed-with-errors run.
    /// The new run is queued with trigger type Retry and linked to the original.
    /// </summary>
    public static EmailIngestionRun CreateRetry(
        Guid tenantId,
        Guid emailSourceId,
        Guid originalRunId,
        Guid? actorId,
        string? correlationId)
    {
        return new EmailIngestionRun
        {
            Id               = Guid.CreateVersion7(),
            TenantId         = tenantId,
            EmailSourceId    = emailSourceId,
            TriggerType      = IngestionRunTriggerType.Manual,
            Status           = IngestionRunStatus.Queued,
            StartedAt        = DateTime.UtcNow,
            CorrelationId    = correlationId,
            ActorId          = actorId,
            RetryOfRunId     = originalRunId,
            CreatedAtUtc     = DateTime.UtcNow,
            UpdatedAtUtc     = DateTime.UtcNow,
        };
    }

    public static EmailIngestionRun Create(
        Guid tenantId,
        Guid emailSourceId,
        IngestionRunTriggerType triggerType,
        string? correlationId,
        Guid? actorId,
        string? workerInstanceId,
        string? cursorBeforeSafeSummary)
    {
        return new EmailIngestionRun
        {
            Id                       = Guid.CreateVersion7(),
            TenantId                 = tenantId,
            EmailSourceId            = emailSourceId,
            TriggerType              = triggerType,
            Status                   = IngestionRunStatus.Queued,
            StartedAt                = DateTime.UtcNow,
            CorrelationId            = correlationId,
            ActorId                  = actorId,
            WorkerInstanceId         = workerInstanceId,
            CursorBeforeSafeSummary  = cursorBeforeSafeSummary,
            CreatedAtUtc             = DateTime.UtcNow,
            UpdatedAtUtc             = DateTime.UtcNow,
        };
    }

    public void MarkRunning()
    {
        Status       = IngestionRunStatus.Running;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void IncrementPage() => PagesProcessed++;
    public void IncrementRetry() => RetryCount++;

    public void AddDiscovered(int count) => MessagesDiscovered += count;
    public void AddImported(int count) => MessagesImported += count;
    public void AddUpdated(int count) => MessagesUpdated += count;
    public void AddDuplicated(int count) => MessagesDuplicated += count;
    public void AddFailed(int count) => MessagesFailed += count;

    public void AddAttachmentsDiscovered(int count) => AttachmentsDiscovered += count;
    public void AddAttachmentsDispatched(int count) => AttachmentsDispatched += count;
    public void AddAttachmentsFailed(int count) => AttachmentsFailed += count;

    public void Complete(string? cursorAfterSafeSummary)
    {
        var hasErrors = MessagesFailed > 0 || AttachmentsFailed > 0;
        Status                  = hasErrors ? IngestionRunStatus.CompletedWithErrors : IngestionRunStatus.Completed;
        CompletedAt             = DateTime.UtcNow;
        DurationMs              = (long)(DateTime.UtcNow - StartedAt).TotalMilliseconds;
        CursorAfterSafeSummary  = cursorAfterSafeSummary;
        UpdatedAtUtc            = DateTime.UtcNow;
    }

    public void Fail(string errorCode, string safeErrorSummary)
    {
        Status          = IngestionRunStatus.Failed;
        CompletedAt     = DateTime.UtcNow;
        DurationMs      = (long)(DateTime.UtcNow - StartedAt).TotalMilliseconds;
        ErrorCode       = errorCode;
        SafeErrorSummary= safeErrorSummary;
        UpdatedAtUtc    = DateTime.UtcNow;
    }

    public void Cancel()
    {
        Status       = IngestionRunStatus.Cancelled;
        CompletedAt  = DateTime.UtcNow;
        DurationMs   = (long)(DateTime.UtcNow - StartedAt).TotalMilliseconds;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Interrupt()
    {
        Status       = IngestionRunStatus.Interrupted;
        CompletedAt  = DateTime.UtcNow;
        DurationMs   = (long)(DateTime.UtcNow - StartedAt).TotalMilliseconds;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public bool IsTerminal => Status is
        IngestionRunStatus.Completed or
        IngestionRunStatus.CompletedWithErrors or
        IngestionRunStatus.Failed or
        IngestionRunStatus.Cancelled or
        IngestionRunStatus.Interrupted;
}
