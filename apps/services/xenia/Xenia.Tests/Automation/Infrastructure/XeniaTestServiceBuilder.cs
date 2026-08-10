using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using Xenia.Application.Automation;
using Xenia.Domain.Automation;
using Xenia.Infrastructure.Automation;
using Xenia.Infrastructure.Persistence;

namespace Xenia.Tests.Automation.Infrastructure;

/// <summary>
/// Builds a minimal <see cref="IServiceProvider"/> for automation runtime tests.
///
/// Registers only the EF-backed services needed by tests — no hosted services are started
/// (hosted services only run when IHost.StartAsync() is called, which tests never do).
///
/// Service dependency graph:
///   EfAutomationRegistry:
///     IDbContextFactory, IAutomationRuntimeStateStore, IAutomationEventPublisher, ILogger
///   EfAutomationDeadLetterStore:
///     IDbContextFactory, ILogger
///   EfAutomationRuntimeStateStore:
///     IDbContextFactory, ILogger
///   EfAutomationScheduleStore:
///     IDbContextFactory, IOptions&lt;XeniaAutomationOptions&gt;, ILogger
///   EfAutomationConfigurationService:
///     IDbContextFactory, ILogger
///   EfAutomationIdempotencyService:
///     IDbContextFactory, ILogger
///   EfAutomationExecutionService (scoped):
///     IAutomationRegistry, IAutomationEventPublisher, IAutomationDeadLetterStore,
///     IAutomationIdempotencyService, ILogger
///   EfAutomationRegistryReconciler:
///     IAutomationRegistry, IDbContextFactory, ILogger
/// </summary>
public static class XeniaTestServiceBuilder
{
    /// <summary>
    /// Builds a full automation DI container connected to the test MySQL database.
    /// All stores are EF-backed. No hosted services start.
    /// </summary>
    public static ServiceProvider Build(
        string connectionString = XeniaRelationalFixture.ConnectionString,
        string? instanceId = null)
    {
        var services = new ServiceCollection();

        services.AddLogging();

        // Options
        services.Configure<XeniaAutomationOptions>(_ => { });

        // DbContext factory (Pomelo MySQL)
        services.AddDbContextFactory<XeniaDbContext>(opts =>
        {
            var serverVersion = new MySqlServerVersion(new Version(8, 0, 0));
            opts.UseMySql(connectionString, serverVersion, my =>
            {
                my.MigrationsAssembly("Xenia.Infrastructure");
                my.CommandTimeout(30);
            });
        });

        // Noop event publisher — prevents audit client dependency in tests
        services.AddSingleton<IAutomationEventPublisher, NoopAutomationEventPublisher>();

        // EF-backed singletons (order matters for DI resolution)
        services.AddSingleton<IAutomationRuntimeStateStore, EfAutomationRuntimeStateStore>();
        services.AddSingleton<IAutomationDeadLetterStore, EfAutomationDeadLetterStore>();
        services.AddSingleton<IAutomationScheduler, EfAutomationScheduleStore>();
        services.AddSingleton<IAutomationRegistry, EfAutomationRegistry>();
        services.AddSingleton<IAutomationConfigurationService, EfAutomationConfigurationService>();
        services.AddSingleton<IAutomationIdempotencyService, EfAutomationIdempotencyService>();

        // Reconciler (singleton + interface alias)
        services.AddSingleton<EfAutomationRegistryReconciler>();
        services.AddSingleton<IAutomationRegistryReconciler>(
            sp => sp.GetRequiredService<EfAutomationRegistryReconciler>());

        // Scoped services
        services.AddScoped<IAutomationExecutionService, EfAutomationExecutionService>();

        return services.BuildServiceProvider();
    }

    // Use Build(connectionString: X, instanceId: Y) for multi-instance tests.
}

/// <summary>No-op event publisher. Prevents audit service dependency in tests.</summary>
public sealed class NoopAutomationEventPublisher : IAutomationEventPublisher
{
    public Task PublishRegisteredAsync(
        string automationKey, string version, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task PublishEnabledAsync(
        string automationKey, string version, Guid? tenantId, Guid actorId,
        CancellationToken ct = default) => Task.CompletedTask;

    public Task PublishDisabledAsync(
        string automationKey, string version, Guid? tenantId, Guid actorId,
        CancellationToken ct = default) => Task.CompletedTask;

    public Task PublishExecutionQueuedAsync(
        string automationKey, string version, Guid executionId, Guid? tenantId,
        string? correlationId, CancellationToken ct = default) => Task.CompletedTask;

    public Task PublishExecutionStartedAsync(
        string automationKey, string version, Guid executionId, Guid? tenantId,
        string? correlationId, CancellationToken ct = default) => Task.CompletedTask;

    public Task PublishExecutionCompletedAsync(
        string automationKey, string version, Guid executionId, Guid? tenantId,
        string? correlationId, CancellationToken ct = default) => Task.CompletedTask;

    public Task PublishExecutionFailedAsync(
        string automationKey, string version, Guid executionId, Guid? tenantId,
        string? correlationId, string failureCategory, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task PublishExecutionCancelledAsync(
        string automationKey, string version, Guid executionId, Guid? tenantId,
        CancellationToken ct = default) => Task.CompletedTask;

    public Task PublishDeadLetteredAsync(
        string automationKey, string version, Guid executionId, Guid? tenantId,
        string failureCategory, CancellationToken ct = default) => Task.CompletedTask;
}
