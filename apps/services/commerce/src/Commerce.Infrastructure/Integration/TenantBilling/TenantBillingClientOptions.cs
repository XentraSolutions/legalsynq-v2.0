namespace Commerce.Infrastructure.Integration.TenantBilling;

/// <summary>
/// TB-INT-01 / TB-INT-02 — bound to <c>Commerce:TenantBilling</c> in
/// configuration.
///
/// <para>Defaults are deliberately safe: <see cref="Enabled"/> is
/// <c>false</c> and the URL/token are empty so an unconfigured deploy
/// can boot and answer health probes but cannot accidentally publish.
/// In production these should be supplied via environment variables:
/// <c>Commerce__TenantBilling__Enabled</c>,
/// <c>Commerce__TenantBilling__BaseUrl</c>,
/// <c>Commerce__TenantBilling__InternalToken</c>.</para>
///
/// <para>Resilience knobs added in TB-INT-02 are clamped via
/// <see cref="Normalised"/> so a hostile or sloppy config can't cause
/// unbounded retries or zero-duration circuit breakers.</para>
/// </summary>
public sealed class TenantBillingClientOptions
{
    public const string SectionName = "Commerce:TenantBilling";

    /// <summary>
    /// Master switch. When <c>false</c> the publisher returns
    /// <c>Skipped/publisher-disabled</c> without making any HTTP call.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Absolute base URL of the canonical Tenant Billing API, e.g.
    /// <c>http://localhost:5001</c>. The publisher always appends
    /// <c>/api/tenant-billing/entitlements/apply</c>.
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Shared secret for the Tenant Billing <c>X-Internal-Token</c>
    /// header. Must come from a secret store, never from a committed
    /// config file.
    /// </summary>
    public string InternalToken { get; set; } = string.Empty;

    /// <summary>HTTP timeout for a single publish attempt. Defaults to 10s.</summary>
    public int TimeoutSeconds { get; set; } = 10;

    // ───────── TB-INT-02 resilience knobs ─────────

    /// <summary>
    /// Number of retry attempts after the initial attempt. Default 2
    /// (so up to 3 total attempts). Clamped to [0, 10] in
    /// <see cref="Normalised"/>.
    /// </summary>
    public int RetryAttempts { get; set; } = 2;

    /// <summary>
    /// Constant delay between retry attempts, in milliseconds. Default
    /// 250. Clamped to [0, 10000]. Tests set this to 0.
    /// </summary>
    public int RetryDelayMilliseconds { get; set; } = 250;

    /// <summary>
    /// When true, an in-process circuit breaker fails fast after a
    /// configured number of consecutive transient failures.
    /// </summary>
    public bool CircuitBreakerEnabled { get; set; }

    /// <summary>
    /// Consecutive transient publish failures that trip the breaker.
    /// Default 5. Clamped to [1, 100].
    /// </summary>
    public int CircuitBreakerFailures { get; set; } = 5;

    /// <summary>
    /// How long the breaker stays open before allowing a probe call.
    /// Default 30 seconds. Clamped to [1, 3600].
    /// </summary>
    public int CircuitBreakerDurationSeconds { get; set; } = 30;

    // ───────── TB-INT-03 auto-publish knobs ─────────

    /// <summary>
    /// When true, lifecycle changes in Commerce (subscription
    /// create/activate/suspend/reactivate/cancel/plan-change and
    /// account-standing recalculation) enqueue an auto-publish work
    /// item that is processed by a background worker. Defaults to
    /// <c>false</c> so a fresh deploy never auto-publishes until an
    /// operator opts in. Independent from <see cref="Enabled"/>: when
    /// auto-publish is on but the publisher itself is off, the worker
    /// will still drain the queue and the publisher will return
    /// <see cref="Application.Integration.Abstractions.PublishEntitlementOutcome.Skipped"/>
    /// without any HTTP traffic.
    /// </summary>
    public bool AutoPublishEnabled { get; set; }

    /// <summary>
    /// Bounded capacity of the in-process auto-publish queue. Once
    /// full, new enqueue attempts return
    /// <see cref="Application.Integration.Abstractions.EnqueueResult.DroppedQueueFull"/>
    /// without blocking the Commerce trigger site. Defaults to 1000;
    /// clamped to [1, 100000] in <see cref="Normalised"/>.
    /// </summary>
    public int AutoPublishQueueCapacity { get; set; } = 1_000;

    // ───────── TB-INT-04 outbox knobs ─────────

    /// <summary>
    /// When true, the auto-publish trigger sites write a durable
    /// outbox row instead of (or alongside) enqueueing onto the
    /// in-process channel. Defaults to <c>false</c> so existing
    /// TB-INT-03 in-memory behaviour is preserved until an operator
    /// opts in. Has no effect when <see cref="AutoPublishEnabled"/>
    /// is <c>false</c>.
    /// </summary>
    public bool OutboxEnabled { get; set; }

    /// <summary>
    /// Maximum number of due rows the outbox processor pulls in a
    /// single batch. Default 25; clamped to [1, 1000].
    /// </summary>
    public int OutboxBatchSize { get; set; } = 25;

    /// <summary>
    /// Outbox worker poll interval in seconds. Default 10; clamped
    /// to [1, 600].
    /// </summary>
    public int OutboxPollSeconds { get; set; } = 10;

    /// <summary>
    /// Maximum publish attempts before a row is marked
    /// <c>Abandoned</c>. Default 10; clamped to [1, 100].
    /// </summary>
    public int OutboxMaxAttempts { get; set; } = 10;

    /// <summary>
    /// Linear backoff base in seconds — the next-attempt delay after
    /// a failed attempt is <c>OutboxRetryBaseDelaySeconds *
    /// min(Attempts, 10)</c>. Default 30; clamped to [1, 3600].
    /// </summary>
    public int OutboxRetryBaseDelaySeconds { get; set; } = 30;

    /// <summary>
    /// Returns a clone with all fields clamped to safe ranges.
    /// Pure / no side effects so it can also be used by tests and the
    /// diagnostics endpoint without mutating the bound singleton.
    /// </summary>
    public TenantBillingClientOptions Normalised() => new()
    {
        Enabled = Enabled,
        BaseUrl = BaseUrl ?? string.Empty,
        InternalToken = InternalToken ?? string.Empty,
        TimeoutSeconds = Clamp(TimeoutSeconds, 1, 600),
        RetryAttempts = Clamp(RetryAttempts, 0, 10),
        RetryDelayMilliseconds = Clamp(RetryDelayMilliseconds, 0, 10_000),
        CircuitBreakerEnabled = CircuitBreakerEnabled,
        CircuitBreakerFailures = Clamp(CircuitBreakerFailures, 1, 100),
        CircuitBreakerDurationSeconds = Clamp(CircuitBreakerDurationSeconds, 1, 3600),
        AutoPublishEnabled = AutoPublishEnabled,
        AutoPublishQueueCapacity = Clamp(AutoPublishQueueCapacity, 1, 100_000),
        OutboxEnabled = OutboxEnabled,
        OutboxBatchSize = Clamp(OutboxBatchSize, 1, 1_000),
        OutboxPollSeconds = Clamp(OutboxPollSeconds, 1, 600),
        OutboxMaxAttempts = Clamp(OutboxMaxAttempts, 1, 100),
        OutboxRetryBaseDelaySeconds = Clamp(OutboxRetryBaseDelaySeconds, 1, 3600),
    };

    private static int Clamp(int value, int min, int max)
        => value < min ? min : (value > max ? max : value);
}
