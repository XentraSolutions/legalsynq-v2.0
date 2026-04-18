using System.Text;
using Documents.Api.Background;
using Documents.Api.Endpoints;
using Documents.Api.Middleware;
using Documents.Domain.Interfaces;
using Documents.Infrastructure;
using Documents.Infrastructure.Database;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Prometheus;
using Serilog;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

// ── Serilog structured logging ────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Service", "documents")
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

// ── Infrastructure (repositories, storage, scanner, queue, health checks) ─────
builder.Services.AddInfrastructure(builder.Configuration);

// ── Background scan worker ────────────────────────────────────────────────────
builder.Services.AddHostedService<DocumentScanWorker>();

// ── JWT authentication ────────────────────────────────────────────────────────
var jwtSection  = builder.Configuration.GetSection("Jwt");
var signingKey  = jwtSection["SigningKey"];
var jwksUri     = jwtSection["JwksUri"];
var issuer      = jwtSection["Issuer"];
var audience    = jwtSection["Audience"];

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;

        if (!string.IsNullOrWhiteSpace(jwksUri))
        {
            options.Authority              = issuer;
            options.MetadataAddress        = jwksUri;
            options.RequireHttpsMetadata   = !builder.Environment.IsDevelopment();
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer    = issuer is not null,
                ValidIssuer       = issuer,
                ValidateAudience  = audience is not null,
                ValidAudience     = audience,
                ValidateLifetime  = true,
                ClockSkew         = TimeSpan.Zero,
            };
        }
        else if (!string.IsNullOrWhiteSpace(signingKey))
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                ValidateIssuer           = issuer is not null,
                ValidIssuer              = issuer,
                ValidateAudience         = audience is not null,
                ValidAudience            = audience,
                ValidateLifetime         = true,
                ClockSkew                = TimeSpan.Zero,
            };
        }
        else
        {
            throw new InvalidOperationException("Either Jwt:SigningKey or Jwt:JwksUri must be configured.");
        }

        options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("JwtAuth");
                logger.LogError(context.Exception, "JWT authentication failed for {Path}", context.HttpContext.Request.Path);
                return Task.CompletedTask;
            },
            OnChallenge = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("JwtAuth");
                logger.LogWarning("JWT challenge issued for {Path}: {Error} - {ErrorDescription}",
                    context.Request.Path, context.Error, context.ErrorDescription);
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// ── Rate limiting (ASP.NET Core built-in) ─────────────────────────────────────
builder.Services.AddRateLimiter(opts =>
{
    opts.AddFixedWindowLimiter("general", o =>
    {
        o.Window           = TimeSpan.FromMinutes(1);
        o.PermitLimit      = 100;
        o.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
        o.QueueLimit       = 0;
    });

    opts.AddFixedWindowLimiter("upload", o =>
    {
        o.Window      = TimeSpan.FromMinutes(1);
        o.PermitLimit = 10;
        o.QueueLimit  = 0;
    });

    opts.AddFixedWindowLimiter("signed-url", o =>
    {
        o.Window      = TimeSpan.FromMinutes(1);
        o.PermitLimit = 30;
        o.QueueLimit  = 0;
    });

    opts.OnRejected = async (ctx, ct) =>
    {
        ctx.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        ctx.HttpContext.Response.Headers["Retry-After"] = "60";
        await ctx.HttpContext.Response.WriteAsJsonAsync(new
        {
            error   = "RATE_LIMIT_EXCEEDED",
            message = "Rate limit exceeded. Retry after 60 second(s).",
            retryAfter = 60,
        }, ct);
    };
});

// ── CORS ──────────────────────────────────────────────────────────────────────
var allowedOrigins = builder.Configuration["Cors:Origins"]?.Split(',')
    .Select(o => o.Trim()).ToArray()
    ?? Array.Empty<string>();

builder.Services.AddCors(opts => opts.AddDefaultPolicy(policy =>
{
    if (allowedOrigins.Length == 0 || allowedOrigins.Contains("*"))
        policy.AllowAnyOrigin();
    else
        policy.WithOrigins(allowedOrigins)
              .AllowCredentials();

    policy.WithHeaders("Authorization", "Content-Type", "X-Correlation-Id")
          .WithExposedHeaders("X-Correlation-Id");
}));

// ── Swagger ───────────────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title       = "Documents Service (.NET)",
        Version     = "v1",
        Description = "Multi-tenant document management service — .NET 8 parallel implementation",
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type         = SecuritySchemeType.Http,
        Scheme       = "bearer",
        BearerFormat = "JWT",
        Description  = "Enter your Bearer JWT token",
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
        }] = Array.Empty<string>(),
    });

    c.MapType<IFormFile>(() => new OpenApiSchema { Type = "string", Format = "binary" });
});

// ─────────────────────────────────────────────────────────────────────────────

var app = builder.Build();

// ── DB schema setup ──────────────────────────────────────────────────────────
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<DocsDbContext>();
    await db.Database.MigrateAsync();
    app.Logger.LogInformation("Documents database migrated successfully");
}
catch (Exception ex)
{
    // If the schema already exists (e.g. deployed over a live DB), log a
    // warning and continue — the service can still serve requests against
    // the existing schema rather than crashing on startup.
    app.Logger.LogWarning(ex, "Could not apply Documents database migrations on startup — schema may be out of sync.");
}

// ── Migration coverage self-test ─────────────────────────────────────────────
// Compares every EF-mapped column against the live schema and logs an ERROR
// if any are missing. Catches the class of bug behind Task #58: a migration
// committed without its [Migration] attribute (or otherwise un-applied)
// leaves the EF model and the live schema out of sync, which previously
// surfaced only as runtime "Unknown column" SQL errors.
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<DocsDbContext>();
    await BuildingBlocks.Diagnostics.MigrationCoverageProbe.RunAsync(db, app.Logger);
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "Migration coverage self-test could not run");
}

// ── Middleware pipeline ────────────────────────────────────────────────────────
app.UseCorrelationId();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseSerilogRequestLogging();

// ── Prometheus HTTP metrics (request count, duration, in-flight) ──────────────
app.UseHttpMetrics(options =>
{
    options.ReduceStatusCodeCardinality();
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Documents Service (.NET) v1");
        c.RoutePrefix = "docs";
    });
}

app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// ── Prometheus metrics endpoint ────────────────────────────────────────────────
app.MapMetrics("/metrics").AllowAnonymous();

// ── Health + business endpoints ───────────────────────────────────────────────
app.MapHealthEndpoints();
app.MapDocumentEndpoints();
app.MapAccessEndpoints();
app.MapPublicLogoEndpoints();

// ── Local file serving ────────────────────────────────────────────────────────
// Required whenever the storage provider is "local" (both dev and production on
// Replit) because GenerateSignedUrlAsync returns /internal/files?token=… and the
// Documents content endpoint issues a 302 redirect to that URL.
{
    app.MapGet("/internal/files", async (string token, string disposition, HttpContext ctx) =>
    {
        var local = ctx.RequestServices.GetRequiredService<Documents.Infrastructure.Storage.LocalStorageProvider>();
        var (key, expired) = local.ResolveToken(token);

        if (expired) return Results.Json(new { error = "TOKEN_EXPIRED" }, statusCode: 401);
        if (key is null) return Results.Json(new { error = "TOKEN_INVALID" }, statusCode: 401);

        var basePath = ctx.RequestServices.GetRequiredService<IConfiguration>()["Storage:Local:BasePath"] ?? "/tmp/docs-local";
        var filePath = Path.Combine(basePath, key.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(filePath)) return Results.NotFound();

        var mimeType = "application/octet-stream";
        return Results.File(filePath, mimeType,
            enableRangeProcessing: true,
            fileDownloadName: disposition == "download" ? Path.GetFileName(key) : null);
    })
    .AllowAnonymous()
    .ExcludeFromDescription();
}

app.Run();
