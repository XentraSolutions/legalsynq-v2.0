using System.Reflection;
using Commerce.Application.Abstractions;
using Commerce.Application.Common.Time;
using Commerce.Application.SystemInfo;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Commerce.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddCommerceApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly(), includeInternalTypes: true);
        services.AddSingleton<ISystemInfoService, SystemInfoService>();
        services.AddSingleton<IClock, SystemClock>();
        return services;
    }
}
