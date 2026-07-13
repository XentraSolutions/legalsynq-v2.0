using System.Text;
using System.Text.Json.Serialization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Xenia.Api.Endpoints;
using Xenia.Api.Middleware;
using Xenia.Application;
using Xenia.Infrastructure;

var environmentName =
    Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
    ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
    ?? Environments.Production;
var contentRoot = ResolveContentRoot();
var bootstrapConfiguration = BuildConfiguration(contentRoot, environmentName);
var urls = bootstrapConfiguration["ASPNETCORE_URLS"]
    ?? bootstrapConfiguration["Urls"]
    ?? "http://0.0.0.0:5035";

using var host = new HostBuilder()
    .UseEnvironment(environmentName)
    .ConfigureAppConfiguration((_, config) =>
    {
        config.Sources.Clear();
        AddConfigurationSources(config, contentRoot, environmentName);
    })
    .ConfigureLogging((context, logging) =>
    {
        logging.ClearProviders();
        logging.AddConsole();
        logging.AddConfiguration(context.Configuration.GetSection("Logging"));
    })
    .ConfigureWebHost(webBuilder =>
    {
        webBuilder.UseEnvironment(environmentName);
        webBuilder.UseContentRoot(contentRoot);
        webBuilder.UseKestrel();
        webBuilder.UseUrls(urls);
        webBuilder.ConfigureServices((context, services) =>
            ConfigureXeniaServices(services, context.Configuration));
        webBuilder.Configure((context, app) =>
            ConfigureXeniaPipeline(app, context.HostingEnvironment));
    })
    .Build();

await host.RunAsync();

static IConfigurationRoot BuildConfiguration(string contentRoot, string environmentName)
{
    var builder = new ConfigurationBuilder();
    AddConfigurationSources(builder, contentRoot, environmentName);
    return builder.Build();
}

static void AddConfigurationSources(
    IConfigurationBuilder builder,
    string contentRoot,
    string environmentName)
{
    builder
        .SetBasePath(contentRoot)
        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
        .AddJsonFile($"appsettings.{environmentName}.json", optional: true, reloadOnChange: true)
        .AddEnvironmentVariables();
}

static string ResolveContentRoot()
{
    var baseDirectory = Path.GetFullPath(AppContext.BaseDirectory);
    var current = new DirectoryInfo(baseDirectory);

    while (current is not null)
    {
        if (File.Exists(Path.Combine(current.FullName, "Xenia.Api.csproj")) &&
            File.Exists(Path.Combine(current.FullName, "appsettings.json")))
        {
            return current.FullName;
        }

        current = current.Parent;
    }

    return baseDirectory;
}

static void ConfigureXeniaServices(IServiceCollection services, IConfiguration configuration)
{
    services.AddRouting();
    services.Configure<JsonOptions>(o =>
        o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

    var jwtSection = configuration.GetSection("Jwt");
    var signingKey = jwtSection["SigningKey"]
        ?? throw new InvalidOperationException(
            "Jwt:SigningKey is not configured. Set it via the Jwt__SigningKey environment variable.");

    services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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
                NameClaimType = "sub",
                RoleClaimType = "role",
            };
        });

    AddXeniaAuthorizationPolicies(services);

    services.AddXeniaApplication();
    services.AddXeniaInfrastructure(configuration);
    services.AddHttpContextAccessor();
}

static void AddXeniaAuthorizationPolicies(IServiceCollection services)
{
    services.AddAuthorization(options =>
    {
        static bool IsPlatformAdmin(System.Security.Claims.ClaimsPrincipal u)
            => u.IsInRole("PlatformAdmin") || u.HasClaim("role", "PlatformAdmin") || u.HasClaim(ClaimTypes.Role, "PlatformAdmin");

        static bool IsTenantAdmin(System.Security.Claims.ClaimsPrincipal u)
            => u.IsInRole("TenantAdmin") || u.HasClaim("role", "TenantAdmin") || u.HasClaim(ClaimTypes.Role, "TenantAdmin") || IsPlatformAdmin(u);

        static bool HasXeniaProduct(System.Security.Claims.ClaimsPrincipal u)
        {
            static string Normalize(string value)
                => value.Trim().Replace("_", "", StringComparison.OrdinalIgnoreCase).Replace("-", "", StringComparison.OrdinalIgnoreCase).ToUpperInvariant();

            return u.FindAll("product_codes").Any(c => Normalize(c.Value) is "SYNQAI" or "XENIA")
                || u.FindAll("enabled_products").Any(c => Normalize(c.Value) is "SYNQAI" or "XENIA")
                || u.FindAll("product_roles").Any(c => c.Value.StartsWith("SYNQ_AI:", StringComparison.OrdinalIgnoreCase) || c.Value.StartsWith("XENIA:", StringComparison.OrdinalIgnoreCase));
        }

        options.AddPolicy(XeniaPolicies.Read, policy =>
            policy.RequireAuthenticatedUser()
                  .RequireAssertion(ctx =>
                      IsPlatformAdmin(ctx.User) ||
                      ctx.User.HasClaim("permissions", XeniaPermissions.Read) ||
                      ctx.User.HasClaim("permissions", XeniaPermissions.Admin) ||
                      ctx.User.HasClaim("permissions", XeniaPermissions.ModulesRead) ||
                      ctx.User.HasClaim("permissions", XeniaPermissions.AdaptersRead) ||
                      ctx.User.HasClaim("permissions", XeniaPermissions.ConfigurationRead)));

        options.AddPolicy(XeniaPolicies.Admin, policy =>
            policy.RequireAuthenticatedUser()
                  .RequireAssertion(ctx =>
                      IsPlatformAdmin(ctx.User) ||
                      ctx.User.HasClaim("permissions", XeniaPermissions.Admin)));

        options.AddPolicy(XeniaPolicies.ModulesRead, policy =>
            policy.RequireAuthenticatedUser()
                  .RequireAssertion(ctx =>
                      IsPlatformAdmin(ctx.User) ||
                      ctx.User.HasClaim("permissions", XeniaPermissions.ModulesRead) ||
                      ctx.User.HasClaim("permissions", XeniaPermissions.Admin)));

        options.AddPolicy(XeniaPolicies.ModulesManage, policy =>
            policy.RequireAuthenticatedUser()
                  .RequireAssertion(ctx =>
                      IsPlatformAdmin(ctx.User) ||
                      ctx.User.HasClaim("permissions", XeniaPermissions.ModulesManage) ||
                      ctx.User.HasClaim("permissions", XeniaPermissions.Admin)));

        options.AddPolicy(XeniaPolicies.AdaptersRead, policy =>
            policy.RequireAuthenticatedUser()
                  .RequireAssertion(ctx =>
                      IsPlatformAdmin(ctx.User) ||
                      ctx.User.HasClaim("permissions", XeniaPermissions.AdaptersRead) ||
                      ctx.User.HasClaim("permissions", XeniaPermissions.Admin)));

        options.AddPolicy(XeniaPolicies.ConfigurationRead, policy =>
            policy.RequireAuthenticatedUser()
                  .RequireAssertion(ctx =>
                      IsPlatformAdmin(ctx.User) ||
                      ctx.User.HasClaim("permissions", XeniaPermissions.ConfigurationRead) ||
                      ctx.User.HasClaim("permissions", XeniaPermissions.Admin)));

        options.AddPolicy(XeniaPolicies.ConfigurationManage, policy =>
            policy.RequireAuthenticatedUser()
                  .RequireAssertion(ctx =>
                      IsPlatformAdmin(ctx.User) ||
                      ctx.User.HasClaim("permissions", XeniaPermissions.ConfigurationManage) ||
                      ctx.User.HasClaim("permissions", XeniaPermissions.Admin)));

        options.AddPolicy(XeniaPolicies.EmailRead, policy =>
            policy.RequireAuthenticatedUser()
                  .RequireAssertion(ctx =>
                      IsPlatformAdmin(ctx.User) ||
                      IsTenantAdmin(ctx.User) ||
                      ctx.User.HasClaim("permissions", XeniaPermissions.EmailRead) ||
                      ctx.User.HasClaim("permissions", XeniaPermissions.EmailManage) ||
                      ctx.User.HasClaim("permissions", XeniaPermissions.Admin)));

        options.AddPolicy(XeniaPolicies.EmailManage, policy =>
            policy.RequireAuthenticatedUser()
                  .RequireAssertion(ctx =>
                      IsPlatformAdmin(ctx.User) ||
                      IsTenantAdmin(ctx.User) ||
                      ctx.User.HasClaim("permissions", XeniaPermissions.EmailManage) ||
                      ctx.User.HasClaim("permissions", XeniaPermissions.Admin)));

        options.AddPolicy(XeniaPolicies.EmailValidate, policy =>
            policy.RequireAuthenticatedUser()
                  .RequireAssertion(ctx =>
                      IsPlatformAdmin(ctx.User) ||
                      IsTenantAdmin(ctx.User) ||
                      ctx.User.HasClaim("permissions", XeniaPermissions.EmailValidate) ||
                      ctx.User.HasClaim("permissions", XeniaPermissions.EmailManage) ||
                      ctx.User.HasClaim("permissions", XeniaPermissions.Admin)));

        options.AddPolicy(XeniaPolicies.EmailSync, policy =>
            policy.RequireAuthenticatedUser()
                  .RequireAssertion(ctx =>
                      IsPlatformAdmin(ctx.User) ||
                      IsTenantAdmin(ctx.User) ||
                      ctx.User.HasClaim("permissions", XeniaPermissions.EmailSync) ||
                      ctx.User.HasClaim("permissions", XeniaPermissions.EmailManage) ||
                      ctx.User.HasClaim("permissions", XeniaPermissions.Admin)));

        options.AddPolicy(XeniaPolicies.EmailOperationsRead, policy =>
            policy.RequireAuthenticatedUser()
                  .RequireAssertion(ctx =>
                      IsPlatformAdmin(ctx.User) ||
                      IsTenantAdmin(ctx.User) ||
                      ctx.User.HasClaim("permissions", XeniaPermissions.EmailOperationsRead) ||
                      ctx.User.HasClaim("permissions", XeniaPermissions.EmailOperationsManage) ||
                      ctx.User.HasClaim("permissions", XeniaPermissions.EmailManage) ||
                      ctx.User.HasClaim("permissions", XeniaPermissions.Admin)));

        options.AddPolicy(XeniaPolicies.EmailOperationsManage, policy =>
            policy.RequireAuthenticatedUser()
                  .RequireAssertion(ctx =>
                      IsPlatformAdmin(ctx.User) ||
                      IsTenantAdmin(ctx.User) ||
                      ctx.User.HasClaim("permissions", XeniaPermissions.EmailOperationsManage) ||
                      ctx.User.HasClaim("permissions", XeniaPermissions.EmailManage) ||
                      ctx.User.HasClaim("permissions", XeniaPermissions.Admin)));

        options.AddPolicy(XeniaPolicies.EmailAlertsManage, policy =>
            policy.RequireAuthenticatedUser()
                  .RequireAssertion(ctx =>
                      IsPlatformAdmin(ctx.User) ||
                      IsTenantAdmin(ctx.User) ||
                      ctx.User.HasClaim("permissions", XeniaPermissions.EmailAlertsManage) ||
                      ctx.User.HasClaim("permissions", XeniaPermissions.EmailOperationsManage) ||
                      ctx.User.HasClaim("permissions", XeniaPermissions.Admin)));

        options.AddPolicy(XeniaPolicies.EmailRetentionManage, policy =>
            policy.RequireAuthenticatedUser()
                  .RequireAssertion(ctx =>
                      IsPlatformAdmin(ctx.User) ||
                      ctx.User.HasClaim("permissions", XeniaPermissions.EmailRetentionManage) ||
                      ctx.User.HasClaim("permissions", XeniaPermissions.Admin)));

        options.AddPolicy(XeniaPolicies.AssistantUse, policy =>
            policy.RequireAuthenticatedUser()
                  .RequireAssertion(ctx =>
                      IsPlatformAdmin(ctx.User) ||
                      HasXeniaProduct(ctx.User) ||
                      ctx.User.HasClaim("permissions", XeniaPermissions.AssistantUse) ||
                      ctx.User.HasClaim("permissions", "SYNQ_AI.assistant:use") ||
                      ctx.User.HasClaim("permissions", XeniaPermissions.AssistantManage) ||
                      ctx.User.HasClaim("permissions", "SYNQ_AI.assistant:manage") ||
                      ctx.User.HasClaim("permissions", XeniaPermissions.Admin)));

        options.AddPolicy(XeniaPolicies.AssistantManage, policy =>
            policy.RequireAuthenticatedUser()
                  .RequireAssertion(ctx =>
                      IsPlatformAdmin(ctx.User) ||
                      ctx.User.HasClaim("permissions", XeniaPermissions.AssistantManage) ||
                      ctx.User.HasClaim("permissions", "SYNQ_AI.assistant:manage") ||
                      ctx.User.HasClaim("permissions", XeniaPermissions.Admin)));

        options.AddPolicy(XeniaPolicies.AssistantUsageRead, policy =>
            policy.RequireAuthenticatedUser()
                  .RequireAssertion(ctx =>
                      IsPlatformAdmin(ctx.User) ||
                      ctx.User.HasClaim("permissions", XeniaPermissions.AssistantUsageRead) ||
                      ctx.User.HasClaim("permissions", "SYNQ_AI.usage:read") ||
                      ctx.User.HasClaim("permissions", XeniaPermissions.AssistantManage) ||
                      ctx.User.HasClaim("permissions", "SYNQ_AI.assistant:manage") ||
                      ctx.User.HasClaim("permissions", XeniaPermissions.Admin)));
    });
}

static void ConfigureXeniaPipeline(IApplicationBuilder app, IWebHostEnvironment environment)
{
    var startupLogger = app.ApplicationServices.GetRequiredService<ILoggerFactory>()
        .CreateLogger("Xenia.Api.Startup");

    startupLogger.LogInformation(
        "Xenia service starting. Environment={Environment} Version={Version}",
        environment.EnvironmentName,
        XeniaBuildInfo.ServiceVersion);

    var cursorProtector = app.ApplicationServices.GetRequiredService<Xenia.Application.Email.Ingestion.IProviderCursorProtector>();
    if (cursorProtector.IsUsingDevFallbackKey)
    {
        if (!environment.IsDevelopment())
            startupLogger.LogCritical(
                "SECURITY: XeniaCursorProtection:Key is not configured. " +
                "The dev-fallback zero-key is active in a non-Development environment. " +
                "Set XeniaCursorProtection:Key to a 64-hex-char value before serving real traffic.");
        else
            startupLogger.LogWarning(
                "XeniaCursorProtection:Key not set — using dev zero-key. Safe for Development only.");
    }

    app.UseMiddleware<XeniaExceptionMiddleware>();
    app.UseMiddleware<XeniaCorrelationMiddleware>();
    app.UseRouting();
    app.UseAuthentication();
    app.UseMiddleware<XeniaTenantContextMiddleware>();
    app.UseAuthorization();
    app.UseEndpoints(MapXeniaEndpoints);
}

static void MapXeniaEndpoints(IEndpointRouteBuilder endpoints)
{
    endpoints.MapXeniaHealthEndpoints();
    endpoints.MapXeniaInfoEndpoints();

    endpoints.MapXeniaModuleEndpoints();
    endpoints.MapXeniaAdapterEndpoints();
    endpoints.MapXeniaConfigurationEndpoints();
    endpoints.MapXeniaAssistantEndpoints();
    endpoints.MapXeniaAssistantAdminEndpoints();

    endpoints.MapXeniaEmailModuleEndpoints();
    endpoints.MapXeniaEmailSourceEndpoints();
    endpoints.MapXeniaEmailProviderEndpoints();
    endpoints.MapXeniaEmailSettingsEndpoints();

    endpoints.MapXeniaEmailSyncEndpoints();
    endpoints.MapXeniaEmailMessageEndpoints();

    endpoints.MapXeniaEmailOperationsEndpoints();
    endpoints.MapXeniaEmailRunEndpoints();
    endpoints.MapXeniaEmailAlertEndpoints();
    endpoints.MapXeniaEmailRetentionEndpoints();

    endpoints.MapAutomationEndpoints();

    endpoints.MapGet("/secure/ping", (HttpContext ctx) =>
    {
        var sub = ctx.User.FindFirst("sub")?.Value ?? "unknown";
        return Results.Ok(new { status = "ok", sub });
    }).RequireAuthorization(XeniaPolicies.Read);
}

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
    public const string EmailSync         = "XeniaEmailSync";
    public const string EmailOperationsRead    = "XeniaEmailOperationsRead";
    public const string EmailOperationsManage  = "XeniaEmailOperationsManage";
    public const string EmailAlertsManage      = "XeniaEmailAlertsManage";
    public const string EmailRetentionManage   = "XeniaEmailRetentionManage";
    public const string AssistantUse           = "XeniaAssistantUse";
    public const string AssistantManage        = "XeniaAssistantManage";
    public const string AssistantUsageRead     = "XeniaAssistantUsageRead";
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
    public const string EmailRead     = "xenia.email.read";
    public const string EmailManage   = "xenia.email.manage";
    public const string EmailValidate = "xenia.email.validate";
    public const string EmailSync             = "xenia.email.sync";
    public const string EmailOperationsRead   = "xenia.email.operations.read";
    public const string EmailOperationsManage = "xenia.email.operations.manage";
    public const string EmailAlertsManage     = "xenia.email.alerts.manage";
    public const string EmailRetentionManage  = "xenia.email.retention.manage";
    public const string AssistantUse          = "xenia.assistant.use";
    public const string AssistantManage       = "xenia.assistant.manage";
    public const string AssistantUsageRead    = "xenia.usage.read";
}

public static class XeniaBuildInfo
{
    public static readonly string ServiceVersion =
        typeof(XeniaBuildInfo).Assembly.GetName().Version?.ToString() ?? "1.0.0";
    public static readonly DateTime StartedAt = DateTime.UtcNow;
}
