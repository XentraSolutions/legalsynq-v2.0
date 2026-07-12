using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Xenia.Infrastructure.Performance;

/// <summary>
/// Phase D — lightweight performance harness for high-frequency Xenia paths.
///
/// Used to track per-operation latency without requiring a full APM agent.
/// Writes to structured log at Trace level and (optionally) to XeniaMetrics histograms.
///
/// Usage:
///   using var op = XeniaPerformanceHarness.Begin("xenia.email.sync", logger);
///   // ... do work ...
///   op.Complete(success: true);
/// </summary>
public sealed class XeniaPerformanceOperation : IDisposable
{
    private readonly string _operationName;
    private readonly ILogger _logger;
    private readonly long _startedAt;
    private bool _completed;
    private bool _success;

    internal XeniaPerformanceOperation(string operationName, ILogger logger)
    {
        _operationName = operationName;
        _logger        = logger;
        _startedAt     = Stopwatch.GetTimestamp();
    }

    public void Complete(bool success, string? detail = null)
    {
        _completed = true;
        _success   = success;
        var elapsed = Stopwatch.GetElapsedTime(_startedAt);
        _logger.LogTrace(
            "Xenia perf: op={Operation} success={Success} duration_ms={DurationMs} detail={Detail}",
            _operationName, success, (int)elapsed.TotalMilliseconds, detail ?? string.Empty);
    }

    public void Dispose()
    {
        if (!_completed)
        {
            var elapsed = Stopwatch.GetElapsedTime(_startedAt);
            _logger.LogTrace(
                "Xenia perf (abandoned): op={Operation} duration_ms={DurationMs}",
                _operationName, (int)elapsed.TotalMilliseconds);
        }
    }

    public TimeSpan Elapsed => Stopwatch.GetElapsedTime(_startedAt);
}

/// <summary>
/// Factory for XeniaPerformanceOperation.
/// </summary>
public static class XeniaPerformanceHarness
{
    public static XeniaPerformanceOperation Begin(string operationName, ILogger logger) =>
        new(operationName, logger);

    /// <summary>
    /// Wraps <paramref name="operation"/> in a performance measurement and logs on completion.
    /// </summary>
    public static async Task<T> MeasureAsync<T>(
        string operationName,
        Func<CancellationToken, Task<T>> operation,
        ILogger logger,
        CancellationToken ct = default)
    {
        using var op = Begin(operationName, logger);
        try
        {
            var result = await operation(ct);
            op.Complete(success: true, detail: $"elapsed_ms={(int)op.Elapsed.TotalMilliseconds}");
            return result;
        }
        catch (Exception)
        {
            op.Complete(success: false);
            throw;
        }
    }

    public static async Task MeasureAsync(
        string operationName,
        Func<CancellationToken, Task> operation,
        ILogger logger,
        CancellationToken ct = default)
    {
        await MeasureAsync<bool>(
            operationName,
            async c => { await operation(c); return true; },
            logger, ct);
    }
}
