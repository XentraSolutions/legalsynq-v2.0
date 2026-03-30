using Documents.Application.Services;
using Documents.Domain.Interfaces;
using Documents.Infrastructure.Health;
using Documents.Infrastructure.Notifications;
using Documents.Infrastructure.TokenStore;
using Documents.Infrastructure.Database;
using Documents.Infrastructure.Scanner;
using Documents.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
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
        services.AddSingleton<IFileScannerProvider>(sp =>
        {
            if (!scannerProvider.Equals("clamav", StringComparison.OrdinalIgnoreCase))
            {
                return scannerProvider.Equals("mock", StringComparison.OrdinalIgnoreCase)
                    ? sp.GetRequiredService<MockScannerProvider>()
                    : (IFileScannerProvider)sp.GetRequiredService<NullScannerProvider>();
            }

            // Wrap ClamAV with the circuit breaker (infrastructure layer only, no leakage into controllers)
            var inner      = sp.GetRequiredService<ClamAvFileScannerProvider>();
            var clamavOpts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ClamAvOptions>>().Value;
            var cbLog      = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<CircuitBreakerScannerProvider>>();
            return new CircuitBreakerScannerProvider(inner, clamavOpts.CircuitBreaker, cbLog);
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

        // ── ClamAV signature freshness monitor (singleton, cached 5 min) ─────
        services.AddSingleton<ClamAvSignatureFreshnessMonitor>();

        // ── Health checks ─────────────────────────────────────────────────────
        var redisActive = services.Any(s => s.ServiceType == typeof(IConnectionMultiplexer));
        var healthBuilder = services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>       ("database",          failureStatus: HealthStatus.Unhealthy, tags: new[] { "ready", "live" })
            .AddCheck<ClamAvHealthCheck>          ("clamav",            failureStatus: HealthStatus.Degraded,  tags: new[] { "ready" })
            .AddCheck<ClamAvSignatureHealthCheck> ("clamav-signatures", failureStatus: HealthStatus.Degraded,  tags: new[] { "ready" });

        if (redisActive)
        {
            healthBuilder.AddCheck<RedisHealthCheck>("redis",
                failureStatus: HealthStatus.Unhealthy,
                tags: new[] { "ready" });
        }

        // ── Scan completion notifications ─────────────────────────────────────
        services.Configure<NotificationOptions>(config.GetSection("Notifications"));
        var notifyProvider = config["Notifications:ScanCompletion:Provider"] ?? "log";

        // Validate: redis publisher requires an active Redis connection
        if (notifyProvider.Equals("redis", StringComparison.OrdinalIgnoreCase) && !redisActive)
        {
            using var warnLog = LoggerFactory.Create(b => b.AddConsole());
            warnLog.CreateLogger(nameof(DependencyInjection)).LogWarning(
                "Notifications:ScanCompletion:Provider=redis but no Redis connection is configured. " +
                "Falling back to 'log' publisher. Add Redis:Url and set a Redis-backed queue or token store.");
        }

        services.AddSingleton<IScanCompletionPublisher>(sp =>
            notifyProvider.ToLowerInvariant() switch
            {
                "none" => (IScanCompletionPublisher) new NullScanCompletionPublisher(),
                "redis" when redisActive => new RedisScanCompletionPublisher(
                    sp.GetRequiredService<IConnectionMultiplexer>(),
                    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<NotificationOptions>>(),
                    sp.GetRequiredService<ILogger<RedisScanCompletionPublisher>>()),
                _ => new LogScanCompletionPublisher(
                    sp.GetRequiredService<ILogger<LogScanCompletionPublisher>>()),
            });

        // ── Application services ─────────────────────────────────────────────
        services.Configure<DocumentServiceOptions>(config.GetSection("Documents"));
        services.Configure<AccessTokenOptions>(config.GetSection("AccessToken"));
        services.AddScoped<ScanService>();
        services.AddScoped<ScanOrchestrationService>();
        services.AddScoped<AuditService>();
        services.AddScoped<DocumentService>();
        services.AddScoped<AccessTokenService>();

        // ── Startup configuration validation ─────────────────────────────────
        ValidateFileSizeConfiguration(config);

        return services;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Validates that file-size configuration is internally consistent.
    /// Fails startup on contradictory settings (upload limit > scan limit).
    /// Warns when application scan limit exceeds the ClamAV technical limit.
    /// </summary>
    private static void ValidateFileSizeConfiguration(IConfiguration config)
    {
        var docOpts    = config.GetSection("Documents").Get<DocumentServiceOptions>() ?? new();
        var clamAvOpts = config.GetSection("Scanner:ClamAv").Get<ClamAvOptions>()     ?? new();

        // HARD FAIL: upload limit must not exceed scan limit (documents would be accepted but never scannable)
        if (docOpts.MaxUploadSizeMb > docOpts.MaxScannableFileSizeMb)
        {
            throw new InvalidOperationException(
                $"Configuration error: Documents:MaxUploadSizeMb ({docOpts.MaxUploadSizeMb} MB) " +
                $"exceeds Documents:MaxScannableFileSizeMb ({docOpts.MaxScannableFileSizeMb} MB). " +
                $"Files could be uploaded but never scanned. " +
                $"Reduce MaxUploadSizeMb or raise MaxScannableFileSizeMb.");
        }

        // WARN: application scan limit exceeds ClamAV's own configured technical limit
        if (docOpts.MaxScannableFileSizeMb > clamAvOpts.MaxScannableFileSizeMb)
        {
            // Use a temporary logger factory since the DI container isn't built yet
            using var logFactory = LoggerFactory.Create(b => b.AddConsole());
            var log = logFactory.CreateLogger(nameof(DependencyInjection));
            log.LogWarning(
                "Configuration advisory: Documents:MaxScannableFileSizeMb ({AppLimit} MB) exceeds " +
                "Scanner:ClamAv:MaxScannableFileSizeMb ({ClamAvLimit} MB). " +
                "ClamAV may reject scans for files between these limits. " +
                "Ensure ClamAV's StreamMaxLength is aligned.",
                docOpts.MaxScannableFileSizeMb, clamAvOpts.MaxScannableFileSizeMb);
        }
    }
}
