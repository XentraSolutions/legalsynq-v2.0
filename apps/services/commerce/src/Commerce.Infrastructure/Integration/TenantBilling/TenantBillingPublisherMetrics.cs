using System.Diagnostics.Metrics;

namespace Commerce.Infrastructure.Integration.TenantBilling;

/// <summary>
/// TB-INT-02 — counters for the entitlement publisher exposed via the
/// .NET <see cref="System.Diagnostics.Metrics.Meter"/> API. The host
/// process's existing OpenTelemetry metrics provider can pick these
/// up by adding <see cref="MeterName"/> to its meter list.
///
/// <para>Registered as a singleton so all attempts increment the same
/// counter regardless of which transient typed-client instance the
/// publisher is resolved into.</para>
/// </summary>
public sealed class TenantBillingPublisherMetrics : IDisposable
{
    public const string MeterName = "Commerce.TenantBilling.Publisher";

    private readonly Meter _meter;
    private readonly Counter<long> _attempts;
    private readonly Counter<long> _published;
    private readonly Counter<long> _skipped;
    private readonly Counter<long> _failed;
    private readonly Counter<long> _retries;

    // TB-INT-03 — auto-publish counters.
    private readonly Counter<long> _autoEnqueued;
    private readonly Counter<long> _autoDropped;
    private readonly Counter<long> _autoProcessed;
    private readonly Counter<long> _autoFailed;

    // TB-INT-04 — durable outbox counters.
    private readonly Counter<long> _outboxEnqueued;
    private readonly Counter<long> _outboxEnqueueFailed;
    private readonly Counter<long> _outboxProcessed;
    private readonly Counter<long> _outboxPublished;
    private readonly Counter<long> _outboxFailed;
    private readonly Counter<long> _outboxAbandoned;
    private readonly Counter<long> _outboxRetried;

    public TenantBillingPublisherMetrics()
    {
        _meter = new Meter(MeterName, "1.0.0");
        _attempts = _meter.CreateCounter<long>(
            "commerce.tenant_billing.publish.attempts",
            description: "Wire attempts to Tenant Billing apply (incl. retries).");
        _published = _meter.CreateCounter<long>(
            "commerce.tenant_billing.publish.published",
            description: "Final outcome=Published.");
        _skipped = _meter.CreateCounter<long>(
            "commerce.tenant_billing.publish.skipped",
            description: "Final outcome=Skipped.");
        _failed = _meter.CreateCounter<long>(
            "commerce.tenant_billing.publish.failed",
            description: "Final outcome=Failed.");
        _retries = _meter.CreateCounter<long>(
            "commerce.tenant_billing.publish.retry_attempts",
            description: "Retry attempts (i.e. attempts 2..N).");

        _autoEnqueued = _meter.CreateCounter<long>(
            "commerce.tenant_billing.autopublish.enqueued",
            description: "Auto-publish work items accepted onto the queue.");
        _autoDropped = _meter.CreateCounter<long>(
            "commerce.tenant_billing.autopublish.dropped",
            description: "Auto-publish enqueue attempts dropped (queue full or disabled).");
        _autoProcessed = _meter.CreateCounter<long>(
            "commerce.tenant_billing.autopublish.processed",
            description: "Auto-publish work items processed by the worker (any outcome).");
        _autoFailed = _meter.CreateCounter<long>(
            "commerce.tenant_billing.autopublish.failed",
            description: "Auto-publish work items whose publisher call failed or threw.");

        _outboxEnqueued = _meter.CreateCounter<long>(
            "commerce.tenant_billing.outbox.enqueued",
            description: "Outbox rows persisted by the trigger sites.");
        _outboxEnqueueFailed = _meter.CreateCounter<long>(
            "commerce.tenant_billing.outbox.enqueue_failed",
            description: "Outbox enqueue attempts that failed (invalid input or persistence exception).");
        _outboxProcessed = _meter.CreateCounter<long>(
            "commerce.tenant_billing.outbox.processed",
            description: "Outbox rows processed by the worker (any outcome).");
        _outboxPublished = _meter.CreateCounter<long>(
            "commerce.tenant_billing.outbox.published",
            description: "Outbox rows that reached terminal state Published.");
        _outboxFailed = _meter.CreateCounter<long>(
            "commerce.tenant_billing.outbox.failed",
            description: "Outbox rows whose publisher call failed/threw on a single attempt.");
        _outboxAbandoned = _meter.CreateCounter<long>(
            "commerce.tenant_billing.outbox.abandoned",
            description: "Outbox rows that reached terminal state Abandoned.");
        _outboxRetried = _meter.CreateCounter<long>(
            "commerce.tenant_billing.outbox.retried",
            description: "Outbox rows that scheduled a retry after a failed attempt.");
    }

    public void RecordAttempt(int attemptNumber, int? httpStatus)
    {
        _attempts.Add(1, BuildTags(reason: null, httpStatus));
        if (attemptNumber > 1)
        {
            _retries.Add(1, BuildTags(reason: null, httpStatus));
        }
    }

    public void RecordOutcome(string outcome, string reason, int? httpStatus)
    {
        var tags = BuildTags(reason, httpStatus);
        switch (outcome)
        {
            case "published": _published.Add(1, tags); break;
            case "skipped":   _skipped.Add(1, tags); break;
            case "failed":    _failed.Add(1, tags); break;
        }
    }

    private static KeyValuePair<string, object?>[] BuildTags(
        string? reason, int? httpStatus)
    {
        if (reason is null && httpStatus is null)
            return Array.Empty<KeyValuePair<string, object?>>();

        var list = new List<KeyValuePair<string, object?>>(2);
        if (reason is not null)
            list.Add(new("reason", reason));
        if (httpStatus is not null)
            list.Add(new("http_status", httpStatus.Value));
        return list.ToArray();
    }

    // ───────── TB-INT-03 auto-publish recorders ─────────

    public void RecordAutoPublishEnqueued(string triggerSource)
        => _autoEnqueued.Add(1,
            new KeyValuePair<string, object?>("trigger_source", triggerSource));

    public void RecordAutoPublishDropped(string triggerSource, string reason)
        => _autoDropped.Add(1,
            new KeyValuePair<string, object?>("trigger_source", triggerSource),
            new KeyValuePair<string, object?>("reason", reason));

    public void RecordAutoPublishProcessed(string triggerSource, string outcome, string reason)
        => _autoProcessed.Add(1,
            new KeyValuePair<string, object?>("trigger_source", triggerSource),
            new KeyValuePair<string, object?>("outcome", outcome),
            new KeyValuePair<string, object?>("reason", reason));

    public void RecordAutoPublishFailed(string triggerSource, string reason)
        => _autoFailed.Add(1,
            new KeyValuePair<string, object?>("trigger_source", triggerSource),
            new KeyValuePair<string, object?>("reason", reason));

    // ───────── TB-INT-04 outbox recorders ─────────

    public void RecordOutboxEnqueued(string triggerSource)
        => _outboxEnqueued.Add(1,
            new KeyValuePair<string, object?>("trigger_source", triggerSource));

    public void RecordOutboxEnqueueFailed(string triggerSource, string reason)
        => _outboxEnqueueFailed.Add(1,
            new KeyValuePair<string, object?>("trigger_source", triggerSource),
            new KeyValuePair<string, object?>("reason", reason));

    public void RecordOutboxProcessed(string triggerSource, string outcome, string reason)
        => _outboxProcessed.Add(1,
            new KeyValuePair<string, object?>("trigger_source", triggerSource),
            new KeyValuePair<string, object?>("outcome", outcome),
            new KeyValuePair<string, object?>("reason", reason));

    public void RecordOutboxPublished(string triggerSource)
        => _outboxPublished.Add(1,
            new KeyValuePair<string, object?>("trigger_source", triggerSource));

    public void RecordOutboxFailed(string triggerSource, string reason)
        => _outboxFailed.Add(1,
            new KeyValuePair<string, object?>("trigger_source", triggerSource),
            new KeyValuePair<string, object?>("reason", reason));

    public void RecordOutboxAbandoned(string triggerSource, string reason)
        => _outboxAbandoned.Add(1,
            new KeyValuePair<string, object?>("trigger_source", triggerSource),
            new KeyValuePair<string, object?>("reason", reason));

    public void RecordOutboxRetried(string triggerSource)
        => _outboxRetried.Add(1,
            new KeyValuePair<string, object?>("trigger_source", triggerSource));

    public void Dispose() => _meter.Dispose();
}
