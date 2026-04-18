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

// ── Migration coverage self-test ─────────────────────────────────────────
// Compares every EF-mapped column against information_schema. If a model
// property has no backing column on the live database, log an ERROR so the
// regression is loud at boot (and visible to CI / log aggregation).
// This catches the class of bug behind Task #58: a migration committed
// without its [Migration] attribute (or otherwise un-applied) leaves the
// EF model and the live schema out of sync, which previously surfaced only
// as runtime "Unknown column" SQL errors at login. Runs after Migrate()
// so anything still missing is a true gap.
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    await BuildingBlocks.Diagnostics.MigrationCoverageProbe.RunAsync(db, app.Logger);
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "Migration coverage self-test could not run");
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

// ── UIX-002-C: Ensure each active ProductRole has a corresponding entry in the Roles table ──
// Product roles are defined in the ProductRoles table but need corresponding entries in the
// Roles table (with IsSystemRole = false) so they can be assigned through ScopedRoleAssignment.
// This seeder is idempotent — it only creates missing entries.
try
{
    using var prScope = app.Services.CreateScope();
    var prDb = prScope.ServiceProvider.GetRequiredService<IdentityDbContext>();

    var activeProductRoles = await prDb.ProductRoles
        .Include(pr => pr.Product)
        .Where(pr => pr.IsActive)
        .ToListAsync();

    var existingRoleNames = await prDb.Roles
        .Select(r => r.Name)
        .ToListAsync();

    var existingNameSet = new HashSet<string>(existingRoleNames, StringComparer.OrdinalIgnoreCase);

    var platformTenantId = await prDb.Tenants
        .Where(t => t.IsActive)
        .OrderBy(t => t.CreatedAtUtc)
        .Select(t => t.Id)
        .FirstOrDefaultAsync();

    if (platformTenantId == Guid.Empty)
    {
        app.Logger.LogWarning("UIX-002-C: No active tenant found — skipping product role seed.");
    }

    var created = 0;
    foreach (var pr in activeProductRoles)
    {
        if (platformTenantId == Guid.Empty)
            break;

        if (existingNameSet.Contains(pr.Code))
            continue;

        var role = Identity.Domain.Role.Create(
            tenantId: platformTenantId,
            name: pr.Code,
            description: $"[Product] {pr.Name} — {pr.Description ?? pr.Product.Name}",
            isSystemRole: false);
        prDb.Roles.Add(role);
        existingNameSet.Add(pr.Code);
        created++;

        app.Logger.LogInformation(
            "UIX-002-C: Seeded Role '{RoleName}' for ProductRole {ProductRoleCode} (Product: {Product})",
            pr.Code, pr.Code, pr.Product.Name);
    }

    if (created > 0)
        await prDb.SaveChangesAsync();

    app.Logger.LogInformation(
        "UIX-002-C product role sync complete — {Created} new Role(s) seeded, {Total} product roles active.",
        created, activeProductRoles.Count);
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "UIX-002-C product role seed encountered an error");
}

// ── Dev-only: ensure every user has a primary org membership ─────────────
// Some tenants were created with the org added separately, leaving the user
// without a UserOrganizationMembership.  This block auto-heals that gap.
if (app.Environment.IsDevelopment())
{
    try
    {
        using var fixScope = app.Services.CreateScope();
        var fixDb = fixScope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        var usersWithoutMembership = await fixDb.Users
            .Where(u => u.IsActive)
            .Where(u => !fixDb.UserOrganizationMemberships.Any(m => m.UserId == u.Id))
            .ToListAsync();

        foreach (var orphan in usersWithoutMembership)
        {
            var org = await fixDb.Organizations
                .Where(o => o.TenantId == orphan.TenantId && o.IsActive)
                .OrderBy(o => o.CreatedAtUtc)
                .FirstOrDefaultAsync();

            if (org is null) continue;

            var membership = Identity.Domain.UserOrganizationMembership.Create(
                orphan.Id, org.Id, Identity.Domain.MemberRole.Admin);
            membership.SetPrimary();
            fixDb.UserOrganizationMemberships.Add(membership);

            app.Logger.LogInformation(
                "Dev fixup: created org membership for user {UserId} ({Email}) → org {OrgId} ({OrgName})",
                orphan.Id, orphan.Email, org.Id, org.Name);
        }

        if (usersWithoutMembership.Count > 0)
            await fixDb.SaveChangesAsync();

        var allMemberships = await fixDb.UserOrganizationMemberships
            .Where(m => m.IsActive)
            .ToListAsync();

        var userIdsWithPrimary = allMemberships.Where(m => m.IsPrimary).Select(m => m.UserId).ToHashSet();
        var needsPrimary = allMemberships
            .Where(m => !userIdsWithPrimary.Contains(m.UserId))
            .GroupBy(m => m.UserId)
            .Select(g => g.OrderBy(m => m.JoinedAtUtc).First())
            .ToList();

        foreach (var m in needsPrimary)
        {
            m.SetPrimary();
            app.Logger.LogInformation(
                "Dev fixup: set primary org membership for user {UserId} → org {OrgId}",
                m.UserId, m.OrganizationId);
        }

        if (needsPrimary.Count > 0)
            await fixDb.SaveChangesAsync();

        // Also ensure OrganizationProduct rows exist for every active TenantProduct + Org pair
        var tenantProducts = await fixDb.Set<Identity.Domain.TenantProduct>()
            .Where(tp => tp.IsEnabled)
            .ToListAsync();

        foreach (var tp in tenantProducts)
        {
            var orgs = await fixDb.Organizations
                .Include(o => o.OrganizationProducts)
                .Where(o => o.TenantId == tp.TenantId && o.IsActive)
                .ToListAsync();

            foreach (var org in orgs)
            {
                if (org.OrganizationProducts.Any(op => op.ProductId == tp.ProductId))
                    continue;

                var op = Identity.Domain.OrganizationProduct.Create(org.Id, tp.ProductId);
                fixDb.Set<Identity.Domain.OrganizationProduct>().Add(op);

                app.Logger.LogInformation(
                    "Dev fixup: created OrganizationProduct for org {OrgId} ({OrgName}) → product {ProductId}",
                    org.Id, org.Name, tp.ProductId);
            }
        }

        await fixDb.SaveChangesAsync();
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Dev fixup for user/org memberships encountered an error");
    }
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
app.MapAccessSourceEndpoints();
app.MapGroupEndpoints();

app.Run();
