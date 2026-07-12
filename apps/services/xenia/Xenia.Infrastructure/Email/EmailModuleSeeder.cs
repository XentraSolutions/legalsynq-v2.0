using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xenia.Application.Email;
using Xenia.Application.Modules;
using Xenia.Infrastructure.Persistence;

namespace Xenia.Infrastructure.Email;

/// <summary>
/// Seeds the Email module registration at startup (idempotent).
///
/// If the "email" module is already registered, this is a no-op.
/// Runs after XeniaMigrationsHostedService to ensure the schema exists.
/// </summary>
internal sealed class EmailModuleSeeder : IHostedService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<EmailModuleSeeder> _logger;

    public EmailModuleSeeder(IServiceProvider services, ILogger<EmailModuleSeeder> logger)
    {
        _services = services;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<XeniaDbContext>();

        var exists = await db.Modules
            .AnyAsync(m => m.ModuleKey == EmailModuleKeys.ModuleKey, cancellationToken);

        if (exists)
        {
            _logger.LogDebug(
                "Email module already registered — skipping seed. ModuleKey={ModuleKey}",
                EmailModuleKeys.ModuleKey);
            return;
        }

        var registry = scope.ServiceProvider.GetRequiredService<IModuleRegistry>();
        await registry.RegisterModuleAsync(
            moduleKey: EmailModuleKeys.ModuleKey,
            name: EmailModuleKeys.ModuleName,
            version: EmailModuleKeys.ModuleVersion,
            description: EmailModuleKeys.ModuleDescription,
            configurationNamespace: EmailModuleKeys.ConfigurationNamespace,
            cancellationToken);

        _logger.LogInformation(
            "Email module registered. Key={ModuleKey} Version={Version}",
            EmailModuleKeys.ModuleKey, EmailModuleKeys.ModuleVersion);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
