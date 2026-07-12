using Microsoft.Extensions.DependencyInjection;

namespace Xenia.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddXeniaApplication(this IServiceCollection services)
    {
        services.AddScoped<IXeniaService, XeniaService>();
        return services;
    }
}
