using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Xenia.Application.Automation;

namespace Xenia.Infrastructure.Automation;

public static class AutomationDependencyInjection
{
    /// <summary>
    /// Registers the generic Xenia automation framework.
    ///
    /// Order:
    ///   1. Configuration options
    ///   2. Internal stores (singleton — in-memory state survives request scoping)
    ///   3. Application services (scoped — per-request lifecycle)
    ///   4. Singleton registry (holds provider references across requests)
    ///   5. Email provider (scoped — depends on scoped orchestrator)
    ///   6. Registration of email provider into registry via hosted init
    ///
    /// Phase H will swap InMemory stores for EF-backed implementations.
    /// </summary>
    public static IServiceCollection AddXeniaAutomation(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<XeniaAutomationOptions>(
            configuration.GetSection(XeniaAutomationOptions.Section));

        services.AddSingleton<IAutomationRuntimeStateStore, InMemoryAutomationRuntimeStateStore>();
        services.AddSingleton<IAutomationDeadLetterStore, InMemoryAutomationDeadLetterStore>();
        services.AddSingleton<IAutomationScheduler, DefaultAutomationScheduler>();
        services.AddSingleton<IAutomationEventPublisher, AuditAdapterAutomationEventPublisher>();
        services.AddSingleton<IAutomationRegistry, InMemoryAutomationRegistry>();

        services.AddScoped<IAutomationDiscoveryService, DefaultAutomationDiscoveryService>();
        services.AddScoped<IAutomationExecutionService, DefaultAutomationExecutionService>();
        services.AddScoped<IAutomationDiagnosticsService, DefaultAutomationDiagnosticsService>();
        services.AddScoped<IAutomationProvider, EmailAutomationProvider>();

        services.AddHostedService<AutomationRegistrationWorker>();

        return services;
    }
}
