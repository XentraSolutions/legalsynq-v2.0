using System.Text;
using BuildingBlocks;
using BuildingBlocks.Authentication.ServiceTokens;
using BuildingBlocks.Authorization;
using Contracts;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Threading.RateLimiting;
using Tenant.Application.Configuration;
using Tenant.Api.Endpoints;
using Tenant.Api.Middleware;
using Tenant.Infrastructure;
using Tenant.Infrastructure.Data;

const string ServiceName = "tenant";
const string Version = "v1";

var builder = WebApplication.CreateBuilder(args);

builder.Logging
    .ClearProviders()
    .AddConsole();

var jwtSection   = builder.Configuration.GetSection("Jwt");
var signingKey   = jwtSection["SigningKey"]
    ?? throw new InvalidOperationException("Jwt:SigningKey is not configured.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = jwtSection["Issuer"],
            ValidAudience            = jwtSection["Audience"],
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)) { KeyId = ServiceTokenAuthenticationDefaults.UserTokenKeyId },
            RoleClaimType            = "role",
            ClockSkew                = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Policies.AuthenticatedUser, policy =>
        policy.RequireAuthenticatedUser());

    options.AddPolicy(Policies.AdminOnly, policy =>
        policy.RequireRole(Roles.PlatformAdmin));

    options.AddPolicy(Policies.PlatformOrTenantAdmin, policy =>
        policy.RequireRole(Roles.PlatformAdmin, Roles.TenantAdmin));
});

// ── Feature flags ─────────────────────────────────────────────────────────────
builder.Services.Configure<TenantFeatures>(
    builder.Configuration.GetSection(TenantFeatures.SectionName));

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            error = "Too many registration attempts. Please wait a few minutes and try again."
        }, cancellationToken);
    };
    options.AddPolicy("tenant-registration", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(10),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

// ── BLK-OPS-01: Production fail-fast (supersedes BLK-SEC-01 inline checks) ────
if (!builder.Environment.IsDevelopment())
{
    var v = new RuntimeConfigValidator(builder.Configuration, "tenant");
    v
        // JWT signing key must be real — not a placeholder
        .RequireNotPlaceholder("Jwt:SigningKey")
        // Provisioning secret gates all internal provisioning endpoints
        .RequireNonEmpty("TenantService:ProvisioningSecret")
        // Database connection string
        .RequireConnectionString("ConnectionStrings:TenantDb");
}

var app = builder.Build();

var env = app.Environment.EnvironmentName;
app.Logger.LogInformation("Starting {Service} {Version} in {Environment}", ServiceName, Version, env);

// Auto-migrate — apply pending EF Core migrations on startup (idempotent).
// A failure here is fatal: starting with an out-of-sync schema causes every
// EF query to throw, so we prefer a clean crash over a silently broken service.
// Transient errors (e.g. "Too many connections" at startup when all services
// migrate simultaneously) are retried with exponential back-off before giving up.
try
{
    var retryDelaysSeconds = new[] { 5, 10, 20, 30 };
    for (var attempt = 0; ; attempt++)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TenantDbContext>();
            await db.Database.MigrateAsync();
            app.Logger.LogInformation("Tenant database migrations applied successfully.");
            break;
        }
        catch (Exception ex) when (attempt < retryDelaysSeconds.Length && IsTransientDbError(ex))
        {
            var delaySec = retryDelaysSeconds[attempt];
            app.Logger.LogWarning(ex,
                "Tenant migration attempt {Attempt} failed (transient); retrying in {Delay}s.",
                attempt + 1, delaySec);
            await Task.Delay(TimeSpan.FromSeconds(delaySec));
        }
    }
}
catch (Exception ex)
{
    app.Logger.LogCritical(ex, "Tenant database migration failed — service cannot start with an out-of-sync schema.");
    throw;
}

static bool IsTransientDbError(Exception ex)
{
    var msg = (ex.Message + " " + (ex.InnerException?.Message ?? "")).ToLowerInvariant();
    return msg.Contains("too many connections")
        || msg.Contains("transient")
        || msg.Contains("connection pool")
        || msg.Contains("unable to connect");
}

// Migration coverage self-test — detects EF model / live schema drift at startup.
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<TenantDbContext>();
    await BuildingBlocks.Diagnostics.MigrationCoverageProbe.RunAsync(db, app.Logger);
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "Migration coverage self-test could not run.");
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () =>
    Results.Ok(new HealthResponse("ok", ServiceName)))
    .AllowAnonymous();

app.MapGet("/info", () =>
    Results.Ok(new InfoResponse(ServiceName, env, Version)))
    .AllowAnonymous();

app.MapTenantEndpoints();
app.MapProvisionEndpoints();
app.MapBrandingEndpoints();
app.MapDomainEndpoints();
app.MapResolutionEndpoints();
app.MapEntitlementEndpoints();
app.MapCapabilityEndpoints();
app.MapSettingEndpoints();
app.MapCareConnectAccessCodeEndpoints();
app.MapMigrationEndpoints();
app.MapReadSourceEndpoints();
app.MapSyncEndpoints();
app.MapRuntimeMetricsEndpoints();
app.MapLogoAdminEndpoints();
app.MapTenantAdminEndpoints();
app.MapActivationEndpoints();     // BLK-TS-02
app.MapTenantRegistrationEndpoints();

app.Run();
