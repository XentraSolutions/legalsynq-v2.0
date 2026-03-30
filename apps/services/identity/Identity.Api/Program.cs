using System.Text;
using Contracts;
using Identity.Api.Endpoints;
using Identity.Infrastructure;
using Identity.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

const string ServiceName = "identity";
const string Version = "v1";

var builder = WebApplication.CreateBuilder(args);

builder.Logging
    .ClearProviders()
    .AddConsole();

builder.Services.AddInfrastructure(builder.Configuration);

// ── JWT authentication (for GET /api/auth/me) ─────────────────────────────
// Identity.Api both ISSUES and VALIDATES JWTs.
// Validation here is used only for the /auth/me endpoint (called by the Next.js BFF).
// The gateway handles JWT validation for all other downstream service routes.
var jwtSection   = builder.Configuration.GetSection("Jwt");
var signingKey   = jwtSection["SigningKey"]
    ?? throw new InvalidOperationException("Jwt:SigningKey is not configured.");
var issuer       = jwtSection["Issuer"]   ?? "legalsynq-identity";
var audience     = jwtSection["Audience"] ?? "legalsynq-platform";

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;   // keep claim names as-is (sub, email, etc.)
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidIssuer              = issuer,
            ValidateAudience         = true,
            ValidAudience            = audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            ValidateLifetime         = true,
            ClockSkew                = TimeSpan.Zero,
            RoleClaimType            = System.Security.Claims.ClaimTypes.Role,
            NameClaimType            = System.Security.Claims.ClaimTypes.NameIdentifier,
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();
var env = app.Environment.EnvironmentName;

app.Logger.LogInformation("Starting {Service} {Version} in {Environment}", ServiceName, Version, env);

if (app.Environment.IsDevelopment())
{
    try
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        db.Database.Migrate();
        app.Logger.LogInformation("Database migrations applied");
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Could not apply migrations — ensure MySQL is running and connection string is correct");
    }
}

// ── Startup diagnostic: Phase G authorization status ─────────────────────────
// Phase G COMPLETE: UserRoles + UserRoleAssignments tables dropped.
// Diagnostics verify OrgTypeRule coverage and ScopedRoleAssignment counts.
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

    // 1. Verify all restricted ProductRoles have OrgTypeRule coverage.
    var unrestrictedRoleCount = await db.Set<Identity.Domain.ProductRole>()
        .Where(pr => pr.IsActive)
        .Where(pr => !db.Set<Identity.Domain.ProductOrganizationTypeRule>()
            .Any(r => r.ProductRoleId == pr.Id && r.IsActive))
        .CountAsync();

    if (unrestrictedRoleCount > 0)
    {
        app.Logger.LogWarning(
            "{Count} active ProductRole(s) have no ProductOrganizationTypeRule rows and are " +
            "therefore unrestricted. If restriction was intended, add OrgTypeRule seed data.",
            unrestrictedRoleCount);
    }
    else
    {
        var totalActive = await db.Set<Identity.Domain.ProductRole>().CountAsync(pr => pr.IsActive);
        app.Logger.LogInformation(
            "Phase G eligibility check passed — {Count} active ProductRole(s), all OrgTypeRule-covered.",
            totalActive);
    }

    // 2. Log ScopedRoleAssignment totals (Phase G: sole authoritative role source).
    var scopedTotal = await db.ScopedRoleAssignments
        .CountAsync(s => s.IsActive && s.ScopeType == "GLOBAL");
    var scopedUsers = await db.ScopedRoleAssignments
        .Where(s => s.IsActive && s.ScopeType == "GLOBAL")
        .Select(s => s.UserId)
        .Distinct()
        .CountAsync();
    app.Logger.LogInformation(
        "Phase G role check: {Assignments} active GLOBAL ScopedRoleAssignment(s) across {Users} user(s).",
        scopedTotal, scopedUsers);

    // 3. Org type consistency: flag organizations where OrganizationTypeId FK is null
    //    but OrgType string is populated, or where the FK code doesn't match the string.
    var orgTypeCheckRows = await db.Organizations
        .Where(o => o.IsActive)
        .Select(o => new { o.Id, o.OrgType, o.OrganizationTypeId })
        .ToListAsync();

    var orgsWithMissingTypeId = orgTypeCheckRows
        .Count(o => o.OrganizationTypeId == null && !string.IsNullOrWhiteSpace(o.OrgType));

    var orgsWithCodeMismatch = orgTypeCheckRows
        .Count(o =>
        {
            if (o.OrganizationTypeId == null) return false;
            var expectedCode = Identity.Domain.OrgTypeMapper.TryResolveCode(o.OrganizationTypeId);
            return expectedCode is not null &&
                   !string.Equals(expectedCode, o.OrgType, StringComparison.OrdinalIgnoreCase);
        });

    if (orgsWithMissingTypeId > 0)
        app.Logger.LogWarning(
            "OrgType consistency: {Count} active org(s) have OrgType string but no OrganizationTypeId. " +
            "Run a backfill migration (or update via Organization.Create) to populate the FK.",
            orgsWithMissingTypeId);

    if (orgsWithCodeMismatch > 0)
        app.Logger.LogWarning(
            "OrgType consistency: {Count} active org(s) have an OrganizationTypeId whose OrgTypeMapper " +
            "code does not match the stored OrgType string. Investigate and reconcile.",
            orgsWithCodeMismatch);

    if (orgsWithMissingTypeId == 0 && orgsWithCodeMismatch == 0)
        app.Logger.LogInformation(
            "OrgType consistency check passed — {Total} active org(s) all have consistent OrganizationTypeId and OrgType.",
            orgTypeCheckRows.Count);
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex,
        "Phase G startup diagnostic skipped — could not query the database at startup.");
}

// ── Middleware pipeline ───────────────────────────────────────────────────
// UseAuthentication + UseAuthorization must precede all endpoint maps
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () =>
    Results.Ok(new HealthResponse("ok", ServiceName)));

app.MapGet("/info", () =>
    Results.Ok(new InfoResponse(ServiceName, env, Version)));

app.MapTenantEndpoints();
app.MapProductEndpoints();
app.MapUserEndpoints();
app.MapAuthEndpoints();
app.MapTenantBrandingEndpoints();
app.MapAdminEndpoints();

app.Run();
