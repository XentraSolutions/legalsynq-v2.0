using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PlatformAuditEventService.Configuration;
using PlatformAuditEventService.Data;
using PlatformAuditEventService.Services;
using Serilog;

namespace PlatformAuditEventService.Tests.Helpers;

/// <summary>
/// Base integration test factory.
///
/// Overrides:
///   - Serilog            → isolated non-reloadable logger per factory (prevents
///                          "logger already frozen" when multiple factories run in the same session)
///   - EF Core DbContext  → fresh isolated InMemory database per factory instance
///   - Export:Provider    → "None" (prevents filesystem writes during tests)
///   - Logging providers  → cleared (keeps test output clean)
///   - Environment        → "Development" (loads appsettings.Development.json)
///
/// Auth defaults inherited from Development appsettings:
///   - IngestAuth:Mode = "None"  → ingest endpoints accept all requests unauthenticated
///   - QueryAuth:Mode  = "None"  → query endpoints resolve all callers as PlatformAdmin
/// </summary>
public class AuditServiceFactory : WebApplicationFactory<Program>
{
    protected override IHost CreateHost(IHostBuilder builder)
    {
        // Install a fresh, non-reloadable Serilog logger for this factory instance.
        // This prevents "The logger is already frozen" errors that occur when multiple
        // WebApplicationFactory instances are created in the same test run, because
        // Program.cs registers a global ReloadableLogger that can only be frozen once.
        // By registering a plain Logger AFTER the entry point, DI uses our registration
        // (last wins) and the ReloadableLogger.Freeze() is never invoked.
        builder.UseSerilog(
            new LoggerConfiguration()
                .MinimumLevel.Fatal()
                .CreateLogger(),
            dispose: true);

        return base.CreateHost(builder);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        var dbName = $"AuditEventDb-Test-{Guid.NewGuid():N}";

        builder.ConfigureServices(services =>
        {
            // Replace EF Core factory with a fresh isolated InMemory database so that
            // each factory instance (and thus each test class) starts with an empty store.
            var existing = services
                .Where(d => d.ServiceType == typeof(IDbContextFactory<AuditEventDbContext>))
                .ToList();
            foreach (var d in existing) services.Remove(d);

            services.AddDbContextFactory<AuditEventDbContext>(
                opts => opts.UseInMemoryDatabase(dbName));

            // Override Export:Provider → "None" so tests don't write to filesystem.
            services.Configure<ExportOptions>(opts => opts.Provider = "None");
        });

        builder.ConfigureLogging(logging => logging.ClearProviders());
    }
}

/// <summary>
/// Integration test factory with <c>IngestAuth:Mode = "ServiceToken"</c> active.
///
/// Exposes <see cref="ValidToken"/> — the pre-shared secret callers must send
/// in the <c>x-service-token</c> header to authenticate ingest requests.
///
/// Uses <c>Configure&lt;IngestAuthOptions&gt;</c> (options post-configuration) rather than
/// <c>ConfigureAppConfiguration</c> to guarantee the override wins over appsettings.Development.json.
///
/// Inherits all other overrides from <see cref="AuditServiceFactory"/>.
/// </summary>
public class ServiceTokenAuditFactory : AuditServiceFactory
{
    public const string ValidToken  = "test-service-token-abc123-integration";
    public const string ServiceName = "test-service";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            // ── Options layer override ────────────────────────────────────────
            // Makes IngestAuthOptions.Mode and ServiceTokens visible to any code
            // that reads via IOptions<IngestAuthOptions> (e.g. ServiceTokenAuthenticator).
            services.Configure<IngestAuthOptions>(opts =>
            {
                opts.Mode = "ServiceToken";
                opts.ServiceTokens =
                [
                    new ServiceTokenEntry
                    {
                        Token       = ValidToken,
                        ServiceName = ServiceName,
                        Enabled     = true,
                    },
                ];
            });

            // ── IIngestAuthenticator DI override ─────────────────────────────
            // Program.cs registers IIngestAuthenticator using a factory lambda that captures
            // the raw configuration value of IngestAuth:Mode AT STARTUP (before ConfigureWebHost
            // runs). This means options-layer overrides are too late to affect which
            // IIngestAuthenticator implementation the factory picks.
            //
            // Fix: remove the existing singleton factory and register ServiceTokenAuthenticator
            // directly (it is already registered as a concrete singleton by Program.cs, so
            // sp.GetRequiredService<ServiceTokenAuthenticator>() is safe here).
            var existing = services
                .Where(d => d.ServiceType == typeof(IIngestAuthenticator))
                .ToList();
            foreach (var d in existing) services.Remove(d);

            services.AddSingleton<IIngestAuthenticator>(
                sp => sp.GetRequiredService<ServiceTokenAuthenticator>());
        });
    }
}
