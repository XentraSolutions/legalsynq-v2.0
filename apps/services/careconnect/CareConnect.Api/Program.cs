using System.Security.Claims;
using BuildingBlocks.Authorization;
using BuildingBlocks.Authentication.ServiceTokens;
using BuildingBlocks.Context;
using BuildingBlocks.FlowClient;
using CareConnect.Api.Endpoints;
using CareConnect.Api.Middleware;
using CareConnect.Infrastructure;
using CareConnect.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// JWT Authentication
var jwtSection = builder.Configuration.GetSection("Jwt");
var signingKey = jwtSection["SigningKey"]
    ?? throw new InvalidOperationException("Jwt:SigningKey is not configured.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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
            RoleClaimType            = ClaimTypes.Role
        };
    })
    // M2M service-token bearer — validates HS256 tokens minted by platform services.
    // Secret is read from FLOW_SERVICE_TOKEN_SECRET env var (see ServiceTokenAuthenticationDefaults).
    .AddServiceTokenBearer(builder.Configuration);

// Authorization policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Policies.AuthenticatedUser, policy =>
        policy.RequireAuthenticatedUser());

    options.AddPolicy(Policies.PlatformOrTenantAdmin, policy =>
        policy.RequireRole(Roles.PlatformAdmin, Roles.TenantAdmin));

    // Internal M2M endpoints — only accept service tokens (not user JWTs).
    options.AddPolicy("ServiceOnly", policy =>
        policy
            .AddAuthenticationSchemes(ServiceTokenAuthenticationDefaults.Scheme)
            .RequireRole(ServiceTokenAuthenticationDefaults.ServiceRole));
});

// Infrastructure (DbContext + repositories + services)
builder.Services.AddInfrastructure(builder.Configuration);
// LS-FLOW-MERGE-P4 — shared Flow HTTP adapter (bearer pass-through, retry, 503 mapping).
builder.Services.AddFlowClient(builder.Configuration, serviceName: "careconnect");

// Request context
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentRequestContext, CurrentRequestContext>();

var app = builder.Build();

// Auto-migrate — apply pending EF Core migrations on startup in all environments.
// CareConnect uses MySQL (RDS) and the __EFMigrationsHistory table tracks which
// migrations have already been applied, so this is safe and idempotent.
// Fail fast if migrations cannot be applied — serving traffic with an incompatible
// schema causes silent 500s and process crashes that are harder to diagnose.
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CareConnectDbContext>();
    db.Database.Migrate();
    app.Logger.LogInformation("CareConnect database migrations applied successfully.");
}

// ── Migration coverage self-test ─────────────────────────────────────────
// Compares every EF-mapped column against the live schema and logs an ERROR
// if any are missing. Guards against the regression behind Task #58 —
// a migration committed without its [Migration] attribute (or otherwise
// un-applied) leaves the EF model and the live schema out of sync, which
// previously surfaced only as runtime "Unknown column" SQL errors.
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CareConnectDbContext>();
    await BuildingBlocks.Diagnostics.MigrationCoverageProbe.RunAsync(db, app.Logger);
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "Migration coverage self-test could not run");
}

// ── Phase H startup diagnostic: provider/facility Identity linkage health ─────
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CareConnectDbContext>();

    var totalProviders           = await db.Providers.CountAsync(p => p.IsActive);
    var providersWithoutOrgLink  = await db.Providers.CountAsync(p => p.IsActive && p.OrganizationId == null);
    var totalFacilities          = await db.Facilities.CountAsync(f => f.IsActive);
    var facilitiesWithoutOrgLink = await db.Facilities.CountAsync(f => f.IsActive && f.OrganizationId == null);

    if (providersWithoutOrgLink > 0)
        app.Logger.LogWarning(
            "Linkage health: {Count}/{Total} active Provider(s) have no Identity Organization link (OrganizationId is null). " +
            "These providers cannot participate in cross-service org-scoped authorization.",
            providersWithoutOrgLink, totalProviders);
    else
        app.Logger.LogInformation(
            "Linkage health: all {Total} active Provider(s) have an Identity Organization link.",
            totalProviders);

    if (facilitiesWithoutOrgLink > 0)
        app.Logger.LogWarning(
            "Linkage health: {Count}/{Total} active Facility(ies) have no Identity Organization link (OrganizationId is null). " +
            "These facilities cannot participate in cross-service org-scoped authorization.",
            facilitiesWithoutOrgLink, totalFacilities);
    else
        app.Logger.LogInformation(
            "Linkage health: all {Total} active Facility(ies) have an Identity Organization link.",
            totalFacilities);
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex,
        "CareConnect Phase H startup diagnostic skipped — could not query the database at startup.");
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

// Health & info
app.MapGet("/health", () => Results.Ok(new { status = "healthy" })).AllowAnonymous();
app.MapGet("/info",   () => Results.Ok(new { service = "CareConnect", version = "1.0.0" })).AllowAnonymous();

// Internal service-to-service endpoints
app.MapInternalProvisionEndpoints();

// API endpoints
app.MapCareConnectIntegrityEndpoints();
app.MapProviderAdminEndpoints();
app.MapAdminDashboardEndpoints();   // LSCC-01-004: admin dashboard, blocked queue, referral monitor
app.MapPerformanceEndpoints();      // LSCC-01-005: referral performance metrics
app.MapAdminBackfillEndpoints();
app.MapActivationAdminEndpoints(); // LSCC-009
app.MapAnalyticsEndpoints();      // LSCC-011
// LS-FLOW-MERGE-P4 — product → Flow integration endpoints.
app.MapWorkflowEndpoints();
app.MapProviderEndpoints();
app.MapReferralEndpoints();
app.MapCategoryEndpoints();
app.MapFacilityEndpoints();
app.MapServiceOfferingEndpoints();
app.MapAvailabilityTemplateEndpoints();
app.MapSlotEndpoints();
app.MapAppointmentEndpoints();
app.MapAvailabilityExceptionEndpoints();
app.MapReferralNoteEndpoints();
app.MapAppointmentNoteEndpoints();
app.MapAttachmentEndpoints();
app.MapNotificationEndpoints();

app.Run();
