using Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations;

/// <summary>
/// Corrects overbroad GLOBAL ScopedRoleAssignments for PROVIDER and LAW_FIRM users.
///
/// Context:
///   Two separate backfill operations created incorrect GLOBAL ScopedRoleAssignments
///   for CareConnect-only users (PROVIDER org = receiver, LAW_FIRM org = referrer):
///
///   1. Migration 20260426000001_SeedSupportRoles assigned TenantAdmin to every active
///      user with no existing GLOBAL assignment — including CC-only org users.
///
///   2. Earlier phase-G backfill (20260330200002) promoted every row from the old
///      UserRoles join table to GLOBAL ScopedRoleAssignments with no scope filter,
///      so product roles such as CARECONNECT_REFERRER also ended up as GLOBAL entries.
///
///   AuthService.BuildLoginResponseAsync originally collected ALL GLOBAL ScopedRoleAssignments
///   as roleNames and checked roleNames.Count == 0 to block CC-only users from the tenant
///   portal.  Because the above backfills gave these users at least one GLOBAL assignment
///   (TenantAdmin or CARECONNECT_REFERRER), the guard never fired.
///
/// Fix:
///   1. AuthService now filters roleNames to system roles only (Role.Scope IN Platform, Tenant),
///      so stray product-role GLOBAL assignments no longer bypass the portal guard.
///
///   2. This migration deactivates ALL auto-backfilled GLOBAL ScopedRoleAssignments
///      (AssignedByUserId IS NULL) for users whose primary org is PROVIDER or LAW_FIRM,
///      except those where Role.Scope is 'Platform' or 'Tenant' that were intentionally
///      assigned.  Manually granted assignments (AssignedByUserId IS NOT NULL) are
///      always left untouched.
///
/// Safe to re-run: UPDATE only matches IsActive = 1 rows; subsequent runs are noops.
/// </summary>
[DbContext(typeof(IdentityDbContext))]
[Migration("20260530000001_RemoveTenantAdminFromCcOnlyUsers")]
public partial class RemoveTenantAdminFromCcOnlyUsers : Migration
{
    /// <summary>
    /// The corrective UPDATE SQL for this migration.
    /// Exposed as a constant so the Program.cs startup guard can reference the same
    /// statement without duplicating it — keeping the migration and the guard in sync.
    /// </summary>
    public static readonly string UpSql = @"
UPDATE `idt_ScopedRoleAssignments` sra
INNER JOIN `idt_Roles` r
       ON  r.`Id`       = sra.`RoleId`
INNER JOIN `idt_Users` u
       ON  u.`Id`       = sra.`UserId`
INNER JOIN `idt_UserOrganizationMemberships` uom
       ON  uom.`UserId`    = u.`Id`
      AND  uom.`IsPrimary` = 1
      AND  uom.`IsActive`  = 1
INNER JOIN `idt_Organizations` o
       ON  o.`Id`      = uom.`OrganizationId`
      AND  o.`OrgType` IN ('PROVIDER', 'LAW_FIRM')
SET  sra.`IsActive`     = 0,
     sra.`UpdatedAtUtc` = UTC_TIMESTAMP()
WHERE sra.`ScopeType`        = 'GLOBAL'
  AND sra.`IsActive`         = 1
  AND sra.`AssignedByUserId` IS NULL
  AND (r.`Scope` NOT IN ('Platform', 'Tenant') OR r.`Scope` IS NULL);";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Deactivate all auto-backfilled GLOBAL ScopedRoleAssignments for PROVIDER/LAW_FIRM
        // primary-org users where the role is NOT a system role (Platform/Tenant scope).
        // This covers both the TenantAdmin over-assignment and stray product-role promotions
        // (e.g. CARECONNECT_REFERRER) left by the phase-G UserRoles backfill.
        migrationBuilder.Sql(UpSql);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // WARNING: This restores the pre-fix state, which re-opens the LS-ID-CC-001
        // vulnerability (CC-only PROVIDER/LAW_FIRM users can bypass the tenant portal guard).
        // Only run Down() in dev/CI rollback scenarios; never execute against production
        // without explicit review and a follow-up remediation plan.
        //
        // Additionally: rows that were already IsActive=0 before Up() ran for unrelated
        // reasons (e.g. a product role deactivated separately) will be incorrectly
        // reactivated, because there is no tombstone distinguishing "deactivated by this
        // migration" from "already inactive".  This is a known limitation of the
        // soft-delete + corrective-migration pattern.
        migrationBuilder.Sql(@"
UPDATE `idt_ScopedRoleAssignments` sra
INNER JOIN `idt_Roles` r
       ON  r.`Id`       = sra.`RoleId`
INNER JOIN `idt_Users` u
       ON  u.`Id`       = sra.`UserId`
INNER JOIN `idt_UserOrganizationMemberships` uom
       ON  uom.`UserId`    = u.`Id`
      AND  uom.`IsPrimary` = 1
      AND  uom.`IsActive`  = 1
INNER JOIN `idt_Organizations` o
       ON  o.`Id`      = uom.`OrganizationId`
      AND  o.`OrgType` IN ('PROVIDER', 'LAW_FIRM')
SET  sra.`IsActive`     = 1,
     sra.`UpdatedAtUtc` = UTC_TIMESTAMP()
WHERE sra.`ScopeType`        = 'GLOBAL'
  AND sra.`IsActive`         = 0
  AND sra.`AssignedByUserId` IS NULL
  AND (r.`Scope` NOT IN ('Platform', 'Tenant') OR r.`Scope` IS NULL);");
    }
}
