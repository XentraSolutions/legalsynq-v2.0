using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.Json;
using Monitoring.Api.Authentication;
using Monitoring.Api.Endpoints;
using Monitoring.Api.Middleware;
using Monitoring.Application;
using Monitoring.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddMonitoringAuthentication(builder.Configuration);

// Serialize enums as their string names so the API contract is human-readable
// and stable across enum reordering. Matches how the values are persisted.
builder.Services.Configure<JsonOptions>(o =>
{
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

var app = builder.Build();

app.UseMiddleware<DomainExceptionMiddleware>();

app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
        .CreateLogger("Monitoring.Api.Request");
    logger.LogInformation("{Method} {Path}", context.Request.Method, context.Request.Path);
    await next();
});

app.UseAuthentication();
app.UseAuthorization();

// Public liveness probe — must remain accessible without authentication.
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "monitoring" }))
    .AllowAnonymous();

// Protected validation endpoint — exists solely to verify the auth pipeline.
// No business logic. Returns the authenticated subject's identity claims only.
app.MapGet("/secure/ping", (HttpContext ctx) =>
{
    var sub = ctx.User.FindFirst("sub")?.Value
              ?? ctx.User.Identity?.Name
              ?? "unknown";
    return Results.Ok(new { status = "ok", sub });
}).RequireAuthorization();

app.MapMonitoredEntityEndpoints();
app.MapMonitoringReadEndpoints();

var startupLogger = app.Services.GetRequiredService<ILoggerFactory>()
    .CreateLogger("Monitoring.Api.Startup");
startupLogger.LogInformation(
    "Monitoring service starting in {Environment} environment",
    app.Environment.EnvironmentName);
startupLogger.LogInformation(
    "Authentication: JWT Bearer (RS256) enabled. /health is public; other endpoints require authentication.");

app.Run();
