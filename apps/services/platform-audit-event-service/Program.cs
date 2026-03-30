using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Events;
using PlatformAuditEventService.Configuration;
using PlatformAuditEventService.Data;
using PlatformAuditEventService.DTOs;
using PlatformAuditEventService.Middleware;
using PlatformAuditEventService.Repositories;
using PlatformAuditEventService.Services;
using PlatformAuditEventService.Validators;

// ── Bootstrap logger ─────────────────────────────────────────────────────────
// Captures startup errors before full Serilog is configured from appsettings.
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting Platform Audit/Event Service");

    var builder = WebApplication.CreateBuilder(args);

    // ── Serilog ───────────────────────────────────────────────────────────────
    builder.Host.UseSerilog((ctx, services, cfg) =>
        cfg.ReadFrom.Configuration(ctx.Configuration)
           .ReadFrom.Services(services)
           .Enrich.FromLogContext());

    // ── Bind all configuration sections ───────────────────────────────────────
    var cfg = builder.Configuration;

    builder.Services.Configure<AuditServiceOptions>(cfg.GetSection(AuditServiceOptions.SectionName));
    builder.Services.Configure<DatabaseOptions>(cfg.GetSection(DatabaseOptions.SectionName));
    builder.Services.Configure<IntegrityOptions>(cfg.GetSection(IntegrityOptions.SectionName));
    builder.Services.Configure<IngestAuthOptions>(cfg.GetSection(IngestAuthOptions.SectionName));
    builder.Services.Configure<QueryAuthOptions>(cfg.GetSection(QueryAuthOptions.SectionName));
    builder.Services.Configure<RetentionOptions>(cfg.GetSection(RetentionOptions.SectionName));
    builder.Services.Configure<ExportOptions>(cfg.GetSection(ExportOptions.SectionName));

    // Eager-read options we need during startup wiring
    var svcOpts = cfg.GetSection(AuditServiceOptions.SectionName).Get<AuditServiceOptions>()  ?? new();
    var dbOpts  = cfg.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>()          ?? new();

    // ── Resolve connection string ─────────────────────────────────────────────
    // Priority order:
    //   1. Database:ConnectionString in appsettings / env
    //   2. ConnectionStrings:AuditEventDb (standard ASP.NET Core convention)
    var connectionString =
        dbOpts.ConnectionString
        ?? cfg.GetConnectionString("AuditEventDb");

    // ── Database + Repository wiring ─────────────────────────────────────────
    switch (dbOpts.Provider)
    {
        case "MySQL":
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException(
                    "Database:Provider is 'MySQL' but no connection string was found. " +
                    "Set Database:ConnectionString or ConnectionStrings:AuditEventDb.");

            var serverVersion = ServerVersion.Parse(dbOpts.ServerVersion);

            builder.Services.AddDbContextFactory<AuditEventDbContext>(opts =>
            {
                opts.UseMySql(connectionString, serverVersion, mysql =>
                {
                    mysql.CommandTimeout(dbOpts.CommandTimeoutSeconds);
                    mysql.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(5),
                        errorNumbersToAdd: null);
                });

                if (dbOpts.EnableSensitiveDataLogging)
                    opts.EnableSensitiveDataLogging();

                if (dbOpts.EnableDetailedErrors)
                    opts.EnableDetailedErrors();
            });

            builder.Services.AddScoped<IAuditEventRepository, EfAuditEventRepository>();
            Log.Information("Persistence: MySQL | Provider={Provider}", dbOpts.ServerVersion);
            break;

        default: // "InMemory"
            builder.Services.AddDbContextFactory<AuditEventDbContext>(opts =>
                opts.UseInMemoryDatabase("AuditEventDb"));

            builder.Services.AddSingleton<IAuditEventRepository, InMemoryAuditEventRepository>();
            Log.Warning("Persistence: InMemory — data is not durable. Set Database:Provider=MySQL for production.");
            break;
    }

    // ── New entity repositories (EF-backed for both MySQL and InMemory modes) ─
    builder.Services.AddScoped<IAuditEventRecordRepository, EfAuditEventRecordRepository>();
    builder.Services.AddScoped<IAuditExportJobRepository,   EfAuditExportJobRepository>();
    builder.Services.AddScoped<IIntegrityCheckpointRepository,     EfIntegrityCheckpointRepository>();
    builder.Services.AddScoped<IIngestSourceRegistrationRepository, EfIngestSourceRegistrationRepository>();

    // ── Controllers + API behavior ────────────────────────────────────────────
    // JsonStringEnumConverter ensures all typed enums (EventCategory, SeverityLevel,
    // ActorType, ScopeType, VisibilityScope, ExportStatus) serialise as strings in
    // both request binding and response output — keeps payloads human-readable
    // without requiring callers to know the underlying tinyint values.
    builder.Services.AddControllers()
        .AddJsonOptions(opts =>
        {
            opts.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });
    builder.Services.AddEndpointsApiExplorer();

    // ── Swagger / OpenAPI ─────────────────────────────────────────────────────
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo
        {
            Title       = svcOpts.ServiceName,
            Version     = $"v{svcOpts.Version}",
            Description =
                "Standalone, independently deployable service that ingests business, security, " +
                "access, administrative, and system activity from distributed systems, normalizes " +
                "it into a canonical event model, and persists immutable, tamper-evident audit records.",
            Contact = new OpenApiContact
            {
                Name  = "LegalSynq Platform Team",
                Email = "platform@legalsynq.com",
            },
        });
        c.DescribeAllParametersInCamelCase();
        c.UseInlineDefinitionsForEnums();
    });

    // ── Validation ────────────────────────────────────────────────────────────
    // Auto-discovers and registers all AbstractValidator<T> implementations
    // in this assembly. Registered as Scoped by default.
    // Covers: IngestAuditEventRequestValidator, BatchIngestRequestValidator,
    //         AuditEventQueryRequestValidator, ExportRequestValidator,
    //         AuditEventScopeDtoValidator, AuditEventActorDtoValidator,
    //         AuditEventEntityDtoValidator
    builder.Services.AddValidatorsFromAssemblyContaining<IngestAuditEventRequestValidator>();

    // ── Domain services ───────────────────────────────────────────────────────
    builder.Services.AddScoped<IAuditEventService, AuditEventService>();

    // ── CORS ──────────────────────────────────────────────────────────────────
    var allowedOrigins = svcOpts.AllowedCorsOrigins;
    builder.Services.AddCors(opts =>
        opts.AddDefaultPolicy(policy =>
        {
            if (allowedOrigins.Count == 0 || allowedOrigins.Contains("*"))
                policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
            else
                policy.WithOrigins([.. allowedOrigins]).AllowAnyHeader().AllowAnyMethod();
        }));

    // ── Health checks ─────────────────────────────────────────────────────────
    var healthBuilder = builder.Services.AddHealthChecks();
    if (dbOpts.Provider == "MySQL" && !string.IsNullOrWhiteSpace(connectionString))
    {
        healthBuilder.AddCheck("mysql", () =>
        {
            // Lightweight structural check — full probe runs at startup via VerifyDatabaseConnectionAsync
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("MySQL configured");
        });
    }

    // ── Build ─────────────────────────────────────────────────────────────────
    var app = builder.Build();

    // ── Startup DB verification (non-fatal probe) ─────────────────────────────
    if (dbOpts.Provider == "MySQL" && dbOpts.VerifyConnectionOnStartup)
    {
        await VerifyDatabaseConnectionAsync(app.Services, dbOpts, app.Logger);
    }

    // ── Startup migration (opt-in) ────────────────────────────────────────────
    if (dbOpts.Provider == "MySQL" && dbOpts.MigrateOnStartup)
    {
        await RunMigrationsAsync(app.Services, app.Logger);
    }

    // ── Middleware pipeline ───────────────────────────────────────────────────
    app.UseMiddleware<ExceptionMiddleware>();
    app.UseMiddleware<CorrelationIdMiddleware>();

    app.UseSerilogRequestLogging(opts =>
    {
        opts.MessageTemplate =
            "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
        opts.GetLevel = (ctx, elapsed, ex) =>
            ex is not null || ctx.Response.StatusCode >= 500
                ? LogEventLevel.Error
                : elapsed > 1_000
                    ? LogEventLevel.Warning
                    : LogEventLevel.Information;
    });

    app.UseCors();

    var showSwagger = app.Environment.IsDevelopment() || svcOpts.ExposeSwagger;
    if (showSwagger)
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", $"{svcOpts.ServiceName} v{svcOpts.Version}");
            c.RoutePrefix = "swagger";
            c.DisplayRequestDuration();
        });
    }

    app.UseAuthorization();
    app.MapControllers();
    app.MapHealthChecks("/health");

    // ── Startup summary ───────────────────────────────────────────────────────
    Log.Information(
        "Platform Audit/Event Service ready | Version={Version} | Env={Env} | DB={DbProvider} | Swagger={Swagger}",
        svcOpts.Version,
        app.Environment.EnvironmentName,
        dbOpts.Provider,
        showSwagger ? "enabled" : "disabled");

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Platform Audit/Event Service terminated unexpectedly.");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}

return 0;

// ── Startup helpers ───────────────────────────────────────────────────────────

static async Task VerifyDatabaseConnectionAsync(
    IServiceProvider services, DatabaseOptions dbOpts, Microsoft.Extensions.Logging.ILogger logger)
{
    using var cts = new CancellationTokenSource(
        TimeSpan.FromSeconds(dbOpts.StartupProbeTimeoutSeconds));

    try
    {
        await using var scope = services.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AuditEventDbContext>>();
        await using var db   = await factory.CreateDbContextAsync(cts.Token);
        var connected = await db.Database.CanConnectAsync(cts.Token);

        if (connected)
            logger.LogInformation("DB connectivity probe: MySQL connection successful.");
        else
            logger.LogWarning("DB connectivity probe: MySQL reported not connected (CanConnect=false).");
    }
    catch (OperationCanceledException)
    {
        logger.LogWarning(
            "DB connectivity probe timed out after {Timeout}s. Service will start but may be degraded.",
            dbOpts.StartupProbeTimeoutSeconds);
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex,
            "DB connectivity probe failed. Service will start but database operations may fail. " +
            "Check Database:ConnectionString and ensure MySQL is reachable.");
    }
}

static async Task RunMigrationsAsync(IServiceProvider services, Microsoft.Extensions.Logging.ILogger logger)
{
    try
    {
        logger.LogInformation("Running EF Core migrations...");
        await using var scope = services.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AuditEventDbContext>>();
        await using var db   = await factory.CreateDbContextAsync();
        await db.Database.MigrateAsync();
        logger.LogInformation("EF Core migrations applied successfully.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "EF Core migration failed. Check migration state and DB connectivity.");
        throw;
    }
}
