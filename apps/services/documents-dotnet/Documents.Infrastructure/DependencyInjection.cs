using Documents.Application.Services;
using Documents.Domain.Interfaces;
using Documents.Infrastructure.Health;
using Documents.Infrastructure.TokenStore;
using Documents.Infrastructure.Database;
using Documents.Infrastructure.Scanner;
using Documents.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace Documents.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration          config)
    {
        // ── PostgreSQL / EF Core ─────────────────────────────────────────────
        var connStr = config.GetConnectionString("DocsDb")
            ?? throw new InvalidOperationException("Connection string 'DocsDb' is required.");

        services.AddDbContext<DocsDbContext>(opts =>
            opts.UseNpgsql(connStr, npg =>
                npg.MigrationsAssembly(typeof(DocsDbContext).Assembly.FullName)));

        // ── Repositories ─────────────────────────────────────────────────────
        services.AddScoped<IDocumentRepository,        DocumentRepository>();
        services.AddScoped<IDocumentVersionRepository, DocumentVersionRepository>();
        services.AddScoped<IAuditRepository,           AuditRepository>();

        // ── Storage provider ─────────────────────────────────────────────────
        var storageProvider = config["Storage:Provider"] ?? "local";
        services.Configure<LocalStorageOptions>(config.GetSection("Storage:Local"));
        services.Configure<S3StorageOptions>(config.GetSection("Storage:S3"));
        services.AddSingleton<LocalStorageProvider>();
        services.AddSingleton<S3StorageProvider>();
        services.AddSingleton<IStorageProvider>(sp =>
            StorageProviderFactory.Create(storageProvider, sp));

        // ── File scanner ─────────────────────────────────────────────────────
        var scannerProvider = config["Scanner:Provider"] ?? "none";
        services.Configure<MockScannerOptions>(config.GetSection("Scanner:Mock"));
        services.Configure<ClamAvOptions>(config.GetSection("Scanner:ClamAv"));
        services.AddSingleton<NullScannerProvider>();
        services.AddSingleton<MockScannerProvider>();
        services.AddSingleton<ClamAvFileScannerProvider>();
        services.AddSingleton<IFileScannerProvider>(sp => scannerProvider switch
        {
            "clamav" => sp.GetRequiredService<ClamAvFileScannerProvider>(),
            "mock"   => sp.GetRequiredService<MockScannerProvider>(),
            _        => sp.GetRequiredService<NullScannerProvider>(),
        });

        // ── Scan worker options ───────────────────────────────────────────────
        services.Configure<ScanWorkerOptions>(config.GetSection("ScanWorker"));
        var workerOpts = config.GetSection("ScanWorker").Get<ScanWorkerOptions>() ?? new();

        // ── Scan job queue ────────────────────────────────────────────────────
        if (workerOpts.QueueProvider.Equals("redis", StringComparison.OrdinalIgnoreCase))
        {
            var redisUrl = config["Redis:Url"]
                ?? throw new InvalidOperationException("Redis:Url required when ScanWorker:QueueProvider=redis");

            if (!services.Any(s => s.ServiceType == typeof(IConnectionMultiplexer)))
                services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisUrl));

            services.AddSingleton<IScanJobQueue, RedisScanJobQueue>();
        }
        else
        {
            services.AddSingleton<IScanJobQueue>(sp =>
            {
                var log      = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<InMemoryScanJobQueue>>();
                var capacity = workerOpts.QueueCapacity;
                return new InMemoryScanJobQueue(log, capacity);
            });
        }

        // ── Access token store ───────────────────────────────────────────────
        var tokenStore = config["AccessToken:Store"] ?? "memory";
        if (tokenStore == "redis")
        {
            var redisUrl = config["Redis:Url"]
                ?? throw new InvalidOperationException("Redis:Url required when AccessToken:Store=redis");

            if (!services.Any(s => s.ServiceType == typeof(IConnectionMultiplexer)))
                services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisUrl));

            services.AddSingleton<IAccessTokenStore, RedisAccessTokenStore>();
        }
        else
        {
            services.AddSingleton<IAccessTokenStore, InMemoryAccessTokenStore>();
        }

        // ── Health checks ─────────────────────────────────────────────────────
        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database", failureStatus: HealthStatus.Unhealthy, tags: new[] { "ready", "live" })
            .AddCheck<ClamAvHealthCheck>  ("clamav",   failureStatus: HealthStatus.Degraded,  tags: new[] { "ready" });

        // ── Application services ─────────────────────────────────────────────
        services.Configure<DocumentServiceOptions>(config.GetSection("Documents"));
        services.Configure<AccessTokenOptions>(config.GetSection("AccessToken"));
        services.AddScoped<ScanService>();
        services.AddScoped<ScanOrchestrationService>();
        services.AddScoped<AuditService>();
        services.AddScoped<DocumentService>();
        services.AddScoped<AccessTokenService>();

        return services;
    }
}
