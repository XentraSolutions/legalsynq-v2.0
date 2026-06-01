using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BackfillTenantOwnerUserId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Backfill OwnerUserId for all existing tenants.
            //
            // Strategy: for each tenant, pick the user who holds the TenantAdmin role
            // with the earliest AssignedAtUtc. This matches the convention established
            // for all tenants created going forward — CreateTenant wires SetOwner on
            // the admin user created alongside the tenant.
            //
            // Tenants that have no active TenantAdmin scoped-role assignment are left
            // as NULL (e.g. the LegalSynq Internal seed tenant). This is safe: the
            // enrollment owner-guard only fires when OwnerUserId IS NOT NULL.
            //
            // The ORDER BY + INNER JOIN pattern picks the single earliest assignment
            // per tenant without requiring a subquery GROUP BY, which is valid in MySQL 8+.
            // If two users share the exact same AssignedAtUtc, MySQL picks one
            // deterministically (by primary key order); this edge case is acceptable.
            migrationBuilder.Sql(@"
UPDATE `idt_Tenants` t
INNER JOIN (
    SELECT sra.`TenantId`, sra.`UserId`
    FROM `ScopedRoleAssignments` sra
    INNER JOIN `Roles` r ON r.`Id` = sra.`RoleId`
    WHERE r.`Name`         = 'TenantAdmin'
      AND sra.`IsActive`   = 1
      AND sra.`TenantId`   IS NOT NULL
) first_admin ON first_admin.`TenantId` = t.`Id`
INNER JOIN (
    SELECT sra2.`TenantId`, MIN(sra2.`AssignedAtUtc`) AS `EarliestAt`
    FROM `ScopedRoleAssignments` sra2
    INNER JOIN `Roles` r2 ON r2.`Id` = sra2.`RoleId`
    WHERE r2.`Name`       = 'TenantAdmin'
      AND sra2.`IsActive` = 1
      AND sra2.`TenantId` IS NOT NULL
    GROUP BY sra2.`TenantId`
) earliest ON earliest.`TenantId` = first_admin.`TenantId`
INNER JOIN `ScopedRoleAssignments` sra3
       ON  sra3.`TenantId`      = earliest.`TenantId`
       AND sra3.`AssignedAtUtc` = earliest.`EarliestAt`
INNER JOIN `Roles` r3 ON r3.`Id` = sra3.`RoleId` AND r3.`Name` = 'TenantAdmin'
SET t.`OwnerUserId` = sra3.`UserId`
WHERE t.`OwnerUserId` IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Clear backfilled values — only clears rows that were set by this migration
            // (tenants that had a TenantAdmin role assignment). Manually-set values are
            // not distinguishable, so Down wipes all OwnerUserId values for safety.
            migrationBuilder.Sql(@"
UPDATE `idt_Tenants`
SET `OwnerUserId` = NULL
WHERE `OwnerUserId` IS NOT NULL;");
        }
    }
}
