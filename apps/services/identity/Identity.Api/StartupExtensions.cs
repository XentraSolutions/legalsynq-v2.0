using Identity.Domain;
using Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Identity.Api;

/// <summary>
/// Extension methods that move the startup diagnostic / seeding / fixup
/// blocks out of Program.cs so it remains readable.
/// </summary>
internal static class StartupExtensions
{
    // ── Dev: apply EF migrations ──────────────────────────────────────────────

    public static void RunDevMigrations(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment()) return;

        try
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            db.Database.Migrate();
            app.Logger.LogInformation("Database migrations applied");
        }
        catch (Exception ex)
        {
            app.Logger.LogWarning(ex,
                "Could not apply migrations — ensure MySQL is running and connection string is correct");
        }
    }

    // ── Phase G diagnostics ───────────────────────────────────────────────────

    public static async Task RunPhaseGDiagnosticsAsync(this WebApplication app)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

            // 1. Verify all active ProductRoles have OrgTypeRule coverage.
            var unrestrictedRoleCount = await db.Set<ProductRole>()
                .Where(pr => pr.IsActive)
                .Where(pr => !db.Set<ProductOrganizationTypeRule>()
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
                var totalActive = await db.Set<ProductRole>().CountAsync(pr => pr.IsActive);
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

            // 3. Org-type consistency check.
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
                    var expectedCode = OrgTypeMapper.TryResolveCode(o.OrganizationTypeId);
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
    }

    // ── UIX-002-C: sync ProductRoles → Roles table ───────────────────────────

    public static async Task SeedProductRolesAsync(this WebApplication app)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

            var activeProductRoles = await db.ProductRoles
                .Include(pr => pr.Product)
                .Where(pr => pr.IsActive)
                .ToListAsync();

            var existingRoleNames = await db.Roles
                .Select(r => r.Name)
                .ToListAsync();

            var existingNameSet = new HashSet<string>(existingRoleNames, StringComparer.OrdinalIgnoreCase);

            var platformTenantId = await db.Tenants
                .Where(t => t.IsActive)
                .OrderBy(t => t.CreatedAtUtc)
                .Select(t => t.Id)
                .FirstOrDefaultAsync();

            if (platformTenantId == Guid.Empty)
            {
                app.Logger.LogWarning("UIX-002-C: No active tenant found — skipping product role seed.");
                return;
            }

            var created = 0;
            foreach (var pr in activeProductRoles)
            {
                if (existingNameSet.Contains(pr.Code)) continue;

                var role = Role.Create(
                    tenantId: platformTenantId,
                    name: pr.Code,
                    description: $"[Product] {pr.Name} — {pr.Description ?? pr.Product.Name}",
                    isSystemRole: false);
                db.Roles.Add(role);
                existingNameSet.Add(pr.Code);
                created++;

                app.Logger.LogInformation(
                    "UIX-002-C: Seeded Role '{RoleName}' for ProductRole {ProductRoleCode} (Product: {Product})",
                    pr.Code, pr.Code, pr.Product.Name);
            }

            if (created > 0)
                await db.SaveChangesAsync();

            app.Logger.LogInformation(
                "UIX-002-C product role sync complete — {Created} new Role(s) seeded, {Total} product roles active.",
                created, activeProductRoles.Count);
        }
        catch (Exception ex)
        {
            app.Logger.LogWarning(ex, "UIX-002-C product role seed encountered an error");
        }
    }

    // ── Dev-only: heal missing org memberships and OrganizationProduct rows ───

    public static async Task RunDevFixupsAsync(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment()) return;

        try
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

            // Heal users with no org membership.
            var usersWithoutMembership = await db.Users
                .Where(u => u.IsActive)
                .Where(u => !db.UserOrganizationMemberships.Any(m => m.UserId == u.Id))
                .ToListAsync();

            foreach (var orphan in usersWithoutMembership)
            {
                var org = await db.Organizations
                    .Where(o => o.TenantId == orphan.TenantId && o.IsActive)
                    .OrderBy(o => o.CreatedAtUtc)
                    .FirstOrDefaultAsync();

                if (org is null) continue;

                var membership = UserOrganizationMembership.Create(
                    orphan.Id, org.Id, MemberRole.Admin);
                membership.SetPrimary();
                db.UserOrganizationMemberships.Add(membership);

                app.Logger.LogInformation(
                    "Dev fixup: created org membership for user {UserId} ({Email}) → org {OrgId} ({OrgName})",
                    orphan.Id, orphan.Email, org.Id, org.Name);
            }

            if (usersWithoutMembership.Count > 0)
                await db.SaveChangesAsync();

            // Ensure every active user has exactly one primary membership.
            var allMemberships = await db.UserOrganizationMemberships
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
                await db.SaveChangesAsync();

            // Ensure OrganizationProduct rows exist for every TenantProduct + Org pair.
            var tenantProducts = await db.Set<TenantProduct>()
                .Where(tp => tp.IsEnabled)
                .ToListAsync();

            foreach (var tp in tenantProducts)
            {
                var orgs = await db.Organizations
                    .Include(o => o.OrganizationProducts)
                    .Where(o => o.TenantId == tp.TenantId && o.IsActive)
                    .ToListAsync();

                foreach (var org in orgs)
                {
                    if (org.OrganizationProducts.Any(op => op.ProductId == tp.ProductId))
                        continue;

                    var op = OrganizationProduct.Create(org.Id, tp.ProductId);
                    db.Set<OrganizationProduct>().Add(op);

                    app.Logger.LogInformation(
                        "Dev fixup: created OrganizationProduct for org {OrgId} ({OrgName}) → product {ProductId}",
                        org.Id, org.Name, tp.ProductId);
                }
            }

            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            app.Logger.LogWarning(ex, "Dev fixup for user/org memberships encountered an error");
        }
    }
}
