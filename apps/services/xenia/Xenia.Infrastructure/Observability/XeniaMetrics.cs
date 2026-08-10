using System.Diagnostics.Metrics;

namespace Xenia.Infrastructure.Observability;

/// <summary>
/// Phase B — Enterprise observability via System.Diagnostics.Metrics.
///
/// Uses the .NET built-in Meter API so metrics are automatically compatible with
/// OpenTelemetry, Prometheus (via prometheus-net), and any future APM integration.
///
/// Meter name: "Xenia" — matches the service name in any OTel collector config.
///
/// Design rules:
/// - All counters monotonically increase; never reset in process.
/// - Tag names use lowercase_snake_case for Prometheus compatibility.
/// - No secrets, tokens, message bodies, or attachment filenames in tag values.
/// - Tenant IDs are only included when explicitly configured (TenantTagging = true).
/// </summary>
public sealed class XeniaMetrics : IDisposable
{
    public const string MeterName = "Xenia";
    public const string Version   = "1.0.0";

    private readonly Meter _meter;

    // ── Email sync counters ────────────────────────────────────────
    public Counter<long> SyncRequests { get; }
    public Counter<long> SyncSucceeded { get; }
    public Counter<long> SyncFailed { get; }
    public Counter<long> SyncCancelled { get; }
    public Counter<long> SyncCursorInvalidated { get; }

    // ── Message counters ───────────────────────────────────────────
    public Counter<long> MessagesImported { get; }
    public Counter<long> MessagesDuplicated { get; }
    public Counter<long> MessagesFailed { get; }
    public Counter<long> MessagesUpdated { get; }

    // ── Attachment counters ────────────────────────────────────────
    public Counter<long> AttachmentsDispatched { get; }
    public Counter<long> AttachmentsFailed { get; }
    public Counter<long> AttachmentsRetryQueued { get; }

    // ── Automation counters ────────────────────────────────────────
    public Counter<long> AutomationExecutionsQueued { get; }
    public Counter<long> AutomationExecutionsCompleted { get; }
    public Counter<long> AutomationExecutionsFailed { get; }
    public Counter<long> AutomationExecutionsCancelled { get; }
    public Counter<long> AutomationDeadLettered { get; }
    public Counter<long> AutomationRegistrations { get; }

    // ── Assistant counters ──────────────────────────────────────────
    public Counter<long> AssistantConversationsCreated { get; }
    public Counter<long> AssistantRequestsCompleted { get; }
    public Counter<long> AssistantRequestsFailed { get; }
    public Counter<long> AssistantTokens { get; }

    // ── Alert counters ─────────────────────────────────────────────
    public Counter<long> AlertsOpened { get; }
    public Counter<long> AlertsResolved { get; }

    // ── Histograms ─────────────────────────────────────────────────
    public Histogram<double> SyncDurationMs { get; }
    public Histogram<double> AttachmentDispatchDurationMs { get; }
    public Histogram<double> AutomationExecutionDurationMs { get; }
    public Histogram<double> AssistantResponseDurationMs { get; }
    public Histogram<long> MessagesPerSyncRun { get; }
    public Histogram<long> PagesPerSyncRun { get; }

    // ── Gauges (ObservableGauge) ───────────────────────────────────
    public ObservableGauge<int> ActiveSyncLocks { get; }
    public ObservableGauge<int> ActiveAutomationExecutions { get; }
    public ObservableGauge<int> DeadLetterQueueDepth { get; }

    private int _activeSyncLocks;
    private int _activeAutomationExecutions;
    private int _deadLetterQueueDepth;

    public XeniaMetrics(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create(MeterName, Version);

        SyncRequests          = _meter.CreateCounter<long>("xenia.email.sync.requests.total", "requests", "Total email sync requests received.");
        SyncSucceeded         = _meter.CreateCounter<long>("xenia.email.sync.succeeded.total", "syncs", "Completed sync runs with no failures.");
        SyncFailed            = _meter.CreateCounter<long>("xenia.email.sync.failed.total", "syncs", "Sync runs that failed entirely.");
        SyncCancelled         = _meter.CreateCounter<long>("xenia.email.sync.cancelled.total", "syncs", "Sync runs cancelled by operator.");
        SyncCursorInvalidated = _meter.CreateCounter<long>("xenia.email.sync.cursor_invalidated.total", "events", "Cursor invalidation events.");

        MessagesImported  = _meter.CreateCounter<long>("xenia.email.messages.imported.total", "messages", "Messages successfully imported.");
        MessagesDuplicated = _meter.CreateCounter<long>("xenia.email.messages.duplicated.total", "messages", "Messages identified as duplicates.");
        MessagesFailed    = _meter.CreateCounter<long>("xenia.email.messages.failed.total", "messages", "Messages that failed to import.");
        MessagesUpdated   = _meter.CreateCounter<long>("xenia.email.messages.updated.total", "messages", "Messages updated after re-import.");

        AttachmentsDispatched = _meter.CreateCounter<long>("xenia.email.attachments.dispatched.total", "attachments", "Attachments successfully dispatched.");
        AttachmentsFailed     = _meter.CreateCounter<long>("xenia.email.attachments.failed.total", "attachments", "Attachment dispatch failures.");
        AttachmentsRetryQueued = _meter.CreateCounter<long>("xenia.email.attachments.retry_queued.total", "attachments", "Attachments queued for retry.");

        AutomationExecutionsQueued    = _meter.CreateCounter<long>("xenia.automation.executions.queued.total", "executions");
        AutomationExecutionsCompleted = _meter.CreateCounter<long>("xenia.automation.executions.completed.total", "executions");
        AutomationExecutionsFailed    = _meter.CreateCounter<long>("xenia.automation.executions.failed.total", "executions");
        AutomationExecutionsCancelled = _meter.CreateCounter<long>("xenia.automation.executions.cancelled.total", "executions");
        AutomationDeadLettered        = _meter.CreateCounter<long>("xenia.automation.executions.dead_lettered.total", "executions");
        AutomationRegistrations       = _meter.CreateCounter<long>("xenia.automation.registrations.total", "automations");

        AssistantConversationsCreated = _meter.CreateCounter<long>("xenia.assistant.conversations.created.total", "conversations");
        AssistantRequestsCompleted    = _meter.CreateCounter<long>("xenia.assistant.requests.completed.total", "requests");
        AssistantRequestsFailed       = _meter.CreateCounter<long>("xenia.assistant.requests.failed.total", "requests");
        AssistantTokens               = _meter.CreateCounter<long>("xenia.assistant.tokens.total", "tokens");

        AlertsOpened   = _meter.CreateCounter<long>("xenia.email.alerts.opened.total", "alerts");
        AlertsResolved = _meter.CreateCounter<long>("xenia.email.alerts.resolved.total", "alerts");

        SyncDurationMs               = _meter.CreateHistogram<double>("xenia.email.sync.duration.ms", "ms", "Email sync run duration.");
        AttachmentDispatchDurationMs = _meter.CreateHistogram<double>("xenia.email.attachment.dispatch.duration.ms", "ms", "Attachment dispatch duration.");
        AutomationExecutionDurationMs = _meter.CreateHistogram<double>("xenia.automation.execution.duration.ms", "ms", "Automation execution duration.");
        AssistantResponseDurationMs   = _meter.CreateHistogram<double>("xenia.assistant.response.duration.ms", "ms", "Assistant provider response duration.");
        MessagesPerSyncRun           = _meter.CreateHistogram<long>("xenia.email.sync.messages_per_run", "messages", "Messages processed per sync run.");
        PagesPerSyncRun              = _meter.CreateHistogram<long>("xenia.email.sync.pages_per_run", "pages", "Pages fetched per sync run.");

        ActiveSyncLocks            = _meter.CreateObservableGauge("xenia.email.sync.active_locks", () => _activeSyncLocks, "locks", "Currently held sync locks.");
        ActiveAutomationExecutions = _meter.CreateObservableGauge("xenia.automation.active_executions", () => _activeAutomationExecutions, "executions", "Automation executions in progress.");
        DeadLetterQueueDepth       = _meter.CreateObservableGauge("xenia.automation.dlq.depth", () => _deadLetterQueueDepth, "entries", "Open dead-letter entries.");
    }

    public void RecordActiveSyncLocks(int count) => _activeSyncLocks = count;
    public void RecordActiveAutomationExecutions(int count) => _activeAutomationExecutions = count;
    public void RecordDeadLetterQueueDepth(int count) => _deadLetterQueueDepth = count;

    public void Dispose() => _meter.Dispose();
}
