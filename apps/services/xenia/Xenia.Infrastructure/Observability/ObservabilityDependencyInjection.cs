using Microsoft.Extensions.DependencyInjection;

namespace Xenia.Infrastructure.Observability;

/// <summary>
/// Phase B — registers Xenia observability infrastructure.
/// Metrics are surfaced via System.Diagnostics.Metrics (IMeterFactory).
/// </summary>
public static class ObservabilityDependencyInjection
{
    public static IServiceCollection AddXeniaObservability(this IServiceCollection services)
    {
        services.AddSingleton<XeniaMetrics>();
        return services;
    }
}
