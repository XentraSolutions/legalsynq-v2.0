using Microsoft.Extensions.DependencyInjection;
using Xenia.Application.TenantContext;

namespace Xenia.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers Xenia Application-layer services.
    /// Abstractions registered here are implemented in Xenia.Infrastructure.
    /// </summary>
    public static IServiceCollection AddXeniaApplication(this IServiceCollection services)
    {
        services.AddScoped<XeniaTenantContextAccessor>();

        return services;
    }
}
