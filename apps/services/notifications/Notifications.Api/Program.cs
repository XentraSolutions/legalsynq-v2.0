using System.Security.Claims;
using System.Text;
using BuildingBlocks.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Notifications.Api.Endpoints;
using Notifications.Api.Middleware;
using Notifications.Infrastructure;
using Notifications.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

// ── JWT Authentication ────────────────────────────────────────────────────────

var jwtSection = builder.Configuration.GetSection("Jwt");
var signingKey = jwtSection["SigningKey"]
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
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            RoleClaimType            = ClaimTypes.Role,
            ClockSkew                = TimeSpan.Zero,
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Policies.AuthenticatedUser, policy =>
        policy.RequireAuthenticatedUser());

    options.AddPolicy(Policies.AdminOnly, policy =>
        policy.RequireRole(Roles.PlatformAdmin));
});

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
