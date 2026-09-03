-- Manual installer for Identity migration:
--   20260903020000_AddSynqLienUserManagement
--
-- Run this against the Identity database while Identity and Liens are stopped.
-- The schema, backfill, and seed operations are idempotent. The EF migration
-- history row is inserted only after the live schema/data contract and the
-- immediately preceding Identity migration have been verified. If the small
-- 20260824124500 capability migration is missing, this script applies and
-- validates it first, provided its own predecessor is already recorded.

-- Apply the missing immediate predecessor atomically. The migration-history row
-- is the exactly-once condition for invalidating access tokens: an interrupted
-- attempt rolls back both the capability mappings and the AccessVersion bump.
DELIMITER //
DROP PROCEDURE IF EXISTS `apply_synqlien_user_management_predecessor`//
CREATE PROCEDURE `apply_synqlien_user_management_predecessor`()
BEGIN
    DECLARE predecessor_exists int DEFAULT 0;
    DECLARE capability_count int DEFAULT 0;
    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    START TRANSACTION;

    SELECT COUNT(*) INTO predecessor_exists
    FROM `__EFMigrationsHistory`
    WHERE `MigrationId` = '20260824124500_AddCareConnectReferrerAdminReferralCapabilities'
    FOR UPDATE;

    IF predecessor_exists = 0 THEN
        IF NOT EXISTS (
            SELECT 1
            FROM `__EFMigrationsHistory`
            WHERE `MigrationId` = '20260824120000_MigrateCareConnectLawFirmReferrerToAdmin'
        ) THEN
            SIGNAL SQLSTATE '45000'
                SET MESSAGE_TEXT = 'Missing prerequisite migration 20260824120000_MigrateCareConnectLawFirmReferrerToAdmin';
        END IF;

        INSERT IGNORE INTO `idt_RoleCapabilities` (`ProductRoleId`, `CapabilityId`)
        SELECT '50000000-0000-0000-0000-000000000012', required.`CapabilityId`
        FROM (
            SELECT '60000000-0000-0000-0000-000000000001' AS `CapabilityId`
            UNION ALL SELECT '60000000-0000-0000-0000-000000000002'
            UNION ALL SELECT '60000000-0000-0000-0000-000000000003'
            UNION ALL SELECT '60000000-0000-0000-0000-000000000011'
        ) AS required;

        SELECT COUNT(*) INTO capability_count
        FROM `idt_RoleCapabilities`
        WHERE `ProductRoleId` = '50000000-0000-0000-0000-000000000012'
          AND `CapabilityId` IN (
              '60000000-0000-0000-0000-000000000001',
              '60000000-0000-0000-0000-000000000002',
              '60000000-0000-0000-0000-000000000003',
              '60000000-0000-0000-0000-000000000011'
          );

        IF capability_count <> 4 THEN
            SIGNAL SQLSTATE '45000'
                SET MESSAGE_TEXT = 'Unable to apply prerequisite migration 20260824124500_AddCareConnectReferrerAdminReferralCapabilities';
        END IF;

        UPDATE `idt_Users` AS user
        SET user.`AccessVersion` = user.`AccessVersion` + 1,
            user.`UpdatedAtUtc` = UTC_TIMESTAMP(6)
        WHERE EXISTS (
            SELECT 1
            FROM `idt_UserRoleAssignments` AS assignment
            WHERE assignment.`UserId` = user.`Id`
              AND assignment.`ProductCode` = 'SYNQ_CARECONNECT'
              AND assignment.`RoleCode` = 'CARECONNECT_REFERRER_ADMIN'
              AND assignment.`AssignmentStatus` = 'Active'
        );

        INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
        VALUES ('20260824124500_AddCareConnectReferrerAdminReferralCapabilities', '8.0.0');
    END IF;

    COMMIT;
END//
CALL `apply_synqlien_user_management_predecessor`()//
DROP PROCEDURE `apply_synqlien_user_management_predecessor`//
DELIMITER ;

-- Add organization profile fields.
SET @migration_sql = IF(
    EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = DATABASE()
          AND table_name = 'idt_UserOrganizationMemberships'
          AND column_name = 'Department'
    ),
    'SELECT 1',
    'ALTER TABLE `idt_UserOrganizationMemberships` ADD COLUMN `Department` varchar(150) CHARACTER SET utf8mb4 NULL'
);
PREPARE migration_statement FROM @migration_sql;
EXECUTE migration_statement;
DEALLOCATE PREPARE migration_statement;

SET @migration_sql = IF(
    EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = DATABASE()
          AND table_name = 'idt_UserOrganizationMemberships'
          AND column_name = 'JobTitle'
    ),
    'SELECT 1',
    'ALTER TABLE `idt_UserOrganizationMemberships` ADD COLUMN `JobTitle` varchar(150) CHARACTER SET utf8mb4 NULL'
);
PREPARE migration_statement FROM @migration_sql;
EXECUTE migration_statement;
DEALLOCATE PREPARE migration_statement;

-- Add pending SynqLien grant fields to invitations.
SET @migration_sql = IF(
    EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = DATABASE()
          AND table_name = 'idt_UserInvitations'
          AND column_name = 'OrganizationId'
    ),
    'SELECT 1',
    'ALTER TABLE `idt_UserInvitations` ADD COLUMN `OrganizationId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NULL'
);
PREPARE migration_statement FROM @migration_sql;
EXECUTE migration_statement;
DEALLOCATE PREPARE migration_statement;

SET @migration_sql = IF(
    EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = DATABASE()
          AND table_name = 'idt_UserInvitations'
          AND column_name = 'ProductCode'
    ),
    'SELECT 1',
    'ALTER TABLE `idt_UserInvitations` ADD COLUMN `ProductCode` varchar(50) CHARACTER SET utf8mb4 NULL'
);
PREPARE migration_statement FROM @migration_sql;
EXECUTE migration_statement;
DEALLOCATE PREPARE migration_statement;

SET @migration_sql = IF(
    EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = DATABASE()
          AND table_name = 'idt_UserInvitations'
          AND column_name = 'PendingAccessRoleId'
    ),
    'SELECT 1',
    'ALTER TABLE `idt_UserInvitations` ADD COLUMN `PendingAccessRoleId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NULL'
);
PREPARE migration_statement FROM @migration_sql;
EXECUTE migration_statement;
DEALLOCATE PREPARE migration_statement;

SET @migration_sql = IF(
    EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = DATABASE()
          AND table_name = 'idt_UserInvitations'
          AND column_name = 'PendingDepartment'
    ),
    'SELECT 1',
    'ALTER TABLE `idt_UserInvitations` ADD COLUMN `PendingDepartment` varchar(150) CHARACTER SET utf8mb4 NULL'
);
PREPARE migration_statement FROM @migration_sql;
EXECUTE migration_statement;
DEALLOCATE PREPARE migration_statement;

SET @migration_sql = IF(
    EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = DATABASE()
          AND table_name = 'idt_UserInvitations'
          AND column_name = 'PendingJobTitle'
    ),
    'SELECT 1',
    'ALTER TABLE `idt_UserInvitations` ADD COLUMN `PendingJobTitle` varchar(150) CHARACTER SET utf8mb4 NULL'
);
PREPARE migration_statement FROM @migration_sql;
EXECUTE migration_statement;
DEALLOCATE PREPARE migration_statement;

SET @migration_sql = IF(
    EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = DATABASE()
          AND table_name = 'idt_UserInvitations'
          AND column_name = 'RequiresAccountActivation'
    ),
    'SELECT 1',
    'ALTER TABLE `idt_UserInvitations` ADD COLUMN `RequiresAccountActivation` tinyint(1) NOT NULL DEFAULT 1'
);
PREPARE migration_statement FROM @migration_sql;
EXECUTE migration_statement;
DEALLOCATE PREPARE migration_statement;

-- Normalize nullable organization scope so the unique product-access index
-- also enforces uniqueness for legacy tenant-scoped rows.
SET @migration_sql = IF(
    EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = DATABASE()
          AND table_name = 'idt_UserProductAccess'
          AND column_name = 'OrganizationScopeId'
    ),
    'SELECT 1',
    'ALTER TABLE `idt_UserProductAccess` ADD COLUMN `OrganizationScopeId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci GENERATED ALWAYS AS (COALESCE(`OrganizationId`, ''00000000-0000-0000-0000-000000000000'')) STORED'
);
PREPARE migration_statement FROM @migration_sql;
EXECUTE migration_statement;
DEALLOCATE PREPARE migration_statement;

-- Replace tenant-wide indexes with organization-aware indexes. Drop the old
-- unique product index before expanding legacy grants to multiple orgs.
SET @migration_sql = IF(
    EXISTS (
        SELECT 1 FROM information_schema.statistics
        WHERE table_schema = DATABASE()
          AND table_name = 'idt_UserProductAccess'
          AND index_name = 'IX_UserProductAccess_TenantId_UserId_ProductCode'
    ),
    'ALTER TABLE `idt_UserProductAccess` DROP INDEX `IX_UserProductAccess_TenantId_UserId_ProductCode`',
    'SELECT 1'
);
PREPARE migration_statement FROM @migration_sql;
EXECUTE migration_statement;
DEALLOCATE PREPARE migration_statement;

SET @migration_sql = IF(
    EXISTS (
        SELECT 1 FROM information_schema.statistics
        WHERE table_schema = DATABASE()
          AND table_name = 'idt_UserRoleAssignments'
          AND index_name = 'IX_UserRoleAssignments_TenantId_UserId_RoleCode'
    ),
    'ALTER TABLE `idt_UserRoleAssignments` DROP INDEX `IX_UserRoleAssignments_TenantId_UserId_RoleCode`',
    'SELECT 1'
);
PREPARE migration_statement FROM @migration_sql;
EXECUTE migration_statement;
DEALLOCATE PREPARE migration_statement;

-- Materialize one exact organization grant/persona for every active
-- organization membership before deleting ambiguous NULL-scoped SynqLien rows.
START TRANSACTION;

INSERT INTO `idt_UserProductAccess`
    (`Id`, `TenantId`, `UserId`, `ProductCode`, `AccessStatus`, `OrganizationId`,
     `SourceType`, `GrantedAtUtc`, `RevokedAtUtc`, `CreatedAtUtc`, `UpdatedAtUtc`,
     `CreatedByUserId`, `UpdatedByUserId`)
SELECT UUID(), access.`TenantId`, access.`UserId`, access.`ProductCode`, access.`AccessStatus`, membership.`OrganizationId`,
       access.`SourceType`, access.`GrantedAtUtc`, access.`RevokedAtUtc`, access.`CreatedAtUtc`, access.`UpdatedAtUtc`,
       access.`CreatedByUserId`, access.`UpdatedByUserId`
FROM `idt_UserProductAccess` AS access
INNER JOIN `idt_UserOrganizationMemberships` AS membership
    ON membership.`UserId` = access.`UserId`
   AND membership.`IsActive` = 1
INNER JOIN `idt_Organizations` AS organization
    ON organization.`Id` = membership.`OrganizationId`
   AND organization.`TenantId` = access.`TenantId`
WHERE access.`ProductCode` = 'SYNQ_LIENS'
  AND access.`OrganizationId` IS NULL
  AND access.`AccessStatus` = 'Granted'
  AND organization.`IsActive` = 1
  AND NOT EXISTS (
      SELECT 1
      FROM `idt_UserProductAccess` AS existing
      WHERE existing.`TenantId` = access.`TenantId`
        AND existing.`OrganizationId` = membership.`OrganizationId`
        AND existing.`UserId` = access.`UserId`
        AND existing.`ProductCode` = access.`ProductCode`
  );

DELETE FROM `idt_UserProductAccess`
WHERE `ProductCode` = 'SYNQ_LIENS'
  AND `OrganizationId` IS NULL;

INSERT INTO `idt_UserRoleAssignments`
    (`Id`, `TenantId`, `UserId`, `ProductCode`, `RoleCode`, `AssignmentStatus`, `OrganizationId`,
     `SourceType`, `AssignedAtUtc`, `RemovedAtUtc`, `CreatedAtUtc`, `UpdatedAtUtc`,
     `CreatedByUserId`, `UpdatedByUserId`)
SELECT UUID(), assignment.`TenantId`, assignment.`UserId`, assignment.`ProductCode`, assignment.`RoleCode`,
       assignment.`AssignmentStatus`, membership.`OrganizationId`, assignment.`SourceType`, assignment.`AssignedAtUtc`,
       assignment.`RemovedAtUtc`, assignment.`CreatedAtUtc`, assignment.`UpdatedAtUtc`,
       assignment.`CreatedByUserId`, assignment.`UpdatedByUserId`
FROM `idt_UserRoleAssignments` AS assignment
INNER JOIN `idt_UserOrganizationMemberships` AS membership
    ON membership.`UserId` = assignment.`UserId`
   AND membership.`IsActive` = 1
INNER JOIN `idt_Organizations` AS organization
    ON organization.`Id` = membership.`OrganizationId`
   AND organization.`TenantId` = assignment.`TenantId`
WHERE assignment.`ProductCode` = 'SYNQ_LIENS'
  AND assignment.`OrganizationId` IS NULL
  AND assignment.`AssignmentStatus` = 'Active'
  AND organization.`IsActive` = 1
  AND NOT EXISTS (
      SELECT 1
      FROM `idt_UserRoleAssignments` AS existing
      WHERE existing.`TenantId` = assignment.`TenantId`
        AND existing.`OrganizationId` = membership.`OrganizationId`
        AND existing.`UserId` = assignment.`UserId`
        AND existing.`RoleCode` = assignment.`RoleCode`
  );

DELETE FROM `idt_UserRoleAssignments`
WHERE `ProductCode` = 'SYNQ_LIENS'
  AND `OrganizationId` IS NULL;

COMMIT;

SET @migration_sql = IF(
    EXISTS (
        SELECT 1 FROM information_schema.statistics
        WHERE table_schema = DATABASE()
          AND table_name = 'idt_UserProductAccess'
          AND index_name = 'IX_UserProductAccess_TenantId_OrganizationId_UserId_ProductCode'
    ),
    'SELECT 1',
    'CREATE UNIQUE INDEX `IX_UserProductAccess_TenantId_OrganizationId_UserId_ProductCode` ON `idt_UserProductAccess` (`TenantId`, `OrganizationScopeId`, `UserId`, `ProductCode`)'
);
PREPARE migration_statement FROM @migration_sql;
EXECUTE migration_statement;
DEALLOCATE PREPARE migration_statement;

SET @migration_sql = IF(
    EXISTS (
        SELECT 1 FROM information_schema.statistics
        WHERE table_schema = DATABASE()
          AND table_name = 'idt_UserRoleAssignments'
          AND index_name = 'IX_UserRoleAssignments_TenantId_OrganizationId_UserId_RoleCode'
    ),
    'SELECT 1',
    'CREATE INDEX `IX_UserRoleAssignments_TenantId_OrganizationId_UserId_RoleCode` ON `idt_UserRoleAssignments` (`TenantId`, `OrganizationId`, `UserId`, `RoleCode`)'
);
PREPARE migration_statement FROM @migration_sql;
EXECUTE migration_statement;
DEALLOCATE PREPARE migration_statement;

SET @migration_sql = IF(
    EXISTS (
        SELECT 1 FROM information_schema.statistics
        WHERE table_schema = DATABASE()
          AND table_name = 'idt_UserOrganizationMemberships'
          AND index_name = 'IX_idt_UserOrganizationMemberships_OrganizationId_Department'
    ),
    'SELECT 1',
    'CREATE INDEX `IX_idt_UserOrganizationMemberships_OrganizationId_Department` ON `idt_UserOrganizationMemberships` (`OrganizationId`, `Department`)'
);
PREPARE migration_statement FROM @migration_sql;
EXECUTE migration_statement;
DEALLOCATE PREPARE migration_statement;

SET @migration_sql = IF(
    EXISTS (
        SELECT 1 FROM information_schema.statistics
        WHERE table_schema = DATABASE()
          AND table_name = 'idt_UserInvitations'
          AND index_name = 'IX_UserInvitations_SynqLienScopeUserStatus'
    ),
    'SELECT 1',
    'CREATE INDEX `IX_UserInvitations_SynqLienScopeUserStatus` ON `idt_UserInvitations` (`TenantId`, `OrganizationId`, `ProductCode`, `UserId`, `Status`)'
);
PREPARE migration_statement FROM @migration_sql;
EXECUTE migration_statement;
DEALLOCATE PREPARE migration_statement;

-- Organization-scoped SynqLien management roles and assignments.
CREATE TABLE IF NOT EXISTS `idt_SynqLienAccessRoles` (
    `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
    `TenantId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
    `OrganizationId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
    `Name` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `ActiveName` varchar(100) CHARACTER SET utf8mb4
        GENERATED ALWAYS AS (CASE WHEN `IsActive` = 1 THEN LOWER(`Name`) ELSE NULL END) STORED,
    `Description` varchar(500) CHARACTER SET utf8mb4 NULL,
    `IsSystem` tinyint(1) NOT NULL,
    `IsActive` tinyint(1) NOT NULL,
    `CreatedAtUtc` datetime(6) NOT NULL,
    `UpdatedAtUtc` datetime(6) NOT NULL,
    `CreatedByUserId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NULL,
    `UpdatedByUserId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NULL,
    CONSTRAINT `PK_idt_SynqLienAccessRoles` PRIMARY KEY (`Id`),
    UNIQUE KEY `IX_SynqLienAccessRoles_Tenant_Organization_ActiveName`
        (`TenantId`, `OrganizationId`, `ActiveName`)
) CHARACTER SET=utf8mb4;

CREATE TABLE IF NOT EXISTS `idt_SynqLienAccessRolePermissions` (
    `RoleId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
    `PermissionId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
    CONSTRAINT `PK_idt_SynqLienAccessRolePermissions` PRIMARY KEY (`RoleId`, `PermissionId`),
    CONSTRAINT `FK_SynqLienRolePermissions_Role`
        FOREIGN KEY (`RoleId`) REFERENCES `idt_SynqLienAccessRoles` (`Id`) ON DELETE CASCADE,
    CONSTRAINT `FK_SynqLienRolePermissions_Permission`
        FOREIGN KEY (`PermissionId`) REFERENCES `idt_Capabilities` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;

CREATE TABLE IF NOT EXISTS `idt_SynqLienUserAccessRoleAssignments` (
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
    CONSTRAINT `FK_SynqLienUserAccessRoleAssignments_Role`
        FOREIGN KEY (`RoleId`) REFERENCES `idt_SynqLienAccessRoles` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_SynqLienUserAccessRoleAssignments_User`
        FOREIGN KEY (`UserId`) REFERENCES `idt_Users` (`Id`) ON DELETE CASCADE,
    KEY `IX_SynqLienUserAccessRoleAssignments_Scope`
        (`TenantId`, `OrganizationId`, `UserId`, `RoleId`),
    UNIQUE KEY `IX_SynqLienUserAccessRoleAssignments_Active`
        (`TenantId`, `OrganizationId`, `UserId`, `ActiveSlot`)
) CHARACTER SET=utf8mb4;

START TRANSACTION;

INSERT IGNORE INTO `idt_Capabilities`
    (`Id`, `ProductId`, `Code`, `Name`, `Description`, `Category`, `IsActive`,
     `CreatedAtUtc`, `UpdatedAtUtc`, `CreatedBy`, `UpdatedBy`)
VALUES
    ('6b000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000002', 'SYNQ_LIENS.users:view', 'View Users', 'View SynqLien users in the current organization', 'User Management', 1, UTC_TIMESTAMP(6), NULL, NULL, NULL),
    ('6b000000-0000-0000-0000-000000000002', '10000000-0000-0000-0000-000000000002', 'SYNQ_LIENS.users:manage', 'Manage Users', 'Manage SynqLien access in the current organization', 'User Management', 1, UTC_TIMESTAMP(6), NULL, NULL, NULL),
    ('6b000000-0000-0000-0000-000000000003', '10000000-0000-0000-0000-000000000002', 'SYNQ_LIENS.invitations:manage', 'Manage Invitations', 'Invite users and manage pending SynqLien invitations', 'User Management', 1, UTC_TIMESTAMP(6), NULL, NULL, NULL),
    ('6b000000-0000-0000-0000-000000000004', '10000000-0000-0000-0000-000000000002', 'SYNQ_LIENS.roles:view', 'View Roles', 'View organization SynqLien access roles', 'User Management', 1, UTC_TIMESTAMP(6), NULL, NULL, NULL),
    ('6b000000-0000-0000-0000-000000000005', '10000000-0000-0000-0000-000000000002', 'SYNQ_LIENS.roles:manage', 'Manage Roles', 'Create, edit, and retire organization SynqLien access roles', 'User Management', 1, UTC_TIMESTAMP(6), NULL, NULL, NULL),
    ('6b000000-0000-0000-0000-000000000101', '10000000-0000-0000-0000-000000000002', 'SYNQ_LIENS.lien:read', 'Read Liens', 'View liens in the current organization', 'Lien', 1, UTC_TIMESTAMP(6), NULL, NULL, NULL),
    ('6b000000-0000-0000-0000-000000000102', '10000000-0000-0000-0000-000000000002', 'SYNQ_LIENS.lien:update', 'Update Liens', 'Update liens in the current organization', 'Lien', 1, UTC_TIMESTAMP(6), NULL, NULL, NULL),
    ('6b000000-0000-0000-0000-000000000103', '10000000-0000-0000-0000-000000000002', 'SYNQ_LIENS.workflow:manage', 'Manage Workflows', 'Manage SynqLien workflows', 'Workflow', 1, UTC_TIMESTAMP(6), NULL, NULL, NULL),
    ('6b000000-0000-0000-0000-000000000104', '10000000-0000-0000-0000-000000000002', 'SYNQ_LIENS.task_template:manage', 'Manage Task Templates', 'Manage SynqLien task templates', 'Task', 1, UTC_TIMESTAMP(6), NULL, NULL, NULL),
    ('6b000000-0000-0000-0000-000000000105', '10000000-0000-0000-0000-000000000002', 'SYNQ_LIENS.task_automation:manage', 'Manage Task Automations', 'Manage SynqLien task automations', 'Task', 1, UTC_TIMESTAMP(6), NULL, NULL, NULL),
    ('6b000000-0000-0000-0000-000000000106', '10000000-0000-0000-0000-000000000002', 'SYNQ_LIENS.task_note:manage', 'Manage Task Notes', 'Manage SynqLien task notes', 'Task', 1, UTC_TIMESTAMP(6), NULL, NULL, NULL),
    ('6b000000-0000-0000-0000-000000000107', '10000000-0000-0000-0000-000000000002', 'SYNQ_LIENS.case_note:manage', 'Manage Case Notes', 'Manage SynqLien case notes', 'Case', 1, UTC_TIMESTAMP(6), NULL, NULL, NULL);

INSERT INTO `idt_SynqLienAccessRoles`
    (`Id`, `TenantId`, `OrganizationId`, `Name`, `Description`, `IsSystem`, `IsActive`,
     `CreatedAtUtc`, `UpdatedAtUtc`, `CreatedByUserId`, `UpdatedByUserId`)
SELECT UUID(), organization.`TenantId`, organization.`Id`, starter.`Name`, starter.`Description`,
       1, 1, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6), organization.`OwnerUserId`, organization.`OwnerUserId`
FROM `idt_Organizations` AS organization
CROSS JOIN (
    SELECT 'Administrator' AS `Name`, 'Full SynqLien access and organization user management' AS `Description`
    UNION ALL SELECT 'Quality Assurance', 'Operational review and quality-control access'
    UNION ALL SELECT 'View Only', 'Read-only SynqLien access'
) AS starter
WHERE organization.`IsActive` = 1
  AND organization.`OrgType` = 'LAW_FIRM'
  AND NOT EXISTS (
      SELECT 1
      FROM `idt_SynqLienAccessRoles` AS existing
      WHERE existing.`TenantId` = organization.`TenantId`
        AND existing.`OrganizationId` = organization.`Id`
        AND existing.`Name` = starter.`Name`
  );

INSERT IGNORE INTO `idt_SynqLienAccessRolePermissions` (`RoleId`, `PermissionId`)
SELECT role.`Id`, permission.`Id`
FROM `idt_SynqLienAccessRoles` AS role
INNER JOIN `idt_Capabilities` AS permission
    ON permission.`ProductId` = '10000000-0000-0000-0000-000000000002'
   AND permission.`IsActive` = 1
WHERE role.`Name` = 'Administrator'
  AND role.`IsSystem` = 1;

INSERT IGNORE INTO `idt_SynqLienAccessRolePermissions` (`RoleId`, `PermissionId`)
SELECT role.`Id`, permission.`Id`
FROM `idt_SynqLienAccessRoles` AS role
INNER JOIN `idt_Capabilities` AS permission
    ON permission.`Code` IN (
        'SYNQ_LIENS.users:view', 'SYNQ_LIENS.roles:view',
        'SYNQ_LIENS.case:read', 'SYNQ_LIENS.case:update',
        'SYNQ_LIENS.lien:read', 'SYNQ_LIENS.lien:update',
        'SYNQ_LIENS.task:read', 'SYNQ_LIENS.task_note:manage',
        'SYNQ_LIENS.case_note:manage', 'SYNQ_LIENS.lien_sale:read',
        'SYNQ_LIENS.lien_sale:view_analytics'
    )
WHERE role.`Name` = 'Quality Assurance'
  AND role.`IsSystem` = 1;

INSERT IGNORE INTO `idt_SynqLienAccessRolePermissions` (`RoleId`, `PermissionId`)
SELECT role.`Id`, permission.`Id`
FROM `idt_SynqLienAccessRoles` AS role
INNER JOIN `idt_Capabilities` AS permission
    ON permission.`Code` IN (
        'SYNQ_LIENS.users:view', 'SYNQ_LIENS.roles:view',
        'SYNQ_LIENS.case:read', 'SYNQ_LIENS.lien:read',
        'SYNQ_LIENS.task:read', 'SYNQ_LIENS.lien_sale:read'
    )
WHERE role.`Name` = 'View Only'
  AND role.`IsSystem` = 1;

INSERT IGNORE INTO `idt_SynqLienUserAccessRoleAssignments`
    (`Id`, `TenantId`, `OrganizationId`, `UserId`, `RoleId`, `IsActive`, `ActiveSlot`,
     `AssignedAtUtc`, `RemovedAtUtc`, `AssignedByUserId`, `RemovedByUserId`)
SELECT UUID(), role.`TenantId`, role.`OrganizationId`, organization.`OwnerUserId`, role.`Id`,
       1, '00000000-0000-0000-0000-000000000000', UTC_TIMESTAMP(6), NULL,
       organization.`OwnerUserId`, NULL
FROM `idt_SynqLienAccessRoles` AS role
INNER JOIN `idt_Organizations` AS organization
    ON organization.`Id` = role.`OrganizationId`
WHERE role.`Name` = 'Administrator'
  AND role.`IsSystem` = 1
  AND organization.`OwnerUserId` IS NOT NULL
  AND NOT EXISTS (
      SELECT 1
      FROM `idt_SynqLienUserAccessRoleAssignments` AS existing
      WHERE existing.`TenantId` = role.`TenantId`
        AND existing.`OrganizationId` = role.`OrganizationId`
        AND existing.`UserId` = organization.`OwnerUserId`
        AND existing.`RoleId` = role.`Id`
        AND existing.`IsActive` = 1
  );

COMMIT;

-- Validate the complete migration contract before updating EF history.
SET @columns_valid = EXISTS (
    SELECT 1 FROM information_schema.columns
    WHERE table_schema = DATABASE() AND table_name = 'idt_UserOrganizationMemberships'
      AND column_name = 'Department' AND data_type = 'varchar'
      AND character_maximum_length = 150 AND is_nullable = 'YES'
)
AND EXISTS (
    SELECT 1 FROM information_schema.columns
    WHERE table_schema = DATABASE() AND table_name = 'idt_UserOrganizationMemberships'
      AND column_name = 'JobTitle' AND data_type = 'varchar'
      AND character_maximum_length = 150 AND is_nullable = 'YES'
)
AND EXISTS (
    SELECT 1 FROM information_schema.columns
    WHERE table_schema = DATABASE() AND table_name = 'idt_UserInvitations'
      AND column_name = 'OrganizationId' AND data_type = 'char'
      AND character_maximum_length = 36 AND is_nullable = 'YES'
      AND collation_name = 'ascii_general_ci'
)
AND EXISTS (
    SELECT 1 FROM information_schema.columns
    WHERE table_schema = DATABASE() AND table_name = 'idt_UserInvitations'
      AND column_name = 'ProductCode' AND data_type = 'varchar'
      AND character_maximum_length = 50 AND is_nullable = 'YES'
)
AND EXISTS (
    SELECT 1 FROM information_schema.columns
    WHERE table_schema = DATABASE() AND table_name = 'idt_UserInvitations'
      AND column_name = 'PendingAccessRoleId' AND data_type = 'char'
      AND character_maximum_length = 36 AND is_nullable = 'YES'
      AND collation_name = 'ascii_general_ci'
)
AND EXISTS (
    SELECT 1 FROM information_schema.columns
    WHERE table_schema = DATABASE() AND table_name = 'idt_UserInvitations'
      AND column_name = 'PendingDepartment' AND data_type = 'varchar'
      AND character_maximum_length = 150 AND is_nullable = 'YES'
)
AND EXISTS (
    SELECT 1 FROM information_schema.columns
    WHERE table_schema = DATABASE() AND table_name = 'idt_UserInvitations'
      AND column_name = 'PendingJobTitle' AND data_type = 'varchar'
      AND character_maximum_length = 150 AND is_nullable = 'YES'
)
AND EXISTS (
    SELECT 1 FROM information_schema.columns
    WHERE table_schema = DATABASE() AND table_name = 'idt_UserInvitations'
      AND column_name = 'RequiresAccountActivation' AND data_type = 'tinyint'
      AND is_nullable = 'NO' AND column_default = '1'
)
AND EXISTS (
    SELECT 1 FROM information_schema.columns
    WHERE table_schema = DATABASE() AND table_name = 'idt_UserProductAccess'
      AND column_name = 'OrganizationScopeId' AND data_type = 'char'
      AND character_maximum_length = 36 AND collation_name = 'ascii_general_ci'
      AND generation_expression <> ''
      AND LOWER(generation_expression) LIKE '%coalesce%'
      AND generation_expression LIKE '%OrganizationId%'
      AND generation_expression LIKE '%00000000-0000-0000-0000-000000000000%'
);

SET @indexes_valid = (
    SELECT COUNT(*) FROM information_schema.statistics
    WHERE table_schema = DATABASE()
      AND table_name = 'idt_UserOrganizationMemberships'
      AND index_name = 'IX_idt_UserOrganizationMemberships_OrganizationId_Department'
      AND non_unique = 1
      AND ((seq_in_index = 1 AND column_name = 'OrganizationId')
        OR (seq_in_index = 2 AND column_name = 'Department'))
) = 2
AND (
    SELECT COUNT(*) FROM information_schema.statistics
    WHERE table_schema = DATABASE()
      AND table_name = 'idt_UserInvitations'
      AND index_name = 'IX_UserInvitations_SynqLienScopeUserStatus'
      AND non_unique = 1
      AND ((seq_in_index = 1 AND column_name = 'TenantId')
        OR (seq_in_index = 2 AND column_name = 'OrganizationId')
        OR (seq_in_index = 3 AND column_name = 'ProductCode')
        OR (seq_in_index = 4 AND column_name = 'UserId')
        OR (seq_in_index = 5 AND column_name = 'Status'))
) = 5
AND (
    SELECT COUNT(*) FROM information_schema.statistics
    WHERE table_schema = DATABASE()
      AND table_name = 'idt_UserProductAccess'
      AND index_name = 'IX_UserProductAccess_TenantId_OrganizationId_UserId_ProductCode'
      AND non_unique = 0
      AND ((seq_in_index = 1 AND column_name = 'TenantId')
        OR (seq_in_index = 2 AND column_name = 'OrganizationScopeId')
        OR (seq_in_index = 3 AND column_name = 'UserId')
        OR (seq_in_index = 4 AND column_name = 'ProductCode'))
) = 4
AND (
    SELECT COUNT(*) FROM information_schema.statistics
    WHERE table_schema = DATABASE()
      AND table_name = 'idt_UserRoleAssignments'
      AND index_name = 'IX_UserRoleAssignments_TenantId_OrganizationId_UserId_RoleCode'
      AND non_unique = 1
      AND ((seq_in_index = 1 AND column_name = 'TenantId')
        OR (seq_in_index = 2 AND column_name = 'OrganizationId')
        OR (seq_in_index = 3 AND column_name = 'UserId')
        OR (seq_in_index = 4 AND column_name = 'RoleCode'))
) = 4
AND NOT EXISTS (
    SELECT 1 FROM information_schema.statistics
    WHERE table_schema = DATABASE()
      AND ((table_name = 'idt_UserProductAccess'
            AND index_name = 'IX_UserProductAccess_TenantId_UserId_ProductCode')
        OR (table_name = 'idt_UserRoleAssignments'
            AND index_name = 'IX_UserRoleAssignments_TenantId_UserId_RoleCode'))
);

SET @role_columns_valid = (
    SELECT COUNT(*)
    FROM information_schema.columns
    WHERE table_schema = DATABASE()
      AND (
          (table_name = 'idt_SynqLienAccessRoles' AND (
              (column_name IN ('Id', 'TenantId', 'OrganizationId') AND data_type = 'char' AND character_maximum_length = 36 AND is_nullable = 'NO')
              OR (column_name = 'Name' AND data_type = 'varchar' AND character_maximum_length = 100 AND is_nullable = 'NO')
              OR (column_name = 'ActiveName' AND data_type = 'varchar' AND character_maximum_length = 100 AND is_nullable = 'YES' AND generation_expression <> '')
              OR (column_name = 'Description' AND data_type = 'varchar' AND character_maximum_length = 500 AND is_nullable = 'YES')
              OR (column_name IN ('IsSystem', 'IsActive') AND data_type = 'tinyint' AND is_nullable = 'NO')
              OR (column_name IN ('CreatedAtUtc', 'UpdatedAtUtc') AND data_type = 'datetime' AND datetime_precision = 6 AND is_nullable = 'NO')
              OR (column_name IN ('CreatedByUserId', 'UpdatedByUserId') AND data_type = 'char' AND character_maximum_length = 36 AND is_nullable = 'YES')
          ))
          OR (table_name = 'idt_SynqLienAccessRolePermissions'
              AND column_name IN ('RoleId', 'PermissionId')
              AND data_type = 'char' AND character_maximum_length = 36 AND is_nullable = 'NO')
          OR (table_name = 'idt_SynqLienUserAccessRoleAssignments' AND (
              (column_name IN ('Id', 'TenantId', 'OrganizationId', 'UserId', 'RoleId') AND data_type = 'char' AND character_maximum_length = 36 AND is_nullable = 'NO')
              OR (column_name = 'IsActive' AND data_type = 'tinyint' AND is_nullable = 'NO')
              OR (column_name = 'ActiveSlot' AND data_type = 'char' AND character_maximum_length = 36 AND is_nullable = 'YES')
              OR (column_name = 'AssignedAtUtc' AND data_type = 'datetime' AND datetime_precision = 6 AND is_nullable = 'NO')
              OR (column_name = 'RemovedAtUtc' AND data_type = 'datetime' AND datetime_precision = 6 AND is_nullable = 'YES')
              OR (column_name IN ('AssignedByUserId', 'RemovedByUserId') AND data_type = 'char' AND character_maximum_length = 36 AND is_nullable = 'YES')
          ))
      )
) = 25;

SET @role_primary_keys_valid = (
    SELECT COUNT(*) FROM information_schema.key_column_usage
    WHERE constraint_schema = DATABASE()
      AND table_name = 'idt_SynqLienAccessRoles'
      AND constraint_name = 'PRIMARY'
      AND column_name = 'Id' AND ordinal_position = 1
) = 1
AND (
    SELECT COUNT(*) FROM information_schema.key_column_usage
    WHERE constraint_schema = DATABASE()
      AND table_name = 'idt_SynqLienAccessRolePermissions'
      AND constraint_name = 'PRIMARY'
      AND ((column_name = 'RoleId' AND ordinal_position = 1)
        OR (column_name = 'PermissionId' AND ordinal_position = 2))
) = 2
AND (
    SELECT COUNT(*) FROM information_schema.key_column_usage
    WHERE constraint_schema = DATABASE()
      AND table_name = 'idt_SynqLienUserAccessRoleAssignments'
      AND constraint_name = 'PRIMARY'
      AND column_name = 'Id' AND ordinal_position = 1
) = 1;

SET @role_foreign_keys_valid = (
    SELECT COUNT(*)
    FROM information_schema.referential_constraints AS relation
    INNER JOIN information_schema.key_column_usage AS key_column
        ON key_column.constraint_schema = relation.constraint_schema
       AND key_column.constraint_name = relation.constraint_name
       AND key_column.table_name = relation.table_name
    WHERE relation.constraint_schema = DATABASE()
      AND (
          (relation.table_name = 'idt_SynqLienAccessRolePermissions'
              AND relation.constraint_name = 'FK_SynqLienRolePermissions_Role'
              AND key_column.column_name = 'RoleId'
              AND key_column.referenced_table_name = 'idt_SynqLienAccessRoles'
              AND key_column.referenced_column_name = 'Id'
              AND relation.delete_rule = 'CASCADE')
          OR (relation.table_name = 'idt_SynqLienAccessRolePermissions'
              AND relation.constraint_name = 'FK_SynqLienRolePermissions_Permission'
              AND key_column.column_name = 'PermissionId'
              AND key_column.referenced_table_name = 'idt_Capabilities'
              AND key_column.referenced_column_name = 'Id'
              AND relation.delete_rule = 'RESTRICT')
          OR (relation.table_name = 'idt_SynqLienUserAccessRoleAssignments'
              AND relation.constraint_name = 'FK_SynqLienUserAccessRoleAssignments_Role'
              AND key_column.column_name = 'RoleId'
              AND key_column.referenced_table_name = 'idt_SynqLienAccessRoles'
              AND key_column.referenced_column_name = 'Id'
              AND relation.delete_rule = 'RESTRICT')
          OR (relation.table_name = 'idt_SynqLienUserAccessRoleAssignments'
              AND relation.constraint_name = 'FK_SynqLienUserAccessRoleAssignments_User'
              AND key_column.column_name = 'UserId'
              AND key_column.referenced_table_name = 'idt_Users'
              AND key_column.referenced_column_name = 'Id'
              AND relation.delete_rule = 'CASCADE')
      )
) = 4;

SET @role_indexes_valid = (
    SELECT COUNT(*) FROM information_schema.statistics
    WHERE table_schema = DATABASE()
      AND table_name = 'idt_SynqLienAccessRoles'
      AND index_name = 'IX_SynqLienAccessRoles_Tenant_Organization_ActiveName'
      AND non_unique = 0
      AND ((seq_in_index = 1 AND column_name = 'TenantId')
        OR (seq_in_index = 2 AND column_name = 'OrganizationId')
        OR (seq_in_index = 3 AND column_name = 'ActiveName'))
) = 3
AND (
    SELECT COUNT(*) FROM information_schema.statistics
    WHERE table_schema = DATABASE()
      AND table_name = 'idt_SynqLienUserAccessRoleAssignments'
      AND index_name = 'IX_SynqLienUserAccessRoleAssignments_Scope'
      AND non_unique = 1
      AND ((seq_in_index = 1 AND column_name = 'TenantId')
        OR (seq_in_index = 2 AND column_name = 'OrganizationId')
        OR (seq_in_index = 3 AND column_name = 'UserId')
        OR (seq_in_index = 4 AND column_name = 'RoleId'))
) = 4
AND (
    SELECT COUNT(*) FROM information_schema.statistics
    WHERE table_schema = DATABASE()
      AND table_name = 'idt_SynqLienUserAccessRoleAssignments'
      AND index_name = 'IX_SynqLienUserAccessRoleAssignments_Active'
      AND non_unique = 0
      AND ((seq_in_index = 1 AND column_name = 'TenantId')
        OR (seq_in_index = 2 AND column_name = 'OrganizationId')
        OR (seq_in_index = 3 AND column_name = 'UserId')
        OR (seq_in_index = 4 AND column_name = 'ActiveSlot'))
) = 4;

SET @tables_valid = (
    SELECT COUNT(*)
    FROM information_schema.tables
    WHERE table_schema = DATABASE()
      AND table_name IN (
          'idt_SynqLienAccessRoles',
          'idt_SynqLienAccessRolePermissions',
          'idt_SynqLienUserAccessRoleAssignments')
) = 3
AND @role_columns_valid = 1
AND @role_primary_keys_valid = 1
AND @role_foreign_keys_valid = 1
AND @role_indexes_valid = 1;

SET @backfill_valid = NOT EXISTS (
    SELECT 1
    FROM `idt_UserProductAccess`
    WHERE `ProductCode` = 'SYNQ_LIENS'
      AND `OrganizationId` IS NULL
)
AND NOT EXISTS (
    SELECT 1
    FROM `idt_UserRoleAssignments`
    WHERE `ProductCode` = 'SYNQ_LIENS'
      AND `OrganizationId` IS NULL
);

SET @capabilities_valid = (
    SELECT COUNT(DISTINCT `Code`)
    FROM `idt_Capabilities`
    WHERE `ProductId` = '10000000-0000-0000-0000-000000000002'
      AND `IsActive` = 1
      AND `Code` IN (
        'SYNQ_LIENS.users:view', 'SYNQ_LIENS.users:manage',
        'SYNQ_LIENS.invitations:manage', 'SYNQ_LIENS.roles:view',
        'SYNQ_LIENS.roles:manage', 'SYNQ_LIENS.lien:read',
        'SYNQ_LIENS.lien:update', 'SYNQ_LIENS.workflow:manage',
        'SYNQ_LIENS.task_template:manage', 'SYNQ_LIENS.task_automation:manage',
        'SYNQ_LIENS.task_note:manage', 'SYNQ_LIENS.case_note:manage'
    )
) = 12;

SET @starter_roles_valid = NOT EXISTS (
    SELECT 1
    FROM `idt_Organizations` AS organization
    CROSS JOIN (
        SELECT 'Administrator' AS `Name`
        UNION ALL SELECT 'Quality Assurance'
        UNION ALL SELECT 'View Only'
    ) AS starter
    LEFT JOIN `idt_SynqLienAccessRoles` AS role
        ON role.`TenantId` = organization.`TenantId`
       AND role.`OrganizationId` = organization.`Id`
       AND role.`Name` = starter.`Name`
       AND role.`IsSystem` = 1
       AND role.`IsActive` = 1
    WHERE organization.`IsActive` = 1
      AND organization.`OrgType` = 'LAW_FIRM'
      AND role.`Id` IS NULL
);

SET @owner_assignments_valid = NOT EXISTS (
    SELECT 1
    FROM `idt_Organizations` AS organization
    WHERE organization.`IsActive` = 1
      AND organization.`OrgType` = 'LAW_FIRM'
      AND organization.`OwnerUserId` IS NOT NULL
      AND NOT EXISTS (
          SELECT 1
          FROM `idt_SynqLienUserAccessRoleAssignments` AS assignment
          INNER JOIN `idt_SynqLienAccessRoles` AS role
              ON role.`Id` = assignment.`RoleId`
          WHERE assignment.`TenantId` = organization.`TenantId`
            AND assignment.`OrganizationId` = organization.`Id`
            AND assignment.`UserId` = organization.`OwnerUserId`
            AND assignment.`IsActive` = 1
            AND role.`Name` = 'Administrator'
            AND role.`IsSystem` = 1
            AND role.`IsActive` = 1
      )
);

SET @role_permissions_valid = NOT EXISTS (
    SELECT 1
    FROM `idt_SynqLienAccessRoles` AS role
    WHERE role.`IsSystem` = 1
      AND role.`IsActive` = 1
      AND role.`Name` = 'Administrator'
      AND EXISTS (
          SELECT 1
          FROM `idt_Capabilities` AS permission
          LEFT JOIN `idt_SynqLienAccessRolePermissions` AS mapping
              ON mapping.`RoleId` = role.`Id`
             AND mapping.`PermissionId` = permission.`Id`
          WHERE permission.`ProductId` = '10000000-0000-0000-0000-000000000002'
            AND permission.`IsActive` = 1
            AND mapping.`RoleId` IS NULL
      )
)
AND NOT EXISTS (
    SELECT 1
    FROM `idt_SynqLienAccessRoles` AS role
    WHERE role.`IsSystem` = 1
      AND role.`IsActive` = 1
      AND role.`Name` = 'Quality Assurance'
      AND (
          SELECT COUNT(DISTINCT permission.`Code`)
          FROM `idt_SynqLienAccessRolePermissions` AS mapping
          INNER JOIN `idt_Capabilities` AS permission
              ON permission.`Id` = mapping.`PermissionId`
          WHERE mapping.`RoleId` = role.`Id`
            AND permission.`Code` IN (
                'SYNQ_LIENS.users:view', 'SYNQ_LIENS.roles:view',
                'SYNQ_LIENS.case:read', 'SYNQ_LIENS.case:update',
                'SYNQ_LIENS.lien:read', 'SYNQ_LIENS.lien:update',
                'SYNQ_LIENS.task:read', 'SYNQ_LIENS.task_note:manage',
                'SYNQ_LIENS.case_note:manage', 'SYNQ_LIENS.lien_sale:read',
                'SYNQ_LIENS.lien_sale:view_analytics'
            )
      ) <> 11
)
AND NOT EXISTS (
    SELECT 1
    FROM `idt_SynqLienAccessRoles` AS role
    WHERE role.`IsSystem` = 1
      AND role.`IsActive` = 1
      AND role.`Name` = 'View Only'
      AND (
          SELECT COUNT(DISTINCT permission.`Code`)
          FROM `idt_SynqLienAccessRolePermissions` AS mapping
          INNER JOIN `idt_Capabilities` AS permission
              ON permission.`Id` = mapping.`PermissionId`
          WHERE mapping.`RoleId` = role.`Id`
            AND permission.`Code` IN (
                'SYNQ_LIENS.users:view', 'SYNQ_LIENS.roles:view',
                'SYNQ_LIENS.case:read', 'SYNQ_LIENS.lien:read',
                'SYNQ_LIENS.task:read', 'SYNQ_LIENS.lien_sale:read'
            )
      ) <> 6
);

SET @contract_valid =
    @columns_valid = 1
    AND @indexes_valid = 1
    AND @tables_valid = 1
    AND @backfill_valid = 1
    AND @capabilities_valid = 1
    AND @starter_roles_valid = 1
    AND @owner_assignments_valid = 1
    AND @role_permissions_valid = 1;

INSERT IGNORE INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
SELECT '20260903020000_AddSynqLienUserManagement', '8.0.0'
WHERE @contract_valid = 1
  AND EXISTS (
      SELECT 1
      FROM `__EFMigrationsHistory`
      WHERE `MigrationId` = '20260824124500_AddCareConnectReferrerAdminReferralCapabilities'
  );

-- This row must report READY before Identity and Liens are restarted.
SELECT expected.`MigrationId`,
       IF(history.`MigrationId` IS NULL, 'NOT_RECORDED', 'RECORDED') AS `HistoryStatus`,
       IF(expected.`ContractValid` = 1, 'VALID', 'INVALID') AS `ContractStatus`,
       IF(predecessor.`MigrationId` IS NULL, 'MISSING_PREDECESSOR', 'PREDECESSOR_READY') AS `PredecessorStatus`,
       IF(history.`MigrationId` IS NOT NULL
          AND expected.`ContractValid` = 1
          AND predecessor.`MigrationId` IS NOT NULL,
          'READY', 'NOT_READY') AS `Status`
FROM (
    SELECT '20260903020000_AddSynqLienUserManagement' AS `MigrationId`,
           @contract_valid AS `ContractValid`
) AS expected
LEFT JOIN `__EFMigrationsHistory` AS history
    ON history.`MigrationId` = expected.`MigrationId`
LEFT JOIN `__EFMigrationsHistory` AS predecessor
    ON predecessor.`MigrationId` = '20260824124500_AddCareConnectReferrerAdminReferralCapabilities';

SELECT @columns_valid AS `ColumnsValid`,
       @indexes_valid AS `IndexesValid`,
       @tables_valid AS `RoleTablesValid`,
       @role_columns_valid AS `RoleColumnsValid`,
       @role_primary_keys_valid AS `RolePrimaryKeysValid`,
       @role_foreign_keys_valid AS `RoleForeignKeysValid`,
       @role_indexes_valid AS `RoleIndexesValid`,
       @backfill_valid AS `LegacyBackfillValid`,
       @capabilities_valid AS `CapabilitiesValid`,
       @starter_roles_valid AS `StarterRolesValid`,
       @owner_assignments_valid AS `OwnerAssignmentsValid`,
       @role_permissions_valid AS `RolePermissionsValid`;
