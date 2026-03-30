using FluentValidation;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Events;
using PlatformAuditEventService.Configuration;
using PlatformAuditEventService.DTOs;
using PlatformAuditEventService.Middleware;
using PlatformAuditEventService.Repositories;
using PlatformAuditEventService.Services;
using PlatformAuditEventService.Validators;

// ── Bootstrap logger (captures startup errors before full Serilog init) ──────
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting Platform Audit/Event Service");

    var builder = WebApplication.CreateBuilder(args);

    // ── Serilog ──────────────────────────────────────────────────────────────
    builder.Host.UseSerilog((ctx, services, cfg) =>
        cfg.ReadFrom.Configuration(ctx.Configuration)
           .ReadFrom.Services(services)
           .Enrich.FromLogContext());

    // ── Configuration ─────────────────────────────────────────────────────────
    builder.Services.Configure<AuditServiceOptions>(
        builder.Configuration.GetSection(AuditServiceOptions.SectionName));

    var auditOptions = builder.Configuration
        .GetSection(AuditServiceOptions.SectionName)
        .Get<AuditServiceOptions>() ?? new AuditServiceOptions();

    // ── Controllers + API behavior ────────────────────────────────────────────
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();

    // ── Swagger / OpenAPI ─────────────────────────────────────────────────────
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo
        {
            Title       = "Platform Audit/Event Service",
            Version     = "v1",
            Description = "Standalone, independently deployable service that ingests business, " +
                          "security, access, administrative, and system activity from distributed systems, " +
                          "normalizes it into a canonical event model, and persists immutable, tamper-evident audit records.",
            Contact = new OpenApiContact
            {
                Name  = "LegalSynq Platform Team",
                Email = "platform@legalsynq.com",
            },
        });
        c.DescribeAllParametersInCamelCase();
    });

    // ── Validation ────────────────────────────────────────────────────────────
    builder.Services.AddScoped<IValidator<IngestAuditEventRequest>, IngestAuditEventRequestValidator>();

    // ── Persistence ───────────────────────────────────────────────────────────
    builder.Services.AddSingleton<IAuditEventRepository, InMemoryAuditEventRepository>();

    // ── Domain services ───────────────────────────────────────────────────────
    builder.Services.AddScoped<IAuditEventService, AuditEventService>();

    // ── CORS (development permissive; lock down in production) ────────────────
    builder.Services.AddCors(opts =>
        opts.AddDefaultPolicy(policy =>
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

    // ── Health checks ─────────────────────────────────────────────────────────
    builder.Services.AddHealthChecks();

    // ── Build ─────────────────────────────────────────────────────────────────
    var app = builder.Build();

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
                : elapsed > 1000
                    ? LogEventLevel.Warning
                    : LogEventLevel.Information;
    });

    app.UseCors();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "Platform Audit/Event Service v1");
            c.RoutePrefix = "swagger";
            c.DisplayRequestDuration();
        });
    }

    app.UseAuthorization();
    app.MapControllers();
    app.MapHealthChecks("/health");

    // ── Startup summary ───────────────────────────────────────────────────────
    var env  = app.Environment.EnvironmentName;
    var port = builder.Configuration["ASPNETCORE_URLS"]
            ?? builder.Configuration["urls"]
            ?? "http://0.0.0.0:5007";

    Log.Information("Platform Audit/Event Service ready | Env={Env} | Persistence={Provider} | Urls={Urls}",
        env, auditOptions.PersistenceProvider, port);

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
