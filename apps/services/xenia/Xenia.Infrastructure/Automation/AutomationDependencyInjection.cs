using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Xenia.Application.Automation;

namespace Xenia.Infrastructure.Automation;

public static class AutomationDependencyInjection
{
    /// <summary>
    /// Registers the generic Xenia automation framework.
    ///
    /// Order:
    ///   1. Configuration options
    ///   2. Durable EF-backed singletons — IDbContextFactory per op, no captive deps
    ///   3. Application-layer EF-backed singletons (config, idempotency)
    ///   4. Scoped services — per-request lifecycle
    ///   5. Startup reconciliation hosted service
    ///
    /// All in-memory automation components replaced by EF/MySQL implementations
    /// (XENIA-P1-PROD-V1-T1). InMemory impls retained in source as test doubles only.
    /// </summary>
    public static IServiceCollection AddXeniaAutomation(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<XeniaAutomationOptions>(
            configuration.GetSection(XeniaAutomationOptions.Section));

        // ── Durable EF-backed singletons ──────────────────────────────────────
        services.AddSingleton<IAutomationRuntimeStateStore, EfAutomationRuntimeStateStore>();
        services.AddSingleton<IAutomationDeadLetterStore, EfAutomationDeadLetterStore>();
        services.AddSingleton<IAutomationScheduler, EfAutomationScheduleStore>();
        services.AddSingleton<IAutomationEventPublisher, AuditAdapterAutomationEventPublisher>();
        services.AddSingleton<IAutomationRegistry, EfAutomationRegistry>();

        // ── Application-layer EF-backed singletons ────────────────────────────
        services.AddSingleton<IAutomationConfigurationService, EfAutomationConfigurationService>();
        services.AddSingleton<IAutomationIdempotencyService, EfAutomationIdempotencyService>();

        // ── Scoped services — per-request ─────────────────────────────────────
        services.AddScoped<IAutomationDiscoveryService, DefaultAutomationDiscoveryService>();
        services.AddScoped<IAutomationExecutionService, EfAutomationExecutionService>();
        services.AddScoped<IAutomationDiagnosticsService, DefaultAutomationDiagnosticsService>();
        services.AddScoped<IAutomationProvider, EmailAutomationProvider>();

        // ── Startup reconciliation + store validation ──────────────────────────
        services.AddHostedService<AutomationRegistrationWorker>();
        services.AddHostedService<AutomationStoreValidationService>();

        return services;
    }
}
