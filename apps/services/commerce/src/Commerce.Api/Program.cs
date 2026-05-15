using Commerce.Api.Configuration;
using Commerce.Api.Middleware;
using Commerce.Application;
using Commerce.Infrastructure;
using Commerce.Infrastructure.Integration.HostAdapters;
using Commerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ---- Logging (Serilog) ----
builder.Host.UseSerilog((ctx, sp, cfg) =>
{
    cfg.ReadFrom.Configuration(ctx.Configuration)
       .ReadFrom.Services(sp)
       .Enrich.FromLogContext()
       .Enrich.WithProperty("service", "Commerce")
       .Enrich.WithProperty("environment", ctx.HostingEnvironment.EnvironmentName);
});

// ---- Options ----
builder.Services.Configure<CommerceOptions>(builder.Configuration.GetSection(CommerceOptions.SectionName));

// ---- MVC / Controllers ----
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// ---- Swagger / OpenAPI ----
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Commerce API",
        Version = "v1",
        Description = "Independent Commerce platform service. Foundation block (COM-B01)."
    });

    o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT Authorization header. Auth is a placeholder in COM-B01."
    });
});

// ---- Application + Infrastructure ----
builder.Services.AddCommerceApplication();
builder.Services.AddCommerceInfrastructure(builder.Configuration);

// ---- LS-INT-01: LegalSynq identity integration (additive, gated) ----
// Safe default: Enabled=false → standalone mode, zero behavior change.
// When Enabled=true, JWT bearer authentication is registered and the
// LegalSynqJwtHostIdentityContextAccessor + LegalSynqJwtHostTenantResolver
// replace the local no-op stubs.
var legalSynqEnabled = builder.Configuration.GetValue<bool>("LegalSynq:Identity:Enabled");
if (legalSynqEnabled)
{
    builder.Services.AddLegalSynqCommerceIntegration(builder.Configuration);
}

// ---- Demo data seeder (preview/in-memory only) ----
// Seeds a representative dataset across catalog, billing, subscriptions,
// invoices, payments, provider events and account standing when the
// SEED_DEMO_DATA env var is set to "true". Idempotent.
if (string.Equals(
        builder.Configuration["SEED_DEMO_DATA"],
        "true",
        StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddHostedService<Commerce.Api.Demo.DemoDataSeeder>();
}

// ---- Health Checks ----
var connectionString = builder.Configuration["Database:ConnectionString"];
var healthBuilder = builder.Services.AddHealthChecks();
if (!string.IsNullOrWhiteSpace(connectionString))
{
    healthBuilder.AddDbContextCheck<CommerceDbContext>(
        name: "database",
        tags: new[] { "ready" });
}

// ---- OpenTelemetry ----
var otlpEnabled = builder.Configuration.GetValue<bool>("Observability:Otlp:Enabled");
var otlpEndpoint = builder.Configuration["Observability:Otlp:Endpoint"];
var otelServiceName = builder.Configuration["Observability:ServiceName"] ?? "Commerce";

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(serviceName: otelServiceName))
    .WithTracing(t =>
    {
        t.AddAspNetCoreInstrumentation()
         .AddHttpClientInstrumentation()
         .AddEntityFrameworkCoreInstrumentation();
        if (otlpEnabled && !string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            t.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
        }
    })
    .WithMetrics(m =>
    {
        m.AddAspNetCoreInstrumentation()
         .AddHttpClientInstrumentation()
         .AddRuntimeInstrumentation()
         .AddMeter(Commerce.Infrastructure.Integration.TenantBilling
             .TenantBillingPublisherMetrics.MeterName);
        if (otlpEnabled && !string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            m.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
        }
    });

var app = builder.Build();

// ---- Middleware pipeline ----
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ProblemDetailsExceptionMiddleware>();
app.UseSerilogRequestLogging(opts =>
{
    opts.MessageTemplate =
        "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
});

// LS-INT-01: JWT auth middleware — only registered when LegalSynq:Identity:Enabled=true.
// Health + ready endpoints are mapped before MapControllers, so they are never
// gated by [Authorize]. AddLegalSynqCommerceIntegration registers both
// AddAuthentication and AddAuthorization; we call the middleware here
// unconditionally after those registrations (no-op when not registered).
if (legalSynqEnabled)
{
    app.UseAuthentication();
    app.UseAuthorization();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(o => o.SwaggerEndpoint("/swagger/v1/swagger.json", "Commerce API v1"));
}

// ---- Endpoints ----
app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
   .WithName("Liveness")
   .WithTags("Health");

app.MapGet("/ready", async (CommerceDbContext db, IConfiguration config) =>
{
    var conn = config["Database:ConnectionString"];
    if (string.IsNullOrWhiteSpace(conn))
    {
        return Results.Json(
            new { status = "degraded", database = "not-configured" },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    try
    {
        var canConnect = await db.Database.CanConnectAsync();
        if (!canConnect)
        {
            return Results.Json(
                new { status = "degraded", database = "unreachable" },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        return Results.Ok(new { status = "ok", database = "ok" });
    }
    catch (Exception ex)
    {
        return Results.Json(
            new { status = "degraded", database = "error", message = ex.GetType().Name },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
})
.WithName("Readiness")
.WithTags("Health");

app.MapControllers();

try
{
    Log.Information("Commerce service starting in {Environment}", app.Environment.EnvironmentName);
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Commerce service terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// Test host hook
public partial class Program { }
