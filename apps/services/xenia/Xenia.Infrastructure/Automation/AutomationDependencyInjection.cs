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
    ///   5. Startup hosted services (registration order = execution order):
    ///      a. AutomationRegistrationWorker   — discovers &amp; registers DI providers
    ///      b. EfAutomationRegistryReconciler — marks missing providers Unavailable,
    ///                                          restores returned providers (G5/G6 closure)
    ///      c. AutomationStoreValidationService — asserts all 7 stores are EF-backed (G1 closure)
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
        var skipDatabaseStartup = configuration.GetValue<bool>("Xenia:SkipDatabaseStartup");

        // ── Durable EF-backed singletons ──────────────────────────────────────
        services.AddSingleton<IAutomationRuntimeStateStore, EfAutomationRuntimeStateStore>();
        services.AddSingleton<IAutomationDeadLetterStore, EfAutomationDeadLetterStore>();
        services.AddSingleton<IAutomationScheduler, EfAutomationScheduleStore>();
        services.AddSingleton<IAutomationEventPublisher, AuditAdapterAutomationEventPublisher>();
        services.AddSingleton<IAutomationRegistry, EfAutomationRegistry>();

        // ── Application-layer EF-backed singletons ────────────────────────────
        services.AddSingleton<IAutomationConfigurationService, EfAutomationConfigurationService>();
        services.AddSingleton<IAutomationIdempotencyService, EfAutomationIdempotencyService>();

        // ── IAutomationRegistryReconciler (also IHostedService) ───────────────
        // Registered as both singleton and hosted service so callers can inject
        // IAutomationRegistryReconciler directly for on-demand reconciliation.
        services.AddSingleton<EfAutomationRegistryReconciler>();
        services.AddSingleton<IAutomationRegistryReconciler>(
            sp => sp.GetRequiredService<EfAutomationRegistryReconciler>());

        // ── Scoped services — per-request ─────────────────────────────────────
        services.AddScoped<IAutomationDiscoveryService, DefaultAutomationDiscoveryService>();
        services.AddScoped<IAutomationExecutionService, EfAutomationExecutionService>();
        services.AddScoped<IAutomationDiagnosticsService, DefaultAutomationDiagnosticsService>();
        services.AddScoped<IAutomationProvider, EmailAutomationProvider>();

        // ── Startup hosted services — ORDER MATTERS ───────────────────────────
        if (!skipDatabaseStartup)
        {
            // 1. Register providers into the in-process registry + DB
            services.AddHostedService<AutomationRegistrationWorker>();
            // 2. Reconcile: mark missing providers Unavailable, restore returned providers
            services.AddHostedService(sp => sp.GetRequiredService<EfAutomationRegistryReconciler>());
            // 3. Validate all 7 mutable stores are EF-backed (fails fast in Production/Staging)
            services.AddHostedService<AutomationStoreValidationService>();
        }

        return services;
    }
}
