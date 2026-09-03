using Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations;

[DbContext(typeof(IdentityDbContext))]
[Migration("20260903020000_AddSynqLienUserManagement")]
public partial class AddSynqLienUserManagement : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>("Department", "idt_UserOrganizationMemberships", "varchar(150)", maxLength: 150, nullable: true);
        migrationBuilder.AddColumn<string>("JobTitle", "idt_UserOrganizationMemberships", "varchar(150)", maxLength: 150, nullable: true);
        migrationBuilder.AddColumn<Guid>("OrganizationId", "idt_UserInvitations", "char(36)", nullable: true, collation: "ascii_general_ci");
        migrationBuilder.AddColumn<string>("ProductCode", "idt_UserInvitations", "varchar(50)", maxLength: 50, nullable: true)
            .Annotation("MySql:CharSet", "utf8mb4");
        migrationBuilder.AddColumn<Guid>("PendingAccessRoleId", "idt_UserInvitations", "char(36)", nullable: true, collation: "ascii_general_ci");
        migrationBuilder.AddColumn<string>("PendingDepartment", "idt_UserInvitations", "varchar(150)", maxLength: 150, nullable: true)
            .Annotation("MySql:CharSet", "utf8mb4");
        migrationBuilder.AddColumn<string>("PendingJobTitle", "idt_UserInvitations", "varchar(150)", maxLength: 150, nullable: true)
            .Annotation("MySql:CharSet", "utf8mb4");
        migrationBuilder.AddColumn<bool>("RequiresAccountActivation", "idt_UserInvitations", "tinyint(1)", nullable: false, defaultValue: true);
        migrationBuilder.AddColumn<Guid>(
            "OrganizationScopeId", "idt_UserProductAccess", "char(36)", nullable: false,
            computedColumnSql: "COALESCE(`OrganizationId`, '00000000-0000-0000-0000-000000000000')", stored: true,
            collation: "ascii_general_ci");

        migrationBuilder.CreateIndex("IX_idt_UserOrganizationMemberships_OrganizationId_Department", "idt_UserOrganizationMemberships", new[] { "OrganizationId", "Department" });
        migrationBuilder.CreateIndex("IX_UserInvitations_SynqLienScopeUserStatus", "idt_UserInvitations", new[] { "TenantId", "OrganizationId", "ProductCode", "UserId", "Status" });

        migrationBuilder.DropIndex("IX_UserProductAccess_TenantId_UserId_ProductCode", "idt_UserProductAccess");
        migrationBuilder.DropIndex("IX_UserRoleAssignments_TenantId_UserId_RoleCode", "idt_UserRoleAssignments");

        // Legacy SynqLien grants were tenant-scoped. Materialize one exact grant per
        // active LAW_FIRM membership, then remove the ambiguous NULL-scoped record.
        migrationBuilder.Sql("""
            INSERT INTO `idt_UserProductAccess`
                (`Id`,`TenantId`,`UserId`,`ProductCode`,`AccessStatus`,`OrganizationId`,`SourceType`,`GrantedAtUtc`,`RevokedAtUtc`,`CreatedAtUtc`,`UpdatedAtUtc`,`CreatedByUserId`,`UpdatedByUserId`)
            SELECT UUID(), a.`TenantId`, a.`UserId`, a.`ProductCode`, a.`AccessStatus`, m.`OrganizationId`,
                   a.`SourceType`, a.`GrantedAtUtc`, a.`RevokedAtUtc`, a.`CreatedAtUtc`, a.`UpdatedAtUtc`, a.`CreatedByUserId`, a.`UpdatedByUserId`
            FROM `idt_UserProductAccess` a
            INNER JOIN `idt_UserOrganizationMemberships` m ON m.`UserId` = a.`UserId` AND m.`IsActive` = 1
            INNER JOIN `idt_Organizations` o ON o.`Id` = m.`OrganizationId` AND o.`TenantId` = a.`TenantId`
            WHERE a.`ProductCode` = 'SYNQ_LIENS' AND a.`OrganizationId` IS NULL
              AND a.`AccessStatus` = 'Granted' AND o.`IsActive` = 1;

            DELETE FROM `idt_UserProductAccess`
            WHERE `ProductCode` = 'SYNQ_LIENS' AND `OrganizationId` IS NULL;

            INSERT INTO `idt_UserRoleAssignments`
                (`Id`,`TenantId`,`UserId`,`ProductCode`,`RoleCode`,`AssignmentStatus`,`OrganizationId`,`SourceType`,`AssignedAtUtc`,`RemovedAtUtc`,`CreatedAtUtc`,`UpdatedAtUtc`,`CreatedByUserId`,`UpdatedByUserId`)
            SELECT UUID(), a.`TenantId`, a.`UserId`, a.`ProductCode`, a.`RoleCode`, a.`AssignmentStatus`, m.`OrganizationId`,
                   a.`SourceType`, a.`AssignedAtUtc`, a.`RemovedAtUtc`, a.`CreatedAtUtc`, a.`UpdatedAtUtc`, a.`CreatedByUserId`, a.`UpdatedByUserId`
            FROM `idt_UserRoleAssignments` a
            INNER JOIN `idt_UserOrganizationMemberships` m ON m.`UserId` = a.`UserId` AND m.`IsActive` = 1
            INNER JOIN `idt_Organizations` o ON o.`Id` = m.`OrganizationId` AND o.`TenantId` = a.`TenantId`
            WHERE a.`ProductCode` = 'SYNQ_LIENS' AND a.`OrganizationId` IS NULL
              AND a.`AssignmentStatus` = 'Active' AND o.`IsActive` = 1;

            DELETE FROM `idt_UserRoleAssignments`
            WHERE `ProductCode` = 'SYNQ_LIENS' AND `OrganizationId` IS NULL;
            """);

        migrationBuilder.CreateIndex("IX_UserProductAccess_TenantId_OrganizationId_UserId_ProductCode", "idt_UserProductAccess", new[] { "TenantId", "OrganizationScopeId", "UserId", "ProductCode" }, unique: true);
        migrationBuilder.CreateIndex("IX_UserRoleAssignments_TenantId_OrganizationId_UserId_RoleCode", "idt_UserRoleAssignments", new[] { "TenantId", "OrganizationId", "UserId", "RoleCode" });

        migrationBuilder.Sql("""
            CREATE TABLE `idt_SynqLienAccessRoles` (
                `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
                `TenantId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
                `OrganizationId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
                `Name` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
                `ActiveName` varchar(100) CHARACTER SET utf8mb4 GENERATED ALWAYS AS (CASE WHEN `IsActive` = 1 THEN LOWER(`Name`) ELSE NULL END) STORED,
                `Description` varchar(500) CHARACTER SET utf8mb4 NULL,
                `IsSystem` tinyint(1) NOT NULL,
                `IsActive` tinyint(1) NOT NULL,
                `CreatedAtUtc` datetime(6) NOT NULL,
                `UpdatedAtUtc` datetime(6) NOT NULL,
                `CreatedByUserId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NULL,
                `UpdatedByUserId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NULL,
                CONSTRAINT `PK_idt_SynqLienAccessRoles` PRIMARY KEY (`Id`),
                UNIQUE KEY `IX_SynqLienAccessRoles_Tenant_Organization_ActiveName` (`TenantId`,`OrganizationId`,`ActiveName`)
            ) CHARACTER SET=utf8mb4;

            CREATE TABLE `idt_SynqLienAccessRolePermissions` (
                `RoleId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
                `PermissionId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
                CONSTRAINT `PK_idt_SynqLienAccessRolePermissions` PRIMARY KEY (`RoleId`,`PermissionId`),
                CONSTRAINT `FK_SynqLienRolePermissions_Role` FOREIGN KEY (`RoleId`) REFERENCES `idt_SynqLienAccessRoles` (`Id`) ON DELETE CASCADE,
                CONSTRAINT `FK_SynqLienRolePermissions_Permission` FOREIGN KEY (`PermissionId`) REFERENCES `idt_Capabilities` (`Id`) ON DELETE RESTRICT
            ) CHARACTER SET=utf8mb4;

            CREATE TABLE `idt_SynqLienUserAccessRoleAssignments` (
                `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
                `TenantId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
                `OrganizationId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
                `UserId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
                `RoleId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
                `IsActive` tinyint(1) NOT NULL,
                `ActiveSlot` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NULL,
                `AssignedAtUtc` datetime(6) NOT NULL,
                `RemovedAtUtc` datetime(6) NULL,
                `AssignedByUserId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NULL,
                `RemovedByUserId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NULL,
                CONSTRAINT `PK_idt_SynqLienUserAccessRoleAssignments` PRIMARY KEY (`Id`),
                CONSTRAINT `FK_SynqLienUserAccessRoleAssignments_Role` FOREIGN KEY (`RoleId`) REFERENCES `idt_SynqLienAccessRoles` (`Id`) ON DELETE RESTRICT,
                CONSTRAINT `FK_SynqLienUserAccessRoleAssignments_User` FOREIGN KEY (`UserId`) REFERENCES `idt_Users` (`Id`) ON DELETE CASCADE,
                KEY `IX_SynqLienUserAccessRoleAssignments_Scope` (`TenantId`,`OrganizationId`,`UserId`,`RoleId`),
                UNIQUE KEY `IX_SynqLienUserAccessRoleAssignments_Active` (`TenantId`,`OrganizationId`,`UserId`,`ActiveSlot`)
            ) CHARACTER SET=utf8mb4;
            """);

        migrationBuilder.Sql("""
            INSERT IGNORE INTO `idt_Capabilities`
                (`Id`,`ProductId`,`Code`,`Name`,`Description`,`Category`,`IsActive`,`CreatedAtUtc`,`UpdatedAtUtc`,`CreatedBy`,`UpdatedBy`)
            VALUES
                ('6b000000-0000-0000-0000-000000000001','10000000-0000-0000-0000-000000000002','SYNQ_LIENS.users:view','View Users','View SynqLien users in the current organization','User Management',1,UTC_TIMESTAMP(6),NULL,NULL,NULL),
                ('6b000000-0000-0000-0000-000000000002','10000000-0000-0000-0000-000000000002','SYNQ_LIENS.users:manage','Manage Users','Manage SynqLien access in the current organization','User Management',1,UTC_TIMESTAMP(6),NULL,NULL,NULL),
                ('6b000000-0000-0000-0000-000000000003','10000000-0000-0000-0000-000000000002','SYNQ_LIENS.invitations:manage','Manage Invitations','Invite users and manage pending SynqLien invitations','User Management',1,UTC_TIMESTAMP(6),NULL,NULL,NULL),
                ('6b000000-0000-0000-0000-000000000004','10000000-0000-0000-0000-000000000002','SYNQ_LIENS.roles:view','View Roles','View organization SynqLien access roles','User Management',1,UTC_TIMESTAMP(6),NULL,NULL,NULL),
                ('6b000000-0000-0000-0000-000000000005','10000000-0000-0000-0000-000000000002','SYNQ_LIENS.roles:manage','Manage Roles','Create, edit, and retire organization SynqLien access roles','User Management',1,UTC_TIMESTAMP(6),NULL,NULL,NULL);

            INSERT IGNORE INTO `idt_Capabilities`
                (`Id`,`ProductId`,`Code`,`Name`,`Description`,`Category`,`IsActive`,`CreatedAtUtc`,`UpdatedAtUtc`,`CreatedBy`,`UpdatedBy`)
            VALUES
                ('6b000000-0000-0000-0000-000000000101','10000000-0000-0000-0000-000000000002','SYNQ_LIENS.lien:read','Read Liens','View liens in the current organization','Lien',1,UTC_TIMESTAMP(6),NULL,NULL,NULL),
                ('6b000000-0000-0000-0000-000000000102','10000000-0000-0000-0000-000000000002','SYNQ_LIENS.lien:update','Update Liens','Update liens in the current organization','Lien',1,UTC_TIMESTAMP(6),NULL,NULL,NULL),
                ('6b000000-0000-0000-0000-000000000103','10000000-0000-0000-0000-000000000002','SYNQ_LIENS.workflow:manage','Manage Workflows','Manage SynqLien workflows','Workflow',1,UTC_TIMESTAMP(6),NULL,NULL,NULL),
                ('6b000000-0000-0000-0000-000000000104','10000000-0000-0000-0000-000000000002','SYNQ_LIENS.task_template:manage','Manage Task Templates','Manage SynqLien task templates','Task',1,UTC_TIMESTAMP(6),NULL,NULL,NULL),
                ('6b000000-0000-0000-0000-000000000105','10000000-0000-0000-0000-000000000002','SYNQ_LIENS.task_automation:manage','Manage Task Automations','Manage SynqLien task automations','Task',1,UTC_TIMESTAMP(6),NULL,NULL,NULL),
                ('6b000000-0000-0000-0000-000000000106','10000000-0000-0000-0000-000000000002','SYNQ_LIENS.task_note:manage','Manage Task Notes','Manage SynqLien task notes','Task',1,UTC_TIMESTAMP(6),NULL,NULL,NULL),
                ('6b000000-0000-0000-0000-000000000107','10000000-0000-0000-0000-000000000002','SYNQ_LIENS.case_note:manage','Manage Case Notes','Manage SynqLien case notes','Case',1,UTC_TIMESTAMP(6),NULL,NULL,NULL);

            INSERT INTO `idt_SynqLienAccessRoles`
                (`Id`,`TenantId`,`OrganizationId`,`Name`,`Description`,`IsSystem`,`IsActive`,`CreatedAtUtc`,`UpdatedAtUtc`,`CreatedByUserId`,`UpdatedByUserId`)
            SELECT UUID(), o.`TenantId`, o.`Id`, starter.`Name`, starter.`Description`, 1, 1, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6), o.`OwnerUserId`, o.`OwnerUserId`
            FROM `idt_Organizations` o
            CROSS JOIN (
                SELECT 'Administrator' AS `Name`, 'Full SynqLien access and organization user management' AS `Description`
                UNION ALL SELECT 'Quality Assurance', 'Operational review and quality-control access'
                UNION ALL SELECT 'View Only', 'Read-only SynqLien access'
            ) starter
            WHERE o.`IsActive` = 1 AND o.`OrgType` = 'LAW_FIRM'
              AND NOT EXISTS (SELECT 1 FROM `idt_SynqLienAccessRoles` r WHERE r.`TenantId` = o.`TenantId` AND r.`OrganizationId` = o.`Id` AND r.`Name` = starter.`Name`);

            INSERT IGNORE INTO `idt_SynqLienAccessRolePermissions` (`RoleId`,`PermissionId`)
            SELECT r.`Id`, p.`Id` FROM `idt_SynqLienAccessRoles` r
            INNER JOIN `idt_Capabilities` p ON p.`ProductId` = '10000000-0000-0000-0000-000000000002' AND p.`IsActive` = 1
            WHERE r.`Name` = 'Administrator' AND r.`IsSystem` = 1;

            INSERT IGNORE INTO `idt_SynqLienAccessRolePermissions` (`RoleId`,`PermissionId`)
            SELECT r.`Id`, p.`Id` FROM `idt_SynqLienAccessRoles` r
            INNER JOIN `idt_Capabilities` p ON p.`Code` IN (
                'SYNQ_LIENS.users:view','SYNQ_LIENS.roles:view','SYNQ_LIENS.case:read','SYNQ_LIENS.case:update',
                'SYNQ_LIENS.lien:read','SYNQ_LIENS.lien:update','SYNQ_LIENS.task:read',
                'SYNQ_LIENS.task_note:manage','SYNQ_LIENS.case_note:manage','SYNQ_LIENS.lien_sale:read','SYNQ_LIENS.lien_sale:view_analytics')
            WHERE r.`Name` = 'Quality Assurance' AND r.`IsSystem` = 1;

            INSERT IGNORE INTO `idt_SynqLienAccessRolePermissions` (`RoleId`,`PermissionId`)
            SELECT r.`Id`, p.`Id` FROM `idt_SynqLienAccessRoles` r
            INNER JOIN `idt_Capabilities` p ON p.`Code` IN (
                'SYNQ_LIENS.users:view','SYNQ_LIENS.roles:view','SYNQ_LIENS.case:read','SYNQ_LIENS.lien:read',
                'SYNQ_LIENS.task:read','SYNQ_LIENS.lien_sale:read')
            WHERE r.`Name` = 'View Only' AND r.`IsSystem` = 1;

            INSERT INTO `idt_SynqLienUserAccessRoleAssignments`
                (`Id`,`TenantId`,`OrganizationId`,`UserId`,`RoleId`,`IsActive`,`ActiveSlot`,`AssignedAtUtc`,`RemovedAtUtc`,`AssignedByUserId`,`RemovedByUserId`)
            SELECT UUID(), r.`TenantId`, r.`OrganizationId`, o.`OwnerUserId`, r.`Id`, 1, '00000000-0000-0000-0000-000000000000', UTC_TIMESTAMP(6), NULL, o.`OwnerUserId`, NULL
            FROM `idt_SynqLienAccessRoles` r
            INNER JOIN `idt_Organizations` o ON o.`Id` = r.`OrganizationId`
            WHERE r.`Name` = 'Administrator' AND r.`IsSystem` = 1 AND o.`OwnerUserId` IS NOT NULL
              AND NOT EXISTS (SELECT 1 FROM `idt_SynqLienUserAccessRoleAssignments` a WHERE a.`TenantId` = r.`TenantId` AND a.`OrganizationId` = r.`OrganizationId` AND a.`UserId` = o.`OwnerUserId` AND a.`RoleId` = r.`Id` AND a.`IsActive` = 1);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TABLE IF EXISTS `idt_SynqLienAccessRolePermissions`; DROP TABLE IF EXISTS `idt_SynqLienUserAccessRoleAssignments`; DROP TABLE IF EXISTS `idt_SynqLienAccessRoles`;");
        migrationBuilder.DropIndex("IX_UserProductAccess_TenantId_OrganizationId_UserId_ProductCode", "idt_UserProductAccess");
        migrationBuilder.Sql("""
            DELETE a FROM `idt_UserProductAccess` a
            INNER JOIN `idt_UserProductAccess` b
                ON a.`TenantId` = b.`TenantId` AND a.`UserId` = b.`UserId` AND a.`ProductCode` = b.`ProductCode` AND a.`Id` > b.`Id`;
            UPDATE `idt_UserProductAccess` SET `OrganizationId` = NULL;
            UPDATE `idt_UserRoleAssignments` SET `OrganizationId` = NULL;
            """);
        migrationBuilder.CreateIndex("IX_UserProductAccess_TenantId_UserId_ProductCode", "idt_UserProductAccess", new[] { "TenantId", "UserId", "ProductCode" }, unique: true);
        migrationBuilder.DropColumn("OrganizationScopeId", "idt_UserProductAccess");
        migrationBuilder.DropIndex("IX_UserRoleAssignments_TenantId_OrganizationId_UserId_RoleCode", "idt_UserRoleAssignments");
        migrationBuilder.CreateIndex("IX_UserRoleAssignments_TenantId_UserId_RoleCode", "idt_UserRoleAssignments", new[] { "TenantId", "UserId", "RoleCode" });
        migrationBuilder.DropIndex("IX_idt_UserOrganizationMemberships_OrganizationId_Department", "idt_UserOrganizationMemberships");
        migrationBuilder.DropIndex("IX_UserInvitations_SynqLienScopeUserStatus", "idt_UserInvitations");
        migrationBuilder.DropColumn("Department", "idt_UserOrganizationMemberships");
        migrationBuilder.DropColumn("JobTitle", "idt_UserOrganizationMemberships");
        migrationBuilder.DropColumn("OrganizationId", "idt_UserInvitations");
        migrationBuilder.DropColumn("ProductCode", "idt_UserInvitations");
        migrationBuilder.DropColumn("PendingAccessRoleId", "idt_UserInvitations");
        migrationBuilder.DropColumn("PendingDepartment", "idt_UserInvitations");
        migrationBuilder.DropColumn("PendingJobTitle", "idt_UserInvitations");
        migrationBuilder.DropColumn("RequiresAccountActivation", "idt_UserInvitations");
    }
}
