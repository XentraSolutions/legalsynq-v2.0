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
builder.Services.AddAuthorization(options =>
{
    static bool IsPlatformAdmin(System.Security.Claims.ClaimsPrincipal u)
        => u.IsInRole("PlatformAdmin");

    static bool IsTenantAdmin(System.Security.Claims.ClaimsPrincipal u)
        => u.IsInRole("TenantAdmin") || IsPlatformAdmin(u);

    // Broad read — covers any Xenia read access
    options.AddPolicy(XeniaPolicies.Read, policy =>
        policy.RequireAuthenticatedUser()
              .RequireAssertion(ctx =>
                  IsPlatformAdmin(ctx.User) ||
                  ctx.User.HasClaim("permissions", XeniaPermissions.Read) ||
                  ctx.User.HasClaim("permissions", XeniaPermissions.Admin) ||
                  ctx.User.HasClaim("permissions", XeniaPermissions.ModulesRead) ||
                  ctx.User.HasClaim("permissions", XeniaPermissions.AdaptersRead) ||
                  ctx.User.HasClaim("permissions", XeniaPermissions.ConfigurationRead)));

    // Broad admin
    options.AddPolicy(XeniaPolicies.Admin, policy =>
        policy.RequireAuthenticatedUser()
              .RequireAssertion(ctx =>
                  IsPlatformAdmin(ctx.User) ||
                  ctx.User.HasClaim("permissions", XeniaPermissions.Admin)));

    // Module read
    options.AddPolicy(XeniaPolicies.ModulesRead, policy =>
        policy.RequireAuthenticatedUser()
              .RequireAssertion(ctx =>
                  IsPlatformAdmin(ctx.User) ||
                  ctx.User.HasClaim("permissions", XeniaPermissions.ModulesRead) ||
                  ctx.User.HasClaim("permissions", XeniaPermissions.Admin)));

    // Module management
    options.AddPolicy(XeniaPolicies.ModulesManage, policy =>
        policy.RequireAuthenticatedUser()
              .RequireAssertion(ctx =>
                  IsPlatformAdmin(ctx.User) ||
                  ctx.User.HasClaim("permissions", XeniaPermissions.ModulesManage) ||
                  ctx.User.HasClaim("permissions", XeniaPermissions.Admin)));

    // Adapter read
    options.AddPolicy(XeniaPolicies.AdaptersRead, policy =>
        policy.RequireAuthenticatedUser()
              .RequireAssertion(ctx =>
                  IsPlatformAdmin(ctx.User) ||
                  ctx.User.HasClaim("permissions", XeniaPermissions.AdaptersRead) ||
                  ctx.User.HasClaim("permissions", XeniaPermissions.Admin)));

    // Configuration read
    options.AddPolicy(XeniaPolicies.ConfigurationRead, policy =>
        policy.RequireAuthenticatedUser()
              .RequireAssertion(ctx =>
                  IsPlatformAdmin(ctx.User) ||
                  ctx.User.HasClaim("permissions", XeniaPermissions.ConfigurationRead) ||
                  ctx.User.HasClaim("permissions", XeniaPermissions.Admin)));

    // Configuration management
    options.AddPolicy(XeniaPolicies.ConfigurationManage, policy =>
        policy.RequireAuthenticatedUser()
              .RequireAssertion(ctx =>
                  IsPlatformAdmin(ctx.User) ||
                  ctx.User.HasClaim("permissions", XeniaPermissions.ConfigurationManage) ||
                  ctx.User.HasClaim("permissions", XeniaPermissions.Admin)));

    // Email read — tenant admin or platform admin with email.read
    options.AddPolicy(XeniaPolicies.EmailRead, policy =>
        policy.RequireAuthenticatedUser()
              .RequireAssertion(ctx =>
                  IsPlatformAdmin(ctx.User) ||
                  IsTenantAdmin(ctx.User) ||
                  ctx.User.HasClaim("permissions", XeniaPermissions.EmailRead) ||
                  ctx.User.HasClaim("permissions", XeniaPermissions.EmailManage) ||
                  ctx.User.HasClaim("permissions", XeniaPermissions.Admin)));

    // Email manage — create/update/delete/enable/disable sources
    options.AddPolicy(XeniaPolicies.EmailManage, policy =>
        policy.RequireAuthenticatedUser()
              .RequireAssertion(ctx =>
                  IsPlatformAdmin(ctx.User) ||
                  IsTenantAdmin(ctx.User) ||
                  ctx.User.HasClaim("permissions", XeniaPermissions.EmailManage) ||
                  ctx.User.HasClaim("permissions", XeniaPermissions.Admin)));

    // Email validate — trigger connection tests
    options.AddPolicy(XeniaPolicies.EmailValidate, policy =>
        policy.RequireAuthenticatedUser()
              .RequireAssertion(ctx =>
                  IsPlatformAdmin(ctx.User) ||
                  IsTenantAdmin(ctx.User) ||
                  ctx.User.HasClaim("permissions", XeniaPermissions.EmailValidate) ||
                  ctx.User.HasClaim("permissions", XeniaPermissions.EmailManage) ||
                  ctx.User.HasClaim("permissions", XeniaPermissions.Admin)));
});

// ── Application and infrastructure services ───────────────────────────────────
builder.Services.AddXeniaApplication();
builder.Services.AddXeniaInfrastructure(builder.Configuration);

builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// ── Middleware pipeline ───────────────────────────────────────────────────────
// Correct order:
//   1. Exception handling — outermost so all errors produce well-formed responses.
//   2. Correlation       — assigns/propagates X-Correlation-Id for all logs.
//   3. Authentication    — validates JWT, populates HttpContext.User.
//   4. Tenant context    — reads tenant_id from verified JWT claims (never from
//                          caller-supplied headers/query/body).
//   5. Authorization     — policies may require both User AND tenant context.
app.UseMiddleware<XeniaExceptionMiddleware>();
app.UseMiddleware<XeniaCorrelationMiddleware>();

app.UseAuthentication();

app.UseMiddleware<XeniaTenantContextMiddleware>();

app.UseAuthorization();

// ── Endpoints ─────────────────────────────────────────────────────────────────

// Anonymous — liveness, readiness, service information
app.MapXeniaHealthEndpoints();
app.MapXeniaInfoEndpoints();

// Authenticated — module and adapter registry
app.MapXeniaModuleEndpoints();
app.MapXeniaAdapterEndpoints();
app.MapXeniaConfigurationEndpoints();

// Email module endpoints
app.MapXeniaEmailModuleEndpoints();
app.MapXeniaEmailSourceEndpoints();
app.MapXeniaEmailProviderEndpoints();
app.MapXeniaEmailSettingsEndpoints();

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
    public const string EmailRead         = "XeniaEmailRead";
    public const string EmailManage       = "XeniaEmailManage";
    public const string EmailValidate     = "XeniaEmailValidate";
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
    public const string EmailRead = "xenia.email.read";
    public const string EmailManage = "xenia.email.manage";
    public const string EmailValidate = "xenia.email.validate";
}

public static class XeniaBuildInfo
{
    public static readonly string ServiceVersion =
        typeof(XeniaBuildInfo).Assembly.GetName().Version?.ToString() ?? "1.0.0";
    public static readonly DateTime StartedAt = DateTime.UtcNow;
}
