using Microsoft.Extensions.Logging.Abstractions;
using Xenia.Infrastructure.Resilience;
using Xunit;

namespace Xenia.Tests.Resilience;

/// <summary>
/// Phase C — validates XeniaResiliencePolicy retry/backoff behaviour.
/// </summary>
public sealed class XeniaResiliencePolicyTests
{
    [Fact]
    public async Task ExecuteAsync_SucceedsOnFirstAttempt()
    {
        var policy = XeniaResiliencePolicy.Default;
        var calls = 0;
        var result = await policy.ExecuteAsync<int>(
            async _ => { calls++; return 42; },
            NullLogger.Instance);

        Assert.Equal(42, result);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task ExecuteAsync_RetriesOnTransientFailure()
    {
        var policy = new XeniaResiliencePolicy
        {
            MaxAttempts   = 3,
            BaseDelay     = TimeSpan.Zero,
            MaxDelay      = TimeSpan.Zero,
            BackoffFactor = 1.0,
            Jitter        = false,
        };

        var calls = 0;
        var result = await policy.ExecuteAsync<int>(
            async _ =>
            {
                calls++;
                if (calls < 3) throw new TimeoutException("transient");
                return 99;
            },
            NullLogger.Instance);

        Assert.Equal(99, result);
        Assert.Equal(3, calls);
    }

    [Fact]
    public async Task ExecuteAsync_ThrowsAfterMaxAttempts()
    {
        var policy = new XeniaResiliencePolicy
        {
            MaxAttempts   = 2,
            BaseDelay     = TimeSpan.Zero,
            MaxDelay      = TimeSpan.Zero,
            BackoffFactor = 1.0,
            Jitter        = false,
        };

        var ex = await Assert.ThrowsAsync<TimeoutException>(() =>
            policy.ExecuteAsync<int>(
                _ => throw new TimeoutException("always fails"),
                NullLogger.Instance));

        Assert.Equal("always fails", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotRetryOnOperationCanceledException()
    {
        var policy = new XeniaResiliencePolicy
        {
            MaxAttempts   = 3,
            BaseDelay     = TimeSpan.Zero,
            MaxDelay      = TimeSpan.Zero,
            BackoffFactor = 1.0,
            Jitter        = false,
        };

        var calls = 0;
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            policy.ExecuteAsync<int>(
                ct => { calls++; ct.ThrowIfCancellationRequested(); return Task.FromResult(1); },
                NullLogger.Instance, cts.Token));

        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task NoRetryPolicy_NeverRetries()
    {
        var policy = XeniaResiliencePolicy.NoRetry;
        var calls = 0;

        await Assert.ThrowsAsync<TimeoutException>(() =>
            policy.ExecuteAsync<int>(
                _ => { calls++; throw new TimeoutException(); },
                NullLogger.Instance));

        Assert.Equal(1, calls);
    }
}
