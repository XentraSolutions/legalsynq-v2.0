using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Events;
using PlatformAuditEventService.Authorization;
using PlatformAuditEventService.Configuration;
using PlatformAuditEventService.Data;
using PlatformAuditEventService.DTOs;
using PlatformAuditEventService.Middleware;
using PlatformAuditEventService.Repositories;
using PlatformAuditEventService.Services;
using PlatformAuditEventService.Validators;

// Disambiguate legacy IngestAuditEventRequest
using IngestAuditEventRequest = PlatformAuditEventService.DTOs.Ingest.IngestAuditEventRequest;

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
                "it into a canonical event model, and persists immutable, tamper-evident audit records.\n\n" +
                "**Endpoint groups:**\n" +
                "- `/audit/events` — Canonical query surface (filtered list + single record by AuditId).\n" +
                "- `/audit/entity/{entityType}/{entityId}` — Entity-scoped event history.\n" +
                "- `/audit/actor/{actorId}` — Actor-scoped event history.\n" +
                "- `/audit/user/{userId}` — User-scoped event history (actorType=User).\n" +
                "- `/audit/tenant/{tenantId}` — Tenant-scoped event history.\n" +
                "- `/audit/organization/{organizationId}` — Organization-scoped event history.\n" +
                "- `/internal/audit/events` — Machine-to-machine ingestion (single + batch). Internal only.\n" +
                "- `/api/auditevents` — Legacy event ingestion and query (to be superseded).\n" +
                "- `/health` — Service liveness and event count probe.",
            Contact = new OpenApiContact
            {
                Name  = "LegalSynq Platform Team",
                Email = "platform@legalsynq.com",
            },
        });
        c.DescribeAllParametersInCamelCase();
        c.UseInlineDefinitionsForEnums();

        // Wire up XML documentation — surfaces controller/action <summary> comments
        // and <response> codes in the Swagger UI "Description" and response sections.
        var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (File.Exists(xmlPath))
            c.IncludeXmlComments(xmlPath);
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

    // Canonical ingestion pipeline — targets AuditEventRecord + IAuditEventRecordRepository.
    // Replaces the legacy AuditEventService for all new ingest surface area.
    // To switch transports (queued / outbox), register a different IAuditEventRecordRepository
    // implementation in place of EfAuditEventRecordRepository above.
    builder.Services.AddScoped<IAuditEventIngestionService, AuditEventIngestionService>();

    // Canonical query/retrieval pipeline — read-only surface.
    // Applies QueryAuth options (page-size cap, hash exposure) and maps entities → response DTOs.
    builder.Services.AddScoped<IAuditEventQueryService, AuditEventQueryService>();

    // ── Query authorization ───────────────────────────────────────────────────
    // All resolvers registered as singletons (stateless after construction).
    // IQueryCallerResolver resolves to the implementation matching QueryAuth:Mode.
    //
    // To add a new auth mode (e.g. mTLS, API key):
    //   1. Implement IQueryCallerResolver in a new class.
    //   2. Add builder.Services.AddSingleton<YourResolver>() here.
    //   3. Add a case to the switch below.
    //   4. Document the new mode in Docs/query-authorization-model.md.
    builder.Services.AddSingleton<AnonymousCallerResolver>();
    builder.Services.AddSingleton<ClaimsCallerResolver>();

    var queryAuthMode = cfg.GetSection(QueryAuthOptions.SectionName)["Mode"] ?? "None";

    builder.Services.AddSingleton<IQueryCallerResolver>(sp => queryAuthMode switch
    {
        "Bearer" => sp.GetRequiredService<ClaimsCallerResolver>(),
        // Future modes:
        // "ApiKey"   => sp.GetRequiredService<ApiKeyCallerResolver>(),
        // "MtlsHeader" => sp.GetRequiredService<MtlsCallerResolver>(),
        _ => sp.GetRequiredService<AnonymousCallerResolver>(),
    });

    // IQueryAuthorizer applies scope constraints and enforces access rules.
    // Registered as singleton — stateless, reads only from options.
    builder.Services.AddSingleton<IQueryAuthorizer, QueryAuthorizer>();

    if (queryAuthMode.Equals("None", StringComparison.OrdinalIgnoreCase))
    {
        Log.Warning(
            "QueryAuth:Mode = 'None' — query endpoints are unauthenticated and " +
            "all callers receive PlatformAdmin scope. " +
            "Set Mode=Bearer and configure claim types for any non-development environment.");
    }
    else
    {
        Log.Information("QueryAuth:Mode = {Mode} — query endpoint authorization active.", queryAuthMode);
    }

    // ── Ingest authentication ─────────────────────────────────────────────────
    // Concrete authenticators registered as singletons (stateless after construction).
    // IIngestAuthenticator resolves to the implementation matching IngestAuth:Mode.
    //
    // To add a new auth mode (e.g. Bearer/JWT):
    //   1. Implement IIngestAuthenticator in a new class.
    //   2. Add builder.Services.AddSingleton<YourAuthenticator>() here.
    //   3. Add a case to the switch below.
    //   4. Document the new mode in Docs/ingest-auth.md.
    builder.Services.AddSingleton<NullIngestAuthenticator>();
    builder.Services.AddSingleton<ServiceTokenAuthenticator>();

    var ingestAuthMode = cfg.GetSection(IngestAuthOptions.SectionName)["Mode"] ?? "None";

    builder.Services.AddSingleton<IIngestAuthenticator>(sp => ingestAuthMode switch
    {
        "ServiceToken" => sp.GetRequiredService<ServiceTokenAuthenticator>(),
        // Future modes registered here:
        // "Bearer"    => sp.GetRequiredService<JwtIngestAuthenticator>(),
        // "MtlsHeader"=> sp.GetRequiredService<MtlsIngestAuthenticator>(),
        _              => sp.GetRequiredService<NullIngestAuthenticator>(),
    });

    if (ingestAuthMode.Equals("None", StringComparison.OrdinalIgnoreCase))
    {
        Log.Warning(
            "IngestAuth:Mode = 'None' — ingest endpoints are unauthenticated. " +
            "Set Mode=ServiceToken and configure IngestAuth:ServiceTokens for any non-development environment.");
    }
    else
    {
        Log.Information("IngestAuth:Mode = {Mode} — ingest endpoint authentication active.", ingestAuthMode);
    }

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

    // IngestAuthMiddleware must run after CorrelationId (so TraceId is available for
    // error responses) and before Serilog request logging (so auth outcomes appear in logs).
    app.UseMiddleware<IngestAuthMiddleware>();

    // QueryAuthMiddleware resolves the caller context for /audit/* endpoints.
    // Fine-grained scope enforcement (403) is applied in the controller via IQueryAuthorizer.
    app.UseMiddleware<QueryAuthMiddleware>();

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
