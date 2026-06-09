using Microsoft.Extensions.Configuration;
using Polly;
using Polly.Retry;
using Polly.CircuitBreaker;

namespace Commerce.Infrastructure.Resilience;

public interface IResiliencePolicyProvider
{
    ResiliencePipeline GetHttpPipeline();
}

/// <summary>
/// Reusable Polly pipeline registry for future outbound integrations
/// (payment providers, identity providers, webhooks). No real provider
/// calls are made in COM-B01 — this is foundation only.
/// </summary>
public sealed class ResiliencePolicyProvider : IResiliencePolicyProvider
{
    private readonly ResiliencePipeline _httpPipeline;

    public ResiliencePolicyProvider(IConfiguration configuration)
    {
        var retryCount = configuration.GetValue<int?>("Resilience:Http:RetryCount") ?? 3;
        var breakDuration = TimeSpan.FromSeconds(
            configuration.GetValue<int?>("Resilience:Http:CircuitBreaker:BreakDurationSeconds") ?? 30);
        var failureRatio = configuration.GetValue<double?>("Resilience:Http:CircuitBreaker:FailureRatio") ?? 0.5;
        var minThroughput = configuration.GetValue<int?>("Resilience:Http:CircuitBreaker:MinimumThroughput") ?? 10;

        _httpPipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = retryCount,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromMilliseconds(200),
                UseJitter = true
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = failureRatio,
                MinimumThroughput = minThroughput,
                SamplingDuration = TimeSpan.FromSeconds(30),
                BreakDuration = breakDuration
            })
            .AddTimeout(TimeSpan.FromSeconds(30))
            .Build();
    }

    public ResiliencePipeline GetHttpPipeline() => _httpPipeline;
}
