using Microsoft.Extensions.Options;

namespace Commerce.Infrastructure.Integration.TenantBilling;

/// <summary>
/// TB-INT-02 — tiny in-process circuit breaker for the Tenant Billing
/// entitlement publisher. State is held per process (singleton DI)
/// so it survives across the typed-client's transient HttpClient
/// instances.
/// </summary>
public interface ITenantBillingPublisherCircuitBreaker
{
    /// <summary>
    /// Attempt to enter the breaker. Returns <c>true</c> if the call
    /// is allowed (Closed or HalfOpen probe). Returns <c>false</c> if
    /// the breaker is Open and the cool-down has not elapsed; the
    /// caller must short-circuit with <c>tenant-billing-circuit-open</c>.
    /// </summary>
    bool TryEnter();

    /// <summary>Record a successful publish (Closed transition).</summary>
    void RecordSuccess();

    /// <summary>
    /// Record a transient publish failure (timeout / 5xx / 408 / 429 /
    /// transport error). May trip the breaker. Non-transient failures
    /// (4xx other than 408/429) MUST NOT call this.
    /// </summary>
    void RecordTransientFailure();

    /// <summary>Current public state name for diagnostics.</summary>
    string State { get; }
}

/// <summary>
/// Default in-memory implementation. Disabled (always allows + never
/// records) when <see cref="TenantBillingClientOptions.CircuitBreakerEnabled"/>
/// is false.
/// </summary>
public sealed class TenantBillingPublisherCircuitBreaker
    : ITenantBillingPublisherCircuitBreaker
{
    private readonly IOptionsMonitor<TenantBillingClientOptions> _options;
    private readonly Func<DateTimeOffset> _clock;
    private readonly object _gate = new();

    private CircuitState _state = CircuitState.Closed;
    private int _consecutiveFailures;
    private DateTimeOffset _openedUntil;

    public TenantBillingPublisherCircuitBreaker(
        IOptionsMonitor<TenantBillingClientOptions> options)
        : this(options, () => DateTimeOffset.UtcNow) { }

    /// <summary>Test seam for deterministic clock.</summary>
    internal TenantBillingPublisherCircuitBreaker(
        IOptionsMonitor<TenantBillingClientOptions> options,
        Func<DateTimeOffset> clock)
    {
        _options = options;
        _clock = clock;
    }

    public string State
    {
        get { lock (_gate) return _state.ToString(); }
    }

    public bool TryEnter()
    {
        var opts = _options.CurrentValue.Normalised();
        if (!opts.CircuitBreakerEnabled) return true;

        lock (_gate)
        {
            if (_state == CircuitState.Closed) return true;
            if (_state == CircuitState.Open)
            {
                if (_clock() < _openedUntil) return false;
                // Cool-down elapsed — allow exactly one probe call.
                _state = CircuitState.HalfOpen;
                return true;
            }
            // HalfOpen: a probe is already in flight; refuse other
            // concurrent callers to avoid stampedes.
            return false;
        }
    }

    public void RecordSuccess()
    {
        var opts = _options.CurrentValue.Normalised();
        if (!opts.CircuitBreakerEnabled) return;

        lock (_gate)
        {
            _state = CircuitState.Closed;
            _consecutiveFailures = 0;
            _openedUntil = default;
        }
    }

    public void RecordTransientFailure()
    {
        var opts = _options.CurrentValue.Normalised();
        if (!opts.CircuitBreakerEnabled) return;

        lock (_gate)
        {
            if (_state == CircuitState.HalfOpen)
            {
                // Probe failed → reopen for another full window.
                _state = CircuitState.Open;
                _openedUntil = _clock().AddSeconds(opts.CircuitBreakerDurationSeconds);
                return;
            }
            _consecutiveFailures++;
            if (_consecutiveFailures >= opts.CircuitBreakerFailures)
            {
                _state = CircuitState.Open;
                _openedUntil = _clock().AddSeconds(opts.CircuitBreakerDurationSeconds);
            }
        }
    }

    private enum CircuitState { Closed = 0, Open = 1, HalfOpen = 2 }
}
