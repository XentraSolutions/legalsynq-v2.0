using Prometheus;

namespace Documents.Infrastructure.Observability;

/// <summary>
/// Prometheus metrics for the Redis dependency and scan completion notification subsystem.
/// Exposed at GET /metrics alongside ScanMetrics.
/// </summary>
public static class RedisMetrics
{
    // ── Redis health ──────────────────────────────────────────────────────────

    /// <summary>1 if Redis is reachable and responding to PING, 0 otherwise.</summary>
    public static readonly Gauge RedisHealthy = Metrics.CreateGauge(
        "docs_redis_healthy",
        "1 if the Redis server is reachable and responding to PING, 0 otherwise.");

    /// <summary>Total connection/command failures against Redis.</summary>
    public static readonly Counter RedisConnectionFailures = Metrics.CreateCounter(
        "docs_redis_connection_failures_total",
        "Total Redis connection or command failures recorded by the Documents service.");

    // ── Redis Streams (queue) ─────────────────────────────────────────────────

    /// <summary>
    /// Total stale scan jobs reclaimed from the Redis Stream PEL via XAUTOCLAIM.
    /// High values indicate workers are crashing before ACK.
    /// </summary>
    public static readonly Counter RedisStreamReclaims = Metrics.CreateCounter(
        "docs_redis_stream_reclaims_total",
        "Total scan jobs reclaimed from crashed consumers via XAUTOCLAIM.");

    // ── Scan completion notifications ─────────────────────────────────────────

    /// <summary>Total DocumentScanCompleted events emitted (regardless of delivery outcome).</summary>
    public static readonly Counter ScanCompletionEventsEmitted = Metrics.CreateCounter(
        "docs_scan_completion_events_emitted_total",
        "Total DocumentScanCompleted events emitted after final scan resolution.",
        new CounterConfiguration { LabelNames = new[] { "status" } });

    /// <summary>Total notification deliveries that succeeded.</summary>
    public static readonly Counter ScanCompletionDeliverySuccess = Metrics.CreateCounter(
        "docs_scan_completion_delivery_success_total",
        "Total DocumentScanCompleted events delivered successfully.");

    /// <summary>Total notification deliveries that failed (delivery error, not scan failure).</summary>
    public static readonly Counter ScanCompletionDeliveryFailures = Metrics.CreateCounter(
        "docs_scan_completion_delivery_failures_total",
        "Total DocumentScanCompleted event delivery failures (pipeline unaffected).");
}
