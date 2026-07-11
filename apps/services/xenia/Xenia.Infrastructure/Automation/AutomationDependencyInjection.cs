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
    ///   2. Durable EF-backed stores (singleton — IDbContextFactory per operation, no captive deps)
    ///   3. Application services (scoped — per-request lifecycle)
    ///   4. Singleton registry (holds provider references across requests)
    ///   5. Email provider (scoped — depends on scoped orchestrator)
    ///   6. Registration of email provider into registry via hosted init
    ///
    /// InMemory stores replaced by EF implementations (XENIA-P1-PROD-V1 / Migration 8).
    /// </summary>
    public static IServiceCollection AddXeniaAutomation(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<XeniaAutomationOptions>(
            configuration.GetSection(XeniaAutomationOptions.Section));

        // Durable EF-backed stores — singleton using IDbContextFactory to create DbContext per op
        services.AddSingleton<IAutomationRuntimeStateStore, EfAutomationRuntimeStateStore>();
        services.AddSingleton<IAutomationDeadLetterStore, EfAutomationDeadLetterStore>();
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
