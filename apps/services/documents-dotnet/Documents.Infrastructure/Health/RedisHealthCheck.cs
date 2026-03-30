using Documents.Infrastructure.Observability;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Documents.Infrastructure.Health;

/// <summary>
/// Health check for the Redis dependency.
/// Performs a PING against the configured Redis instance and updates
/// <see cref="RedisMetrics.RedisHealthy"/> accordingly.
///
/// Registered only when Redis is actively used (QueueProvider=redis OR AccessToken:Store=redis).
/// Tagged "ready" — surfaces in /health/ready but not in the liveness /health endpoint.
/// </summary>
public sealed class RedisHealthCheck : IHealthCheck
{
    private readonly IConnectionMultiplexer    _redis;
    private readonly ILogger<RedisHealthCheck> _log;

    public RedisHealthCheck(IConnectionMultiplexer redis, ILogger<RedisHealthCheck> log)
    {
        _redis = redis;
        _log   = log;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken  cancellationToken = default)
    {
        try
        {
            var db      = _redis.GetDatabase();
            var latency = await db.PingAsync();

            RedisMetrics.RedisHealthy.Set(1);

            var description = $"Redis reachable — latency {latency.TotalMilliseconds:F1} ms. " +
                              $"Connected endpoints: {_redis.GetCounters().TotalOutstanding}";

            _log.LogDebug("Redis health check passed — latency {LatencyMs} ms",
                latency.TotalMilliseconds);

            return HealthCheckResult.Healthy(description);
        }
        catch (Exception ex)
        {
            RedisMetrics.RedisHealthy.Set(0);
            RedisMetrics.RedisConnectionFailures.Inc();

            _log.LogWarning(ex, "Redis health check failed — Redis may be unreachable");

            return HealthCheckResult.Unhealthy(
                description: $"Redis unreachable: {ex.Message}",
                exception:   ex);
        }
    }
}
