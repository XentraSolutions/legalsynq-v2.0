using System.Diagnostics;

namespace Xenia.Infrastructure.Resilience;

/// <summary>
/// Lightweight thread-safe circuit breaker for Xenia platform adapter calls.
///
/// States: Closed → Open → HalfOpen → Closed
///
/// No external dependencies — pure System namespace only.
/// Phase C resilience contract: adapters that fail consecutively
/// trip the breaker and stop hammering the downstream service.
/// </summary>
public sealed class CircuitBreaker
{
    private enum State { Closed, Open, HalfOpen }

    private readonly int _failureThreshold;
    private readonly TimeSpan _openDuration;
    private readonly string _name;

    private State _state = State.Closed;
    private int _failureCount;
    private long _openedAtTicks;

    public string Name => _name;
    public bool IsOpen => _state == State.Open && !IsExpired;
    public bool IsHalfOpen => _state == State.HalfOpen || (_state == State.Open && IsExpired);

    private bool IsExpired =>
        _state == State.Open
        && Stopwatch.GetElapsedTime(Stopwatch.GetTimestamp() - _openedAtTicks) > _openDuration;

    public CircuitBreaker(string name, int failureThreshold = 5, int openDurationSeconds = 30)
    {
        _name             = name;
        _failureThreshold = failureThreshold;
        _openDuration     = TimeSpan.FromSeconds(openDurationSeconds);
    }

    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct = default)
    {
        if (IsOpen)
            throw new CircuitBreakerOpenException(_name);

        if (IsExpired)
            _state = State.HalfOpen;

        try
        {
            var result = await operation(ct);
            OnSuccess();
            return result;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception)
        {
            OnFailure();
            throw;
        }
    }

    public async Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken ct = default)
    {
        await ExecuteAsync<bool>(async c => { await operation(c); return true; }, ct);
    }

    private void OnSuccess()
    {
        _failureCount = 0;
        _state        = State.Closed;
    }

    private void OnFailure()
    {
        _failureCount++;
        if (_failureCount >= _failureThreshold || _state == State.HalfOpen)
        {
            _state        = State.Open;
            _openedAtTicks = Stopwatch.GetTimestamp();
            _failureCount = 0;
        }
    }

    public void Reset()
    {
        _failureCount = 0;
        _state        = State.Closed;
    }
}

public sealed class CircuitBreakerOpenException(string name)
    : InvalidOperationException($"Circuit breaker '{name}' is open. Calls are blocked until the cooldown period expires.");
