using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Xenia.Infrastructure.Persistence;

/// <summary>
/// Applies pending EF Core migrations during service startup.
/// Runs before any other hosted services that access the database.
/// </summary>
internal sealed class XeniaMigrationsHostedService : IHostedService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<XeniaMigrationsHostedService> _logger;

    public XeniaMigrationsHostedService(
        IServiceProvider services,
        ILogger<XeniaMigrationsHostedService> logger)
    {
        _services = services;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<XeniaDbContext>();

        _logger.LogInformation("Xenia: applying database migrations...");
        await db.Database.MigrateAsync(cancellationToken);
        _logger.LogInformation("Xenia: database migrations complete.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
