using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using Xenia.Infrastructure.Persistence;

namespace Xenia.Tests.Automation.Infrastructure;

/// <summary>
/// xUnit shared fixture that provisions a real MySQL schema (xenia_automation_test)
/// and exposes a pre-migrated <see cref="IDbContextFactory{T}"/> for relational tests.
///
/// MySQL container expected at 127.0.0.1:13309 (docker xenia-test-mysql-v1).
/// Schema is created once per test class via IClassFixture&lt;XeniaRelationalFixture&gt;.
/// Tests must request truncation explicitly via <see cref="TruncateAutomationTablesAsync"/>.
///
/// Usage:
///   [Collection("XeniaRelational")]
///   public class MyTests : IAsyncLifetime
///   {
///       private readonly XeniaRelationalFixture _fx;
///       public MyTests(XeniaRelationalFixture fx) => _fx = fx;
///       public async Task InitializeAsync() => await _fx.TruncateAutomationTablesAsync();
///   }
/// </summary>
public sealed class XeniaRelationalFixture : IAsyncLifetime
{
    public const string ConnectionString =
        "Server=127.0.0.1;Port=13309;Database=xenia_automation_test;" +
        "Uid=root;Pwd=xeniatest123;AllowPublicKeyRetrieval=true;SslMode=None;";

    private ServiceProvider? _sp;

    public IDbContextFactory<XeniaDbContext> ContextFactory =>
        _sp!.GetRequiredService<IDbContextFactory<XeniaDbContext>>();

    public Task InitializeAsync() => SetupAsync(ConnectionString);

    public async Task SetupAsync(string connectionString = ConnectionString)
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));

        services.AddDbContextFactory<XeniaDbContext>(opts =>
        {
            var serverVersion = new MySqlServerVersion(new Version(8, 0, 0));
            opts.UseMySql(connectionString, serverVersion, my =>
            {
                my.MigrationsAssembly("Xenia.Infrastructure");
                my.CommandTimeout(30);
            });
        });

        _sp = services.BuildServiceProvider();

        // Apply all EF migrations on startup
        await using var ctx = await ContextFactory.CreateDbContextAsync();
        await ctx.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_sp is not null)
            await _sp.DisposeAsync();
    }

    /// <summary>
    /// Truncates all automation tables. Call in each test's InitializeAsync to ensure isolation.
    /// </summary>
    public async Task TruncateAutomationTablesAsync()
    {
        await using var ctx = await ContextFactory.CreateDbContextAsync();
        // Disable FK checks, truncate all automation tables, re-enable FK checks
        await ctx.Database.ExecuteSqlRawAsync(
            "SET FOREIGN_KEY_CHECKS=0; " +
            "TRUNCATE TABLE xn_automation_idempotency; " +
            "TRUNCATE TABLE xn_automation_dead_letters; " +
            "TRUNCATE TABLE xn_automation_executions; " +
            "TRUNCATE TABLE xn_automation_runtime_state; " +
            "TRUNCATE TABLE xn_automation_configuration; " +
            "TRUNCATE TABLE xn_tenant_automations; " +
            "TRUNCATE TABLE xn_automation_versions; " +
            "TRUNCATE TABLE xn_automation_registry; " +
            "TRUNCATE TABLE xn_automation_schedules; " +
            "SET FOREIGN_KEY_CHECKS=1;");
    }

    /// <summary>Creates a fresh DbContext (short-lived, caller must dispose).</summary>
    public async Task<XeniaDbContext> CreateContextAsync() =>
        await ContextFactory.CreateDbContextAsync();
}

[CollectionDefinition("XeniaRelational")]
public sealed class XeniaRelationalCollection : ICollectionFixture<XeniaRelationalFixture> { }
