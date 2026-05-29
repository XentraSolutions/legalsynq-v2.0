using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations;

/// <summary>
/// Seeds the Support module's role catalog into idt_Roles AND back-fills
/// ScopedRoleAssignments for ALL active users who have no active GLOBAL assignment.
///
/// Context:
///   Support.Api defines five role strings in SupportRoles.cs that are referenced
///   by its authorization policies (SupportRead, SupportWrite, SupportManage,
///   SupportInternal, CustomerAccess). None of these roles were seeded in the
///   Identity DB, making it impossible for users to ever be assigned them via
///   UserMembershipService.AssignRolesAsync (which looks up roles by name).
///
///   Without these seeds, AssignRolesAsync silently logs "role not found, skipping"
///   and creates no ScopedRoleAssignment — leaving every user with roles=[] in
///   their JWT and a guaranteed 403 on all support endpoints.
///
///   The back-fill now covers EVERY active user who has no active GLOBAL
///   ScopedRoleAssignment (v1 only covered the earliest user per tenant).
///   It assigns the correct role based on the user's tenant:
///     • Platform/system tenant (20000000-…-000001) → PlatformAdmin
///     • All other tenants                          → TenantAdmin
///
/// Roles seeded:
///   SupportAdmin    — platform support administration (manages queues, settings)
///   SupportManager  — support team manager (manages agents and escalations)
///   SupportAgent    — frontline support agent (handles tickets)
///   TenantUser      — regular authenticated tenant user (replaces StandardUser alias)
///   ExternalCustomer — external customer submitting/viewing their own tickets
///
/// Safe to re-run: all INSERTs use INSERT IGNORE / NOT EXISTS guards.
/// </summary>
[Migration("20260426000001_SeedSupportRoles")]
public partial class SeedSupportRoles : Migration
{
    // Platform / system tenant — all system roles are owned by this tenant.
    private const string PlatformTenantId = "20000000-0000-0000-0000-000000000001";

    // Existing role IDs (seeded in the initial migration).
    private const string RolePlatformAdmin = "30000000-0000-0000-0000-000000000001";
    private const string RoleTenantAdmin   = "30000000-0000-0000-0000-000000000002";

    // New role IDs — continuing the 30000000-0000-0000-0000-00000000001X series.
    private const string RoleSupportAdmin     = "30000000-0000-0000-0000-000000000011";
    private const string RoleSupportManager   = "30000000-0000-0000-0000-000000000012";
    private const string RoleSupportAgent     = "30000000-0000-0000-0000-000000000013";
    private const string RoleTenantUser       = "30000000-0000-0000-0000-000000000014";
    private const string RoleExternalCustomer = "30000000-0000-0000-0000-000000000015";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ── Step 1: Seed missing support roles ───────────────────────────────────
        migrationBuilder.Sql($@"
INSERT IGNORE INTO `idt_Roles`
    (`Id`, `TenantId`, `Name`, `Description`, `IsSystemRole`, `Scope`, `CreatedAtUtc`, `UpdatedAtUtc`)
VALUES
    ('{RoleSupportAdmin}',    '{PlatformTenantId}', 'SupportAdmin',     'Full support administration — manages queues, SLAs and settings',         1, 'Support',  '2024-01-01 00:00:00', '2024-01-01 00:00:00'),
    ('{RoleSupportManager}',  '{PlatformTenantId}', 'SupportManager',   'Support team manager — escalations, reporting and agent oversight',        1, 'Support',  '2024-01-01 00:00:00', '2024-01-01 00:00:00'),
    ('{RoleSupportAgent}',    '{PlatformTenantId}', 'SupportAgent',     'Frontline support agent — handles and responds to tickets',                1, 'Support',  '2024-01-01 00:00:00', '2024-01-01 00:00:00'),
    ('{RoleTenantUser}',      '{PlatformTenantId}', 'TenantUser',       'Regular authenticated tenant user — read-only support access',            1, 'Tenant',   '2024-01-01 00:00:00', '2024-01-01 00:00:00'),
    ('{RoleExternalCustomer}','{PlatformTenantId}', 'ExternalCustomer', 'External customer — may view and comment on their own support tickets',   0, 'External', '2024-01-01 00:00:00', '2024-01-01 00:00:00');
");

        // ── Step 2: Back-fill ScopedRoleAssignments for ALL users without one ─────
        //
        // Covers every active user who has NO active GLOBAL ScopedRoleAssignment.
        // The correct role is determined by tenant membership:
        //   • Users in the platform/system tenant → PlatformAdmin
        //   • Users in any other tenant           → TenantAdmin
        //
        // Idempotent: INSERT IGNORE + NOT EXISTS guard prevents duplicates.
        migrationBuilder.Sql($@"
INSERT IGNORE INTO `idt_ScopedRoleAssignments`
    (`Id`, `UserId`, `RoleId`, `ScopeType`, `TenantId`,
     `OrganizationId`, `OrganizationRelationshipId`, `ProductId`,
     `IsActive`, `AssignedAtUtc`, `UpdatedAtUtc`, `AssignedByUserId`)
SELECT
    UUID(),
    u.`Id`   AS UserId,
    CASE
        WHEN u.`TenantId` = '{PlatformTenantId}' THEN '{RolePlatformAdmin}'
        ELSE '{RoleTenantAdmin}'
    END      AS RoleId,
    'GLOBAL' AS ScopeType,
    u.`TenantId`,
    NULL, NULL, NULL,
    1        AS IsActive,
    u.`CreatedAtUtc` AS AssignedAtUtc,
    u.`CreatedAtUtc` AS UpdatedAtUtc,
    NULL     AS AssignedByUserId
FROM `idt_Users` u
WHERE u.`IsActive` = 1
  AND NOT EXISTS (
    SELECT 1
    FROM   `idt_ScopedRoleAssignments` sra
    WHERE  sra.`UserId`    = u.`Id`
      AND  sra.`ScopeType` = 'GLOBAL'
      AND  sra.`IsActive`  = 1
  )
  -- Exclude CareConnect-only primary org users (PROVIDER = receiver, LAW_FIRM = referrer).
  -- These users are gated to the CC common portal only; assigning a system role here
  -- would incorrectly allow them to log into the tenant portal.
  AND NOT EXISTS (
    SELECT 1
    FROM   `idt_UserOrganizationMemberships` uom
    JOIN   `idt_Organizations` o ON o.`Id` = uom.`OrganizationId`
    WHERE  uom.`UserId`    = u.`Id`
      AND  uom.`IsPrimary` = 1
      AND  uom.`IsActive`  = 1
      AND  o.`OrgType`     IN ('PROVIDER', 'LAW_FIRM')
  );
");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql($@"
DELETE FROM `idt_Roles`
WHERE `Id` IN (
    '{RoleSupportAdmin}',
    '{RoleSupportManager}',
    '{RoleSupportAgent}',
    '{RoleTenantUser}',
    '{RoleExternalCustomer}'
);
");
    }
}
