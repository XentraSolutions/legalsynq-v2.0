using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using Xenia.Application.Automation;
using Xenia.Domain.Automation;
using Xenia.Infrastructure.Automation;
using Xenia.Infrastructure.Persistence;

namespace Xenia.Tests.Automation.Validation;

/// <summary>
/// Unit tests for AutomationStoreValidationService (G1).
///
/// Proves:
///   G1 — all seven mutable stores are validated at startup:
///         IAutomationRegistry, IAutomationRuntimeStateStore, IAutomationDeadLetterStore,
///         IAutomationScheduler, IAutomationConfigurationService,
///         IAutomationIdempotencyService, IAutomationExecutionService.
///
/// These are pure unit tests; no MySQL connection is required.
/// The validation service checks TYPES only (not DB connectivity).
/// </summary>
public sealed class AutomationStoreValidationServiceTests
{
    // Minimal connection string for DbContextFactory registration (not called during validation)
    private const string FakeConnStr =
        "Server=127.0.0.1;Port=13309;Database=xenia_test_validation;" +
        "Uid=root;Pwd=xeniatest123;AllowPublicKeyRetrieval=true;SslMode=None;";

    // ── G1: All-EF configuration passes in Production ─────────────────────

    [Fact]
    public async Task AllEfBacked_DoesNotThrow_InProduction()
    {
        var sp  = BuildAllEfServices(isProduction: true);
        var svc = BuildValidationService(sp, isProduction: true);
        var ex  = await Record.ExceptionAsync(() => svc.StartAsync(CancellationToken.None));
        Assert.Null(ex);
    }

    // ── G1: In-memory registry is flagged in Production ───────────────────

    [Fact]
    public async Task InMemoryRegistry_Throws_InProduction()
    {
        var sp  = BuildWithInMemoryRegistry(isProduction: true);
        var svc = BuildValidationService(sp, isProduction: true);
        var ex  = await Record.ExceptionAsync(() => svc.StartAsync(CancellationToken.None));
        Assert.NotNull(ex);
        Assert.IsType<InvalidOperationException>(ex);
        Assert.Contains("IAutomationRegistry", ex.Message);
    }

    // ── G1: In-memory execution service is flagged in Production ─────────

    [Fact]
    public async Task InMemoryExecutionService_Throws_InProduction()
    {
        var sp  = BuildWithDefaultExecutionService(isProduction: true);
        var svc = BuildValidationService(sp, isProduction: true);
        var ex  = await Record.ExceptionAsync(() => svc.StartAsync(CancellationToken.None));
        Assert.NotNull(ex);
        Assert.IsType<InvalidOperationException>(ex);
        Assert.Contains("IAutomationExecutionService", ex.Message);
    }

    // ── G1: In-memory stores only warn (do not throw) in Development ──────

    [Fact]
    public async Task InMemoryRegistry_OnlyWarns_InDevelopment()
    {
        var sp  = BuildWithInMemoryRegistry(isProduction: false);
        var svc = BuildValidationService(sp, isProduction: false);
        // Should NOT throw in Development
        var ex  = await Record.ExceptionAsync(() => svc.StartAsync(CancellationToken.None));
        Assert.Null(ex);
    }

    // ── G1: In-memory dead-letter store is flagged ────────────────────────

    [Fact]
    public async Task InMemoryDeadLetterStore_Throws_InProduction()
    {
        var sp  = BuildWithInMemoryDeadLetterStore(isProduction: true);
        var svc = BuildValidationService(sp, isProduction: true);
        var ex  = await Record.ExceptionAsync(() => svc.StartAsync(CancellationToken.None));
        Assert.NotNull(ex);
        Assert.IsType<InvalidOperationException>(ex);
        Assert.Contains("IAutomationDeadLetterStore", ex.Message);
    }

    // ── G1: Non-EF scheduler is flagged ───────────────────────────────────

    [Fact]
    public async Task DefaultScheduler_Throws_InProduction()
    {
        var sp  = BuildWithDefaultScheduler(isProduction: true);
        var svc = BuildValidationService(sp, isProduction: true);
        var ex  = await Record.ExceptionAsync(() => svc.StartAsync(CancellationToken.None));
        Assert.NotNull(ex);
        Assert.IsType<InvalidOperationException>(ex);
        Assert.Contains("IAutomationScheduler", ex.Message);
    }

    // ── G1: In-memory runtime state store is flagged ──────────────────────

    [Fact]
    public async Task InMemoryRuntimeStateStore_Throws_InProduction()
    {
        var sp  = BuildWithInMemoryRuntimeStateStore(isProduction: true);
        var svc = BuildValidationService(sp, isProduction: true);
        var ex  = await Record.ExceptionAsync(() => svc.StartAsync(CancellationToken.None));
        Assert.NotNull(ex);
        Assert.IsType<InvalidOperationException>(ex);
        Assert.Contains("IAutomationRuntimeStateStore", ex.Message);
    }

    // ── G1: Non-EF configuration service is flagged ───────────────────────

    [Fact]
    public async Task FakeConfigurationService_Throws_InProduction()
    {
        var sp  = BuildWithFakeConfigurationService(isProduction: true);
        var svc = BuildValidationService(sp, isProduction: true);
        var ex  = await Record.ExceptionAsync(() => svc.StartAsync(CancellationToken.None));
        Assert.NotNull(ex);
        Assert.IsType<InvalidOperationException>(ex);
        Assert.Contains("IAutomationConfigurationService", ex.Message);
    }

    // ── G1: Non-EF idempotency service is flagged ─────────────────────────

    [Fact]
    public async Task FakeIdempotencyService_Throws_InProduction()
    {
        var sp  = BuildWithFakeIdempotencyService(isProduction: true);
        var svc = BuildValidationService(sp, isProduction: true);
        var ex  = await Record.ExceptionAsync(() => svc.StartAsync(CancellationToken.None));
        Assert.NotNull(ex);
        Assert.IsType<InvalidOperationException>(ex);
        Assert.Contains("IAutomationIdempotencyService", ex.Message);
    }

    // ── G1: Error message lists all violations when multiple exist ────────

    [Fact]
    public async Task MultipleViolations_ErrorMessageContainsAllViolatedServiceNames()
    {
        // Registry AND execution service are both non-EF
        var sp  = BuildWithMultipleViolations(isProduction: true);
        var svc = BuildValidationService(sp, isProduction: true);
        var ex  = await Record.ExceptionAsync(() => svc.StartAsync(CancellationToken.None));
        Assert.NotNull(ex);
        Assert.Contains("IAutomationRegistry", ex!.Message);
        Assert.Contains("IAutomationExecutionService", ex.Message);
    }

    // ─── Builders ─────────────────────────────────────────────────────────

    /// Builds the AutomationStoreValidationService directly (internal via InternalsVisibleTo).
    private static AutomationStoreValidationService BuildValidationService(
        IServiceProvider sp, bool isProduction)
    {
        var env = new FakeHostEnvironment(
            isProduction ? Environments.Production : Environments.Development);
        return new AutomationStoreValidationService(
            sp, env, NullLogger<AutomationStoreValidationService>.Instance);
    }

    private ServiceProvider BuildAllEfServices(bool isProduction)
    {
        var sc = new ServiceCollection();
        AddMinimalDeps(sc);
        sc.AddSingleton<IAutomationRuntimeStateStore, EfAutomationRuntimeStateStore>();
        sc.AddSingleton<IAutomationDeadLetterStore, EfAutomationDeadLetterStore>();
        sc.AddSingleton<IAutomationScheduler, EfAutomationScheduleStore>();
        sc.AddSingleton<IAutomationEventPublisher, NoopAutomationEventPublisher>();
        sc.AddSingleton<IAutomationRegistry, EfAutomationRegistry>();
        sc.AddSingleton<IAutomationConfigurationService, EfAutomationConfigurationService>();
        sc.AddSingleton<IAutomationIdempotencyService, EfAutomationIdempotencyService>();
        sc.AddScoped<IAutomationExecutionService, EfAutomationExecutionService>();
        return sc.BuildServiceProvider();
    }

    private ServiceProvider BuildWithInMemoryRegistry(bool isProduction)
    {
        var sc = new ServiceCollection();
        AddMinimalDeps(sc);
        sc.AddSingleton<IAutomationRuntimeStateStore, EfAutomationRuntimeStateStore>();
        sc.AddSingleton<IAutomationDeadLetterStore, EfAutomationDeadLetterStore>();
        sc.AddSingleton<IAutomationScheduler, EfAutomationScheduleStore>();
        sc.AddSingleton<IAutomationEventPublisher, NoopAutomationEventPublisher>();
        sc.AddSingleton<IAutomationRegistry, InMemoryAutomationRegistry>(); // ← violation
        sc.AddSingleton<IAutomationConfigurationService, EfAutomationConfigurationService>();
        sc.AddSingleton<IAutomationIdempotencyService, EfAutomationIdempotencyService>();
        sc.AddScoped<IAutomationExecutionService, EfAutomationExecutionService>();
        return sc.BuildServiceProvider();
    }

    private ServiceProvider BuildWithDefaultExecutionService(bool isProduction)
    {
        var sc = new ServiceCollection();
        AddMinimalDeps(sc);
        sc.AddSingleton<IAutomationRuntimeStateStore, EfAutomationRuntimeStateStore>();
        sc.AddSingleton<IAutomationDeadLetterStore, EfAutomationDeadLetterStore>();
        sc.AddSingleton<IAutomationScheduler, EfAutomationScheduleStore>();
        sc.AddSingleton<IAutomationEventPublisher, NoopAutomationEventPublisher>();
        sc.AddSingleton<IAutomationRegistry, EfAutomationRegistry>();
        sc.AddSingleton<IAutomationConfigurationService, EfAutomationConfigurationService>();
        sc.AddSingleton<IAutomationIdempotencyService, EfAutomationIdempotencyService>();
        sc.AddScoped<IAutomationExecutionService, DefaultAutomationExecutionService>(); // ← violation
        return sc.BuildServiceProvider();
    }

    private ServiceProvider BuildWithInMemoryDeadLetterStore(bool isProduction)
    {
        var sc = new ServiceCollection();
        AddMinimalDeps(sc);
        sc.AddSingleton<IAutomationRuntimeStateStore, EfAutomationRuntimeStateStore>();
        sc.AddSingleton<IAutomationDeadLetterStore, InMemoryAutomationDeadLetterStore>(); // ← violation
        sc.AddSingleton<IAutomationScheduler, EfAutomationScheduleStore>();
        sc.AddSingleton<IAutomationEventPublisher, NoopAutomationEventPublisher>();
        sc.AddSingleton<IAutomationRegistry, EfAutomationRegistry>();
        sc.AddSingleton<IAutomationConfigurationService, EfAutomationConfigurationService>();
        sc.AddSingleton<IAutomationIdempotencyService, EfAutomationIdempotencyService>();
        sc.AddScoped<IAutomationExecutionService, EfAutomationExecutionService>();
        return sc.BuildServiceProvider();
    }

    private ServiceProvider BuildWithDefaultScheduler(bool isProduction)
    {
        var sc = new ServiceCollection();
        AddMinimalDeps(sc);
        sc.AddSingleton<IAutomationRuntimeStateStore, EfAutomationRuntimeStateStore>();
        sc.AddSingleton<IAutomationDeadLetterStore, EfAutomationDeadLetterStore>();
        sc.AddSingleton<IAutomationScheduler, DefaultAutomationScheduler>(); // ← violation
        sc.AddSingleton<IAutomationEventPublisher, NoopAutomationEventPublisher>();
        sc.AddSingleton<IAutomationRegistry, EfAutomationRegistry>();
        sc.AddSingleton<IAutomationConfigurationService, EfAutomationConfigurationService>();
        sc.AddSingleton<IAutomationIdempotencyService, EfAutomationIdempotencyService>();
        sc.AddScoped<IAutomationExecutionService, EfAutomationExecutionService>();
        return sc.BuildServiceProvider();
    }

    private ServiceProvider BuildWithInMemoryRuntimeStateStore(bool isProduction)
    {
        var sc = new ServiceCollection();
        AddMinimalDeps(sc);
        sc.AddSingleton<IAutomationRuntimeStateStore, InMemoryAutomationRuntimeStateStore>(); // ← violation
        sc.AddSingleton<IAutomationDeadLetterStore, EfAutomationDeadLetterStore>();
        sc.AddSingleton<IAutomationScheduler, EfAutomationScheduleStore>();
        sc.AddSingleton<IAutomationEventPublisher, NoopAutomationEventPublisher>();
        sc.AddSingleton<IAutomationRegistry, EfAutomationRegistry>();
        sc.AddSingleton<IAutomationConfigurationService, EfAutomationConfigurationService>();
        sc.AddSingleton<IAutomationIdempotencyService, EfAutomationIdempotencyService>();
        sc.AddScoped<IAutomationExecutionService, EfAutomationExecutionService>();
        return sc.BuildServiceProvider();
    }

    private ServiceProvider BuildWithFakeConfigurationService(bool isProduction)
    {
        var sc = new ServiceCollection();
        AddMinimalDeps(sc);
        sc.AddSingleton<IAutomationRuntimeStateStore, EfAutomationRuntimeStateStore>();
        sc.AddSingleton<IAutomationDeadLetterStore, EfAutomationDeadLetterStore>();
        sc.AddSingleton<IAutomationScheduler, EfAutomationScheduleStore>();
        sc.AddSingleton<IAutomationEventPublisher, NoopAutomationEventPublisher>();
        sc.AddSingleton<IAutomationRegistry, EfAutomationRegistry>();
        sc.AddSingleton<IAutomationConfigurationService, StubConfigurationService>(); // ← violation: not EfAutomationConfigurationService
        sc.AddSingleton<IAutomationIdempotencyService, EfAutomationIdempotencyService>();
        sc.AddScoped<IAutomationExecutionService, EfAutomationExecutionService>();
        return sc.BuildServiceProvider();
    }

    private ServiceProvider BuildWithFakeIdempotencyService(bool isProduction)
    {
        var sc = new ServiceCollection();
        AddMinimalDeps(sc);
        sc.AddSingleton<IAutomationRuntimeStateStore, EfAutomationRuntimeStateStore>();
        sc.AddSingleton<IAutomationDeadLetterStore, EfAutomationDeadLetterStore>();
        sc.AddSingleton<IAutomationScheduler, EfAutomationScheduleStore>();
        sc.AddSingleton<IAutomationEventPublisher, NoopAutomationEventPublisher>();
        sc.AddSingleton<IAutomationRegistry, EfAutomationRegistry>();
        sc.AddSingleton<IAutomationConfigurationService, EfAutomationConfigurationService>();
        sc.AddSingleton<IAutomationIdempotencyService, StubIdempotencyService>(); // ← violation
        sc.AddScoped<IAutomationExecutionService, EfAutomationExecutionService>();
        return sc.BuildServiceProvider();
    }

    private ServiceProvider BuildWithMultipleViolations(bool isProduction)
    {
        var sc = new ServiceCollection();
        AddMinimalDeps(sc);
        sc.AddSingleton<IAutomationRuntimeStateStore, EfAutomationRuntimeStateStore>();
        sc.AddSingleton<IAutomationDeadLetterStore, EfAutomationDeadLetterStore>();
        sc.AddSingleton<IAutomationScheduler, EfAutomationScheduleStore>();
        sc.AddSingleton<IAutomationEventPublisher, NoopAutomationEventPublisher>();
        sc.AddSingleton<IAutomationRegistry, InMemoryAutomationRegistry>(); // ← violation 1
        sc.AddSingleton<IAutomationConfigurationService, EfAutomationConfigurationService>();
        sc.AddSingleton<IAutomationIdempotencyService, EfAutomationIdempotencyService>();
        sc.AddScoped<IAutomationExecutionService, DefaultAutomationExecutionService>(); // ← violation 2
        return sc.BuildServiceProvider();
    }

    /// <summary>Registers minimal DI dependencies shared across all test configurations.</summary>
    private void AddMinimalDeps(ServiceCollection sc)
    {
        sc.AddLogging();
        sc.Configure<XeniaAutomationOptions>(_ => { });

        // DbContextFactory with no real DB — validation only checks TYPE, never calls DB methods
        sc.AddDbContextFactory<XeniaDbContext>(opts =>
        {
            var serverVersion = new MySqlServerVersion(new Version(8, 0, 0));
            opts.UseMySql(FakeConnStr, serverVersion, my =>
            {
                my.MigrationsAssembly("Xenia.Infrastructure");
                my.CommandTimeout(5);
            });
        });
    }

    // ─── Test doubles ──────────────────────────────────────────────────────

    /// <summary>Not an EF implementation — triggers ValidateIsEfBacked violation.</summary>
    private sealed class StubConfigurationService : IAutomationConfigurationService
    {
        public Task<AutomationConfigurationEntry?> GetAsync(string k, string n, AutomationConfigurationScope s, Guid? t, CancellationToken ct = default) => Task.FromResult<AutomationConfigurationEntry?>(null);
        public Task<IReadOnlyList<AutomationConfigurationEntry>> ListAsync(string k, Guid? t, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<AutomationConfigurationEntry>>(Array.Empty<AutomationConfigurationEntry>());
        public Task<AutomationConfigurationEntry> UpsertAsync(AutomationConfigurationEntry e, CancellationToken ct = default) => Task.FromResult(e);
        public Task<bool> DeleteAsync(string k, string n, AutomationConfigurationScope s, Guid? t, CancellationToken ct = default) => Task.FromResult(false);
        public Task<AutomationConfigurationEntry?> GetEffectiveAsync(string k, string n, Guid? t, CancellationToken ct = default) => Task.FromResult<AutomationConfigurationEntry?>(null);
    }

    /// <summary>Not an EF implementation — triggers ValidateIsEfBacked violation.</summary>
    private sealed class StubIdempotencyService : IAutomationIdempotencyService
    {
        public Task<IdempotencyReservation> TryReserveAsync(
            Guid tenantId, string automationKey, string idempotencyKey,
            string requestFingerprint, DateTime expiresAt, CancellationToken ct = default) =>
            Task.FromResult(IdempotencyReservation.Reserved());

        public Task<bool> BindExecutionAsync(
            Guid tenantId, string automationKey, string idempotencyKey,
            Guid executionId, CancellationToken ct = default) =>
            Task.FromResult(true);

        public Task<AutomationIdempotencyRecord?> GetAsync(
            Guid tenantId, string automationKey, string idempotencyKey,
            CancellationToken ct = default) =>
            Task.FromResult<AutomationIdempotencyRecord?>(null);
    }

    private sealed class FakeHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Xenia.Tests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; }
            = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
