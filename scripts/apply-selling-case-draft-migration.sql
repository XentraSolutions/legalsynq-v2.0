-- ============================================================================
-- SynqLien Selling case-draft migration repair
--
-- Run this against the target Liens MySQL database only when EF migration
-- 20260826141219_AddSellingCaseDraft is absent from __EFMigrationsHistory.
-- The script is idempotent: it creates only missing schema objects and records
-- the EF migration after the table, indexes, and foreign keys are present.
--
-- Example:
--   mysql --defaults-extra-file=/secure/liens-qa.cnf LS_QA_LIENS \
--     < scripts/apply-selling-case-draft-migration.sql
-- ============================================================================

DROP PROCEDURE IF EXISTS `apply_selling_case_draft_migration`;

DELIMITER //

CREATE PROCEDURE `apply_selling_case_draft_migration`()
BEGIN
    DECLARE v_count INT DEFAULT 0;

    -- MySQL auto-commits DDL, so every operation is independently guarded and
    -- safe to rerun after an interrupted deployment.
    CREATE TABLE IF NOT EXISTS `liens_SellingCaseDrafts` (
        `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
        `TenantId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
        `OrgId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
        `CaseStatus` varchar(50) NOT NULL,
        `AccidentTypeId` varchar(100) NULL,
        `AccidentState` varchar(100) NULL,
        `DateOfLoss` date NULL,
        `HandlingLawFirmCompanyId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NULL,
        `CaseManagerContactPersonId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NULL,
        `CaseTrackingNotes` varchar(4000) NULL,
        `CaseId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NULL,
        `FinalizedAtUtc` datetime(6) NULL,
        `CreatedAtUtc` datetime(6) NOT NULL,
        `UpdatedAtUtc` datetime(6) NOT NULL,
        `CreatedByUserId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
        `UpdatedByUserId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NULL,
        CONSTRAINT `PK_liens_SellingCaseDrafts` PRIMARY KEY (`Id`)
    ) CHARACTER SET=utf8mb4;

    SELECT COUNT(*) INTO v_count
    FROM information_schema.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'liens_SellingCaseDrafts'
      AND INDEX_NAME = 'IX_liens_SellingCaseDrafts_CaseManagerContactPersonId';
    IF v_count = 0 THEN
        CREATE INDEX `IX_liens_SellingCaseDrafts_CaseManagerContactPersonId`
            ON `liens_SellingCaseDrafts` (`CaseManagerContactPersonId`);
    END IF;

    SELECT COUNT(*) INTO v_count
    FROM information_schema.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'liens_SellingCaseDrafts'
      AND INDEX_NAME = 'IX_liens_SellingCaseDrafts_HandlingLawFirmCompanyId';
    IF v_count = 0 THEN
        CREATE INDEX `IX_liens_SellingCaseDrafts_HandlingLawFirmCompanyId`
            ON `liens_SellingCaseDrafts` (`HandlingLawFirmCompanyId`);
    END IF;

    SELECT COUNT(*) INTO v_count
    FROM information_schema.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'liens_SellingCaseDrafts'
      AND INDEX_NAME = 'IX_SellingCaseDrafts_Tenant_Org_CreatedAtUtc';
    IF v_count = 0 THEN
        CREATE INDEX `IX_SellingCaseDrafts_Tenant_Org_CreatedAtUtc`
            ON `liens_SellingCaseDrafts` (`TenantId`, `OrgId`, `CreatedAtUtc`);
    END IF;

    SELECT COUNT(*) INTO v_count
    FROM information_schema.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'liens_SellingCaseDrafts'
      AND INDEX_NAME = 'IX_SellingCaseDrafts_Tenant_Org_FinalizedAtUtc';
    IF v_count = 0 THEN
        CREATE INDEX `IX_SellingCaseDrafts_Tenant_Org_FinalizedAtUtc`
            ON `liens_SellingCaseDrafts` (`TenantId`, `OrgId`, `FinalizedAtUtc`);
    END IF;

    SELECT COUNT(*) INTO v_count
    FROM information_schema.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'liens_SellingCaseDrafts'
      AND INDEX_NAME = 'UX_SellingCaseDrafts_CaseId';
    IF v_count = 0 THEN
        CREATE UNIQUE INDEX `UX_SellingCaseDrafts_CaseId`
            ON `liens_SellingCaseDrafts` (`CaseId`);
    END IF;

    SELECT COUNT(*) INTO v_count
    FROM information_schema.TABLES
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME IN ('liens_Cases', 'liens_Companies', 'liens_CompanyContactPersons');
    IF v_count <> 3 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Required Selling case-draft parent tables are missing.';
    END IF;

    SELECT COUNT(*) INTO v_count
    FROM information_schema.TABLE_CONSTRAINTS
    WHERE CONSTRAINT_SCHEMA = DATABASE()
      AND TABLE_NAME = 'liens_SellingCaseDrafts'
      AND CONSTRAINT_NAME = 'FK_liens_SellingCaseDrafts_liens_Cases_CaseId'
      AND CONSTRAINT_TYPE = 'FOREIGN KEY';
    IF v_count = 0 THEN
        ALTER TABLE `liens_SellingCaseDrafts`
            ADD CONSTRAINT `FK_liens_SellingCaseDrafts_liens_Cases_CaseId`
            FOREIGN KEY (`CaseId`) REFERENCES `liens_Cases` (`Id`) ON DELETE RESTRICT;
    END IF;

    SELECT COUNT(*) INTO v_count
    FROM information_schema.TABLE_CONSTRAINTS
    WHERE CONSTRAINT_SCHEMA = DATABASE()
      AND TABLE_NAME = 'liens_SellingCaseDrafts'
      AND CONSTRAINT_NAME = 'FK_liens_SellingCaseDrafts_liens_Companies_HandlingLawFirmCompa~'
      AND CONSTRAINT_TYPE = 'FOREIGN KEY';
    IF v_count = 0 THEN
        ALTER TABLE `liens_SellingCaseDrafts`
            ADD CONSTRAINT `FK_liens_SellingCaseDrafts_liens_Companies_HandlingLawFirmCompa~`
            FOREIGN KEY (`HandlingLawFirmCompanyId`) REFERENCES `liens_Companies` (`Id`) ON DELETE RESTRICT;
    END IF;

    SELECT COUNT(*) INTO v_count
    FROM information_schema.TABLE_CONSTRAINTS
    WHERE CONSTRAINT_SCHEMA = DATABASE()
      AND TABLE_NAME = 'liens_SellingCaseDrafts'
      AND CONSTRAINT_NAME = 'FK_liens_SellingCaseDrafts_liens_CompanyContactPersons_CaseMana~'
      AND CONSTRAINT_TYPE = 'FOREIGN KEY';
    IF v_count = 0 THEN
        ALTER TABLE `liens_SellingCaseDrafts`
            ADD CONSTRAINT `FK_liens_SellingCaseDrafts_liens_CompanyContactPersons_CaseMana~`
            FOREIGN KEY (`CaseManagerContactPersonId`) REFERENCES `liens_CompanyContactPersons` (`Id`) ON DELETE RESTRICT;
    END IF;

    INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
    SELECT '20260826141219_AddSellingCaseDraft', '8.0.2'
    WHERE NOT EXISTS (
        SELECT 1
        FROM `__EFMigrationsHistory`
        WHERE `MigrationId` = '20260826141219_AddSellingCaseDraft'
    );
END //

DELIMITER ;

CALL `apply_selling_case_draft_migration`();
DROP PROCEDURE `apply_selling_case_draft_migration`;

SELECT `MigrationId`
FROM `__EFMigrationsHistory`
WHERE `MigrationId` = '20260826141219_AddSellingCaseDraft';

SHOW TABLES LIKE 'liens_SellingCaseDrafts';
