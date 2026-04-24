using FluentValidation;
using Support.Api.Audit;
using Support.Api.Auth;
using Support.Api.Configuration;
using Support.Api.Data;
using Support.Api.Endpoints;
using Support.Api.Files;
using Support.Api.Notifications;
using Support.Api.Services;
using Support.Api.Tenancy;
using Support.Api.Validators;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Metrics;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// --- Serilog ---
builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

// --- DbContext (MySQL via Pomelo) ---
var conn = builder.Configuration.GetConnectionString("Support")
    ?? "Server=localhost;Port=3306;Database=support;User=root;Password=;";

if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddDbContext<SupportDbContext>(opt =>
        opt.UseMySql(conn, new MySqlServerVersion(new Version(8, 0, 26))));
}

// --- Tenant context (scoped) ---
builder.Services.AddScoped<ITenantContext, TenantContext>();

// --- Actor accessor (audit) ---
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IActorAccessor, HttpContextActorAccessor>();

// --- Authentication & Authorization ---
builder.Services.AddSupportAuth(builder.Configuration, builder.Environment);

// --- Domain services ---
builder.Services.AddScoped<ITicketNumberGenerator, TicketNumberGenerator>();
builder.Services.AddScoped<IEventLogger, EventLogger>();
builder.Services.AddScoped<ITicketService, TicketService>();
builder.Services.AddScoped<ICommentService, CommentService>();
builder.Services.AddScoped<ITicketAttachmentService, TicketAttachmentService>();
builder.Services.AddScoped<ITicketProductReferenceService, TicketProductReferenceService>();
builder.Services.AddScoped<IQueueService, QueueService>();

// --- Notifications dispatch ---
builder.Services.Configure<NotificationOptions>(
    builder.Configuration.GetSection(NotificationOptions.SectionName));
{
    var section = builder.Configuration.GetSection(NotificationOptions.SectionName);
    var modeStr = section["Mode"];
    var mode = NotificationDispatchMode.NoOp;
    if (!string.IsNullOrWhiteSpace(modeStr) &&
        Enum.TryParse<NotificationDispatchMode>(modeStr, ignoreCase: true, out var parsed))
    {
        mode = parsed;
    }

    if (mode == NotificationDispatchMode.Http)
    {
        builder.Services.AddHttpClient<INotificationPublisher, HttpNotificationPublisher>(
            HttpNotificationPublisher.HttpClientName);
    }
    else
    {
        builder.Services.AddSingleton<INotificationPublisher, NoOpNotificationPublisher>();
    }
}

// --- Audit dispatch ---
builder.Services.Configure<AuditOptions>(
    builder.Configuration.GetSection(AuditOptions.SectionName));
{
    var section = builder.Configuration.GetSection(AuditOptions.SectionName);
    var modeStr = section["Mode"];
    var mode = AuditDispatchMode.NoOp;
    if (!string.IsNullOrWhiteSpace(modeStr) &&
        Enum.TryParse<AuditDispatchMode>(modeStr, ignoreCase: true, out var parsed))
    {
        mode = parsed;
    }

    if (mode == AuditDispatchMode.Http)
    {
        builder.Services.AddHttpClient<IAuditPublisher, HttpAuditPublisher>(
            HttpAuditPublisher.HttpClientName);
    }
    else
    {
        builder.Services.AddSingleton<IAuditPublisher, NoOpAuditPublisher>();
    }
}

// --- File storage (uploads) ---
builder.Services.Configure<FileStorageOptions>(
    builder.Configuration.GetSection(FileStorageOptions.SectionName));
{
    var section = builder.Configuration.GetSection(FileStorageOptions.SectionName);
    var modeStr = section["Mode"];
    var mode = FileStorageMode.NoOp;
    if (!string.IsNullOrWhiteSpace(modeStr) &&
        Enum.TryParse<FileStorageMode>(modeStr, ignoreCase: true, out var parsed))
    {
        mode = parsed;
    }

    switch (mode)
    {
        case FileStorageMode.Local:
            builder.Services.AddSingleton<ISupportFileStorageProvider, LocalSupportFileStorageProvider>();
            break;
        case FileStorageMode.DocumentsService:
            builder.Services.AddHttpClient<ISupportFileStorageProvider, DocumentsServiceFileStorageProvider>(
                DocumentsServiceFileStorageProvider.HttpClientName,
                (sp, http) =>
                {
                    var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<FileStorageOptions>>().Value;
                    http.Timeout = TimeSpan.FromSeconds(Math.Max(1, opts.DocumentsService.TimeoutSeconds));
                });
            break;
        case FileStorageMode.NoOp:
        default:
            builder.Services.AddSingleton<ISupportFileStorageProvider, NoOpSupportFileStorageProvider>();
            break;
    }
}

// --- FluentValidation ---
builder.Services.AddValidatorsFromAssemblyContaining<CreateTicketRequestValidator>();

// --- Swagger / OpenAPI ---
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Support API", Version = "v1" });
});

// --- Health checks ---
builder.Services.AddHealthChecks();

// --- Observability ---
builder.Services.AddOpenTelemetry()
    .WithMetrics(m => m
        .AddAspNetCoreInstrumentation()
        .AddPrometheusExporter());

builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseCors();

// Swagger is exposed only in Development. Production gateways should
// consume the OpenAPI document via internal tooling, not a public UI.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Support API v1");
        c.RoutePrefix = "support/api/swagger";
    });
}

app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();

app.MapHealthChecks("/support/api/health").AllowAnonymous();
app.MapPrometheusScrapingEndpoint("/support/api/metrics");
app.MapTicketEndpoints();
app.MapCommentEndpoints();
app.MapAttachmentEndpoints();
app.MapProductRefEndpoints();
app.MapQueueEndpoints();

app.Run();

public partial class Program { }
