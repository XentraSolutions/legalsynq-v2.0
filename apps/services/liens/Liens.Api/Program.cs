using System.Security.Claims;
using System.Text;
using BuildingBlocks.Authorization;
using BuildingBlocks.Context;
using BuildingBlocks.FlowClient;
using Contracts;
using Liens.Api.Endpoints;
using Liens.Api.Middleware;
using Liens.Domain;
using Liens.Infrastructure;
using Liens.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

const string ServiceName = "liens";
const string Version = "v1";

var builder = WebApplication.CreateBuilder(args);

builder.Logging
    .ClearProviders()
    .AddConsole();

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

builder.Services.AddLiensServices(builder.Configuration);
// LS-FLOW-MERGE-P4 — shared Flow HTTP adapter (bearer pass-through, retry, 503 mapping).
builder.Services.AddFlowClient(builder.Configuration, serviceName: "synqlien");

var app = builder.Build();

var env = app.Environment.EnvironmentName;
app.Logger.LogInformation("Starting {Service} {Version} in {Environment}", ServiceName, Version, env);

// Auto-migrate — apply pending EF Core migrations in all environments (not just Development).
// __EFMigrationsHistory tracks applied migrations; idempotent across restarts and re-deploys.
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
    await db.Database.MigrateAsync();
    app.Logger.LogInformation("Liens database migrations applied successfully.");
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "Could not apply Liens database migrations on startup — schema may be out of sync.");
}

// ── Migration coverage self-test ─────────────────────────────────────────
// Compares every EF-mapped column against the live schema and logs an ERROR
// if any are missing. Catches the class of bug behind Task #58: a migration
// committed without its [Migration] attribute (or otherwise un-applied)
// leaves the EF model and the live schema out of sync, which previously
// surfaced only as runtime "Unknown column" SQL errors.
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
    await BuildingBlocks.Diagnostics.MigrationCoverageProbe.RunAsync(db, app.Logger);
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "Migration coverage self-test could not run");
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () =>
    Results.Ok(new HealthResponse("ok", ServiceName)))
    .AllowAnonymous();

app.MapGet("/info", () =>
    Results.Ok(new InfoResponse(ServiceName, env, Version)))
    .AllowAnonymous();

app.MapGet("/context", (ICurrentRequestContext ctx) =>
{
    if (!ctx.IsAuthenticated)
        return Results.Unauthorized();

    return Results.Ok(new
    {
        authenticated = true,
        userId        = ctx.UserId,
        tenantId      = ctx.TenantId,
        tenantCode    = ctx.TenantCode,
        email         = ctx.Email,
        orgId         = ctx.OrgId,
        orgType       = ctx.OrgType,
        orgTypeId     = ctx.OrgTypeId,
        roles         = ctx.Roles,
        productRoles  = ctx.ProductRoles,
        permissions   = ctx.Permissions,
        isPlatformAdmin = ctx.IsPlatformAdmin,
        capabilities = new
        {
            sell = ctx.CanSellLiens(),
            manageInternal = ctx.CanManageLiensInternal(),
            resolved = ctx.GetLiensCapabilities(),
        },
    });
})
.RequireAuthorization(Policies.AuthenticatedUser);

app.MapLienEndpoints();
app.MapLienOfferEndpoints();
app.MapBillOfSaleEndpoints();
app.MapCaseEndpoints();
app.MapServicingEndpoints();
app.MapContactEndpoints();
// LS-FLOW-MERGE-P4 — product → Flow integration endpoints.
app.MapWorkflowEndpoints();

app.Run();
