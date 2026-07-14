using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Xenia.Infrastructure.Persistence;

/// <summary>
/// Applies pending EF Core migrations during service startup.
/// Runs before any other hosted services that access the database.
///
/// Set <c>Xenia:SkipMigrations=true</c> (env: <c>Xenia__SkipMigrations=true</c>) to skip
/// this step — for example when the schema was applied externally via a SQL script or a
/// DBA-managed migration process, or when running with pre-applied schema in CI smoke tests.
/// </summary>
internal sealed class XeniaMigrationsHostedService : IHostedService
{
    private readonly IServiceProvider _services;
    private readonly IConfiguration    _config;
    private readonly ILogger<XeniaMigrationsHostedService> _logger;

    public XeniaMigrationsHostedService(
        IServiceProvider services,
        IConfiguration    config,
        ILogger<XeniaMigrationsHostedService> logger)
    {
        _services = services;
        _config   = config;
        _logger   = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_config.GetValue<bool>("Xenia:SkipMigrations"))
        {
            _logger.LogInformation(
                "Xenia: SkipMigrations=true — skipping auto-migration. " +
                "Schema must be applied externally before startup.");
            return;
        }

        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<XeniaDbContext>();

        var isInMemory = db.Database.ProviderName?.Contains("InMemory", StringComparison.OrdinalIgnoreCase) == true;
        if (isInMemory)
        {
            _logger.LogInformation("Xenia: InMemory mode — skipping migrations (data will not persist across restarts).");
        }
        else
        {
            try
            {
                _logger.LogInformation("Xenia: applying database migrations...");
                await db.Database.MigrateAsync(cancellationToken);
                _logger.LogInformation("Xenia: database migrations complete.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Xenia: database migration failed — service will start without a working database. " +
                    "DB-dependent endpoints will return errors. " +
                    "Ensure ConnectionStrings__XeniaDb is set to a reachable MySQL instance.");
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
