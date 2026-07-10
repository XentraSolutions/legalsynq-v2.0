using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.IdentityModel.Tokens;
using Xenia.Api.Endpoints;
using Xenia.Api.Middleware;
using Xenia.Application;
using Xenia.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// ── Configuration ─────────────────────────────────────────────────────────────
builder.Configuration
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

// ── Logging ───────────────────────────────────────────────────────────────────
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));

// ── JSON options — enums as strings ──────────────────────────────────────────
builder.Services.Configure<JsonOptions>(o =>
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// ── Authentication — JWT Bearer ───────────────────────────────────────────────
var jwtSection = builder.Configuration.GetSection("Jwt");
var signingKey = jwtSection["SigningKey"]
    ?? throw new InvalidOperationException(
        "Jwt:SigningKey is not configured. Set it via the Jwt__SigningKey environment variable.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSection["Issuer"] ?? "legalsynq-identity",
            ValidateAudience = true,
            ValidAudience = jwtSection["Audience"] ?? "legalsynq-platform",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });

// ── Authorization — Xenia permission policies ─────────────────────────────────
// Two broad policies for backwards compatibility, plus granular per-resource policies.
// PlatformAdmin role satisfies all policies (super-user).
builder.Services.AddAuthorization(options =>
{
    static bool IsPlatformAdmin(System.Security.Claims.ClaimsPrincipal u)
        => u.IsInRole("PlatformAdmin");

    // Broad read — covers any Xenia read access; kept for /secure/ping and general gating
    options.AddPolicy(XeniaPolicies.Read, policy =>
        policy.RequireAuthenticatedUser()
              .RequireAssertion(ctx =>
                  IsPlatformAdmin(ctx.User) ||
                  ctx.User.HasClaim("permissions", XeniaPermissions.Read) ||
                  ctx.User.HasClaim("permissions", XeniaPermissions.Admin) ||
                  ctx.User.HasClaim("permissions", XeniaPermissions.ModulesRead) ||
                  ctx.User.HasClaim("permissions", XeniaPermissions.AdaptersRead) ||
                  ctx.User.HasClaim("permissions", XeniaPermissions.ConfigurationRead)));

    // Broad admin — module mutation, configuration writes
    options.AddPolicy(XeniaPolicies.Admin, policy =>
        policy.RequireAuthenticatedUser()
              .RequireAssertion(ctx =>
                  IsPlatformAdmin(ctx.User) ||
                  ctx.User.HasClaim("permissions", XeniaPermissions.Admin)));

    // Granular — module read
    options.AddPolicy(XeniaPolicies.ModulesRead, policy =>
        policy.RequireAuthenticatedUser()
              .RequireAssertion(ctx =>
                  IsPlatformAdmin(ctx.User) ||
                  ctx.User.HasClaim("permissions", XeniaPermissions.ModulesRead) ||
                  ctx.User.HasClaim("permissions", XeniaPermissions.Admin)));

    // Granular — module management (enable/disable)
    options.AddPolicy(XeniaPolicies.ModulesManage, policy =>
        policy.RequireAuthenticatedUser()
              .RequireAssertion(ctx =>
                  IsPlatformAdmin(ctx.User) ||
                  ctx.User.HasClaim("permissions", XeniaPermissions.ModulesManage) ||
                  ctx.User.HasClaim("permissions", XeniaPermissions.Admin)));

    // Granular — adapter read
    options.AddPolicy(XeniaPolicies.AdaptersRead, policy =>
        policy.RequireAuthenticatedUser()
              .RequireAssertion(ctx =>
                  IsPlatformAdmin(ctx.User) ||
                  ctx.User.HasClaim("permissions", XeniaPermissions.AdaptersRead) ||
                  ctx.User.HasClaim("permissions", XeniaPermissions.Admin)));

    // Granular — configuration read
    options.AddPolicy(XeniaPolicies.ConfigurationRead, policy =>
        policy.RequireAuthenticatedUser()
              .RequireAssertion(ctx =>
                  IsPlatformAdmin(ctx.User) ||
                  ctx.User.HasClaim("permissions", XeniaPermissions.ConfigurationRead) ||
                  ctx.User.HasClaim("permissions", XeniaPermissions.Admin)));

    // Granular — configuration management (write)
    options.AddPolicy(XeniaPolicies.ConfigurationManage, policy =>
        policy.RequireAuthenticatedUser()
              .RequireAssertion(ctx =>
                  IsPlatformAdmin(ctx.User) ||
                  ctx.User.HasClaim("permissions", XeniaPermissions.ConfigurationManage) ||
                  ctx.User.HasClaim("permissions", XeniaPermissions.Admin)));
});

// ── Application and infrastructure services ───────────────────────────────────
builder.Services.AddXeniaApplication();
builder.Services.AddXeniaInfrastructure(builder.Configuration);

builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// ── Middleware pipeline ───────────────────────────────────────────────────────
app.UseMiddleware<XeniaCorrelationMiddleware>();
app.UseMiddleware<XeniaExceptionMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

// ── Tenant context resolution ─────────────────────────────────────────────────
// Must run after authentication so JWT claims are available.
// Reads the tenant_id claim from the verified JWT — never from caller-supplied inputs.
app.UseMiddleware<XeniaTenantContextMiddleware>();

// ── Endpoints ─────────────────────────────────────────────────────────────────

// Anonymous — liveness, readiness, service information
app.MapXeniaHealthEndpoints();
app.MapXeniaInfoEndpoints();

// Authenticated — module and adapter registry
app.MapXeniaModuleEndpoints();
app.MapXeniaAdapterEndpoints();
app.MapXeniaConfigurationEndpoints();

// Auth smoke-test endpoint
app.MapGet("/secure/ping", (HttpContext ctx) =>
{
    var sub = ctx.User.FindFirst("sub")?.Value ?? "unknown";
    return Results.Ok(new { status = "ok", sub });
}).RequireAuthorization(XeniaPolicies.Read);

// ── Startup log ───────────────────────────────────────────────────────────────
var startupLogger = app.Services.GetRequiredService<ILoggerFactory>()
    .CreateLogger("Xenia.Api.Startup");

startupLogger.LogInformation(
    "Xenia service starting. Environment={Environment} Version={Version}",
    app.Environment.EnvironmentName,
    XeniaBuildInfo.ServiceVersion);

app.Run();

// ── Shared constants ──────────────────────────────────────────────────────────
public static class XeniaPolicies
{
    public const string Read              = "XeniaRead";
    public const string Admin             = "XeniaAdmin";
    public const string ModulesRead       = "XeniaModulesRead";
    public const string ModulesManage     = "XeniaModulesManage";
    public const string AdaptersRead      = "XeniaAdaptersRead";
    public const string ConfigurationRead = "XeniaConfigurationRead";
    public const string ConfigurationManage = "XeniaConfigurationManage";
}

public static class XeniaPermissions
{
    public const string Read = "xenia.read";
    public const string Admin = "xenia.admin";
    public const string ModulesRead = "xenia.modules.read";
    public const string ModulesManage = "xenia.modules.manage";
    public const string AdaptersRead = "xenia.adapters.read";
    public const string ConfigurationRead = "xenia.configuration.read";
    public const string ConfigurationManage = "xenia.configuration.manage";
}

public static class XeniaBuildInfo
{
    public static readonly string ServiceVersion =
        typeof(XeniaBuildInfo).Assembly.GetName().Version?.ToString() ?? "1.0.0";
    public static readonly DateTime StartedAt = DateTime.UtcNow;
}
