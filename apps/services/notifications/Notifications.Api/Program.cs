using System.Security.Claims;
using System.Text;
using BuildingBlocks.Authentication.ServiceTokens;
using BuildingBlocks.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Notifications.Api.Authorization;
using Notifications.Api.Endpoints;
using Notifications.Api.Middleware;
using Notifications.Infrastructure;
using Notifications.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

// ── JWT Authentication ────────────────────────────────────────────────────────

var jwtSection = builder.Configuration.GetSection("Jwt");
var signingKey = jwtSection["SigningKey"]
    ?? throw new InvalidOperationException("Jwt:SigningKey is not configured.");

// LS-NOTIF-CORE-021: service token signing key (shared platform secret).
// Preferred from FLOW_SERVICE_TOKEN_SECRET env var, then ServiceTokens:SigningKey config.
var serviceTokenKey =
    Environment.GetEnvironmentVariable(ServiceTokenAuthenticationDefaults.SecretEnvVar)
    ?? builder.Configuration[$"{ServiceTokenOptions.SectionName}:SigningKey"]
    ?? string.Empty;

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    // ── Scheme 1: user JWTs from Identity ────────────────────────────────
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
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            RoleClaimType            = ClaimTypes.Role,
            ClockSkew                = TimeSpan.Zero,
        };
    })
    // ── Scheme 2: service-to-service JWTs (LS-NOTIF-CORE-021) ────────────
    // Accepts tokens minted by ServiceTokenIssuer from any producer service.
    // Validates: issuer=legalsynq-service-tokens, audience=notifications-service
    // OR flow-service (for Flow's existing issuer config), subject=service:*
    .AddJwtBearer(ServiceTokenAuthenticationDefaults.Scheme, options =>
    {
        options.MapInboundClaims    = false;
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = !string.IsNullOrWhiteSpace(serviceTokenKey),
            RequireSignedTokens      = true,
            RequireExpirationTime    = true,
            ValidIssuer              = ServiceTokenAuthenticationDefaults.DefaultIssuer,
            // Accept notifications-service (new preferred) + flow-service
            // (Flow's existing issuer defaults) + legalsynq-services (future).
            ValidAudiences           = ["notifications-service", "flow-service", "legalsynq-services"],
            IssuerSigningKey         = string.IsNullOrWhiteSpace(serviceTokenKey)
                ? null
                : new SymmetricSecurityKey(Encoding.UTF8.GetBytes(serviceTokenKey)),
            NameClaimType            = "sub",
            RoleClaimType            = ClaimTypes.Role,
            ClockSkew                = TimeSpan.FromSeconds(30),
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = ctx =>
            {
                var sub = ctx.Principal?.FindFirst("sub")?.Value;
                if (string.IsNullOrWhiteSpace(sub) ||
                    !sub.StartsWith("service:", StringComparison.Ordinal))
                {
                    ctx.Fail("Service token must have a subject starting with 'service:'.");
                }
                return Task.CompletedTask;
            },
            OnAuthenticationFailed = ctx =>
            {
                var log = ctx.HttpContext.RequestServices
                    .GetService<ILoggerFactory>()
                    ?.CreateLogger(ServiceTokenAuthenticationDefaults.Scheme);
                log?.LogWarning(ctx.Exception,
                    "ServiceToken authentication failed. Path={Path}",
                    ctx.HttpContext.Request.Path);
                return Task.CompletedTask;
            },
        };
    });

// ── HTTP context accessor (required by ServiceSubmissionHandler) ──────────────
builder.Services.AddHttpContextAccessor();

// ── Authorization ─────────────────────────────────────────────────────────────
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Policies.AuthenticatedUser, policy =>
        policy.RequireAuthenticatedUser());

    options.AddPolicy(Policies.AdminOnly, policy =>
        policy.RequireRole(Roles.PlatformAdmin));

    // LS-NOTIF-CORE-021 — service submission gate on POST /v1/notifications.
    // Tries both the user JWT scheme and the ServiceToken scheme;
    // the custom handler also allows legacy X-Tenant-Id header requests.
    options.AddPolicy(Policies.ServiceSubmission, policy =>
        policy
            .AddAuthenticationSchemes(
                JwtBearerDefaults.AuthenticationScheme,
                ServiceTokenAuthenticationDefaults.Scheme)
            .AddRequirements(new ServiceSubmissionRequirement()));
});

// Register the custom authorization handler for ServiceSubmission.
builder.Services.AddSingleton<IAuthorizationHandler, ServiceSubmissionHandler>();

// ── Application services ─────────────────────────────────────────────────────

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

// ── Database startup ──────────────────────────────────────────────────────────

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();

    try
    {
        await SchemaRenamer.RenameSchemaAsync(db, app.Logger);
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Schema rename step failed — tables/columns may already be renamed");
    }

    try
    {
        await db.Database.MigrateAsync();
        app.Logger.LogInformation("Notifications database migrated successfully");
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Could not apply Notifications database migrations on startup — schema may be out of sync.");
    }

    try
    {
        await BuildingBlocks.Diagnostics.MigrationCoverageProbe.RunAsync(db, app.Logger);
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Migration coverage self-test could not run");
    }
}

// ── Middleware pipeline ───────────────────────────────────────────────────────
// Order matters: Authentication → Authorization → custom middleware → endpoints.
// TenantMiddleware is placed AFTER UseAuthentication so it can read context.User
// to extract tenant_id from JWT claims for authenticated requests.

app.UseMiddleware<RawBodyMiddleware>();
app.UseMiddleware<InternalTokenMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<TenantMiddleware>();

// ── Endpoints ─────────────────────────────────────────────────────────────────

app.MapHealthEndpoints();
app.MapNotificationEndpoints();
app.MapAdminNotificationEndpoints();
app.MapTemplateEndpoints();
app.MapGlobalTemplateEndpoints();
app.MapProviderEndpoints();
app.MapWebhookEndpoints();
app.MapBillingEndpoints();
app.MapContactEndpoints();
app.MapBrandingEndpoints();
app.MapInternalEndpoints();

app.Run();
