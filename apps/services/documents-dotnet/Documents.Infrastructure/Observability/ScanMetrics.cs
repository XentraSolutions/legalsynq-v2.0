using Prometheus;

namespace Documents.Infrastructure.Observability;

/// <summary>
/// Prometheus-compatible metrics for the document scan subsystem.
/// Exposed at GET /metrics (prometheus-net.AspNetCore).
/// </summary>
public static class ScanMetrics
{
    // ── Queue ─────────────────────────────────────────────────────────────────

    public static readonly Counter ScanJobsEnqueued = Metrics.CreateCounter(
        "docs_scan_jobs_enqueued_total",
        "Total scan jobs enqueued successfully.");

    public static readonly Counter ScanQueueSaturations = Metrics.CreateCounter(
        "docs_scan_queue_saturations_total",
        "Total times a scan job was rejected due to queue saturation.");

    public static readonly Gauge ScanQueueDepth = Metrics.CreateGauge(
        "docs_scan_queue_depth",
        "Current number of pending scan jobs in the queue.");

    // ── Scan lifecycle ────────────────────────────────────────────────────────

    public static readonly Counter ScanJobsStarted = Metrics.CreateCounter(
        "docs_scan_jobs_started_total",
        "Total scan jobs started by a worker.");

    public static readonly Counter ScanJobsClean = Metrics.CreateCounter(
        "docs_scan_jobs_clean_total",
        "Total scan jobs that completed with status CLEAN.");

    public static readonly Counter ScanJobsInfected = Metrics.CreateCounter(
        "docs_scan_jobs_infected_total",
        "Total scan jobs that detected malware (INFECTED).");

    public static readonly Counter ScanJobsFailed = Metrics.CreateCounter(
        "docs_scan_jobs_failed_total",
        "Total scan jobs that resulted in a FAILED status.");

    public static readonly Counter ScanJobsRetried = Metrics.CreateCounter(
        "docs_scan_jobs_retried_total",
        "Total scan job retry attempts (not counting initial attempt).");

    public static readonly Counter ScanAccessDenied = Metrics.CreateCounter(
        "docs_scan_access_denied_total",
        "Total file access attempts denied due to scan status.",
        new CounterConfiguration { LabelNames = new[] { "scan_status" } });

    // ── Scan duration ─────────────────────────────────────────────────────────

    public static readonly Histogram ScanDurationSeconds = Metrics.CreateHistogram(
        "docs_scan_duration_seconds",
        "ClamAV scan duration in seconds.",
        new HistogramConfiguration
        {
            Buckets = Histogram.LinearBuckets(start: 0.1, width: 0.5, count: 20),
        });

    // ── ClamAV health ─────────────────────────────────────────────────────────

    public static readonly Gauge ClamAvHealthy = Metrics.CreateGauge(
        "docs_clamav_healthy",
        "1 if ClamAV daemon is reachable and responding, 0 otherwise.");

    // ── Circuit breaker ───────────────────────────────────────────────────────

    /// <summary>Current circuit state: 0=closed (normal), 1=open (failing), 2=half-open (probing).</summary>
    public static readonly Gauge ClamAvCircuitState = Metrics.CreateGauge(
        "docs_clamav_circuit_state",
        "Current ClamAV circuit breaker state: 0=closed, 1=open, 2=half-open.");

    /// <summary>Total number of times the circuit has transitioned to OPEN.</summary>
    public static readonly Counter ClamAvCircuitOpenTotal = Metrics.CreateCounter(
        "docs_clamav_circuit_open_total",
        "Total times the ClamAV circuit breaker has opened due to repeated failures.");

    /// <summary>Total scan calls that were short-circuited because the circuit was OPEN.</summary>
    public static readonly Counter ClamAvCircuitShortCircuitTotal = Metrics.CreateCounter(
        "docs_clamav_circuit_short_circuit_total",
        "Total ClamAV scan calls short-circuited by the open circuit breaker.");
}
