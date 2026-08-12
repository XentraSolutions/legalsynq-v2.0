using Microsoft.Extensions.Logging;

namespace Xenia.Infrastructure.Resilience;

/// <summary>
/// Phase C — Lightweight resilience policies for Xenia service calls.
///
/// Design:
/// - No external Polly dependency required — pure in-house exponential backoff.
/// - Policies are stateless records — compose them at call sites.
/// - Always fail-open on transient errors: never dead-letter on first failure.
/// - Audit adapter and notification adapter calls are exempt from retry
///   (they have their own fail-silent wrappers).
///
/// Usage:
///   await XeniaResiliencePolicy.Default.ExecuteAsync(
///       () => SomeExternalCallAsync(ct), logger, ct);
/// </summary>
public sealed record XeniaResiliencePolicy
{
    public static readonly XeniaResiliencePolicy Default = new()
    {
        MaxAttempts   = 3,
        BaseDelay     = TimeSpan.FromSeconds(1),
        MaxDelay      = TimeSpan.FromSeconds(30),
        BackoffFactor = 2.0,
        Jitter        = true,
    };

    public static readonly XeniaResiliencePolicy Aggressive = new()
    {
        MaxAttempts   = 5,
        BaseDelay     = TimeSpan.FromMilliseconds(500),
        MaxDelay      = TimeSpan.FromSeconds(60),
        BackoffFactor = 2.5,
        Jitter        = true,
    };

    public static readonly XeniaResiliencePolicy NoRetry = new()
    {
        MaxAttempts   = 1,
        BaseDelay     = TimeSpan.Zero,
        MaxDelay      = TimeSpan.Zero,
        BackoffFactor = 1.0,
        Jitter        = false,
    };

    public required int MaxAttempts { get; init; }
    public required TimeSpan BaseDelay { get; init; }
    public required TimeSpan MaxDelay { get; init; }
    public required double BackoffFactor { get; init; }
    public required bool Jitter { get; init; }

    private static readonly Random _rng = Random.Shared;

    /// <summary>
    /// Executes <paramref name="operation"/> with exponential back-off retry.
    /// Retries only on transient exceptions (not OperationCanceledException).
    /// </summary>
    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        ILogger logger,
        CancellationToken ct = default,
        Func<Exception, bool>? isRetryable = null)
    {
        Exception? lastEx = null;
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                return await operation(ct);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) when (isRetryable?.Invoke(ex) ?? IsTransient(ex))
            {
                lastEx = ex;
                if (attempt == MaxAttempts) break;

                var delay = ComputeDelay(attempt);
                logger.LogWarning(ex,
                    "Transient failure on attempt {Attempt}/{MaxAttempts}; retrying in {DelayMs}ms",
                    attempt, MaxAttempts, (int)delay.TotalMilliseconds);
                await Task.Delay(delay, ct);
            }
        }
        throw lastEx!;
    }

    /// <summary>
    /// Executes <paramref name="operation"/> with exponential back-off retry (no return value).
    /// </summary>
    public async Task ExecuteAsync(
        Func<CancellationToken, Task> operation,
        ILogger logger,
        CancellationToken ct = default,
        Func<Exception, bool>? isRetryable = null)
    {
        await ExecuteAsync<bool>(async c => { await operation(c); return true; }, logger, ct, isRetryable);
    }

    private TimeSpan ComputeDelay(int attempt)
    {
        var raw = BaseDelay * Math.Pow(BackoffFactor, attempt - 1);
        if (Jitter)
            raw += TimeSpan.FromMilliseconds(_rng.NextDouble() * raw.TotalMilliseconds * 0.3);
        return raw > MaxDelay ? MaxDelay : raw;
    }

    private static bool IsTransient(Exception ex) =>
        ex is TimeoutException
        || ex is System.Net.Http.HttpRequestException httpEx
            && httpEx.StatusCode is not null
            && (int)httpEx.StatusCode >= 500
        || ex is InvalidOperationException
            && ex.Message.Contains("connection", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase);
}
