-- SL-CORE Program 1 v3 report-parity schema prerequisite (MySQL/DBeaver)
--
-- Use this only when the EF migration
-- 20260825160000_AddLegacyReportParityFields has not yet been applied to the
-- selected LS_QA_LIENS or LS_LIENS schema. It is idempotent and deliberately
-- does not insert a row into __EFMigrationsHistory; the normal EF migration
-- must still run later to record its own history.

SET @target_schema = DATABASE();

SELECT COUNT(*) INTO @target_schema_is_valid
FROM information_schema.schemata
WHERE schema_name = @target_schema
  AND @target_schema IN ('LS_QA_LIENS', 'LS_LIENS');

SET @legalsynq_selling_ddl = IF(
    @target_schema_is_valid = 1,
    'SELECT 1',
    'SELECT * FROM `__invalid_liens_target_schema__`');
PREPARE legalsynq_selling_stmt FROM @legalsynq_selling_ddl;
EXECUTE legalsynq_selling_stmt;
DEALLOCATE PREPARE legalsynq_selling_stmt;

SET @legalsynq_selling_ddl = IF(
    (SELECT COUNT(*) FROM information_schema.columns
     WHERE table_schema = DATABASE() AND table_name = 'liens_Liens'
       AND column_name = 'ImportedCreatedByName') = 0,
    'ALTER TABLE `liens_Liens` ADD COLUMN `ImportedCreatedByName` varchar(100) CHARACTER SET utf8mb4 NULL',
    'SELECT 1');
PREPARE legalsynq_selling_stmt FROM @legalsynq_selling_ddl;
EXECUTE legalsynq_selling_stmt;
DEALLOCATE PREPARE legalsynq_selling_stmt;

SET @legalsynq_selling_ddl = IF((
    SELECT COUNT(*) FROM information_schema.columns
    WHERE table_schema = DATABASE() AND table_name = 'liens_Cases'
      AND column_name = 'AttorneyContactPersonId') = 0,
    'ALTER TABLE `liens_Cases` ADD COLUMN `AttorneyContactPersonId` char(36) COLLATE ascii_general_ci NULL',
    'SELECT 1');
PREPARE legalsynq_selling_stmt FROM @legalsynq_selling_ddl;
EXECUTE legalsynq_selling_stmt;
DEALLOCATE PREPARE legalsynq_selling_stmt;

SET @legalsynq_selling_ddl = IF((
    SELECT COUNT(*) FROM information_schema.columns
    WHERE table_schema = DATABASE() AND table_name = 'liens_Cases'
      AND column_name = 'CaseDropped') = 0,
    'ALTER TABLE `liens_Cases` ADD COLUMN `CaseDropped` tinyint(1) NULL',
    'SELECT 1');
PREPARE legalsynq_selling_stmt FROM @legalsynq_selling_ddl;
EXECUTE legalsynq_selling_stmt;
DEALLOCATE PREPARE legalsynq_selling_stmt;

SET @legalsynq_selling_ddl = IF((
    SELECT COUNT(*) FROM information_schema.columns
    WHERE table_schema = DATABASE() AND table_name = 'liens_Cases'
      AND column_name = 'ClientAddressLine1') = 0,
    'ALTER TABLE `liens_Cases` ADD COLUMN `ClientAddressLine1` varchar(300) CHARACTER SET utf8mb4 NULL',
    'SELECT 1');
PREPARE legalsynq_selling_stmt FROM @legalsynq_selling_ddl;
EXECUTE legalsynq_selling_stmt;
DEALLOCATE PREPARE legalsynq_selling_stmt;

SET @legalsynq_selling_ddl = IF((
    SELECT COUNT(*) FROM information_schema.columns
    WHERE table_schema = DATABASE() AND table_name = 'liens_Cases'
      AND column_name = 'ClientCity') = 0,
    'ALTER TABLE `liens_Cases` ADD COLUMN `ClientCity` varchar(100) CHARACTER SET utf8mb4 NULL',
    'SELECT 1');
PREPARE legalsynq_selling_stmt FROM @legalsynq_selling_ddl;
EXECUTE legalsynq_selling_stmt;
DEALLOCATE PREPARE legalsynq_selling_stmt;

SET @legalsynq_selling_ddl = IF((
    SELECT COUNT(*) FROM information_schema.columns
    WHERE table_schema = DATABASE() AND table_name = 'liens_Cases'
      AND column_name = 'ClientPostalCode') = 0,
    'ALTER TABLE `liens_Cases` ADD COLUMN `ClientPostalCode` varchar(20) CHARACTER SET utf8mb4 NULL',
    'SELECT 1');
PREPARE legalsynq_selling_stmt FROM @legalsynq_selling_ddl;
EXECUTE legalsynq_selling_stmt;
DEALLOCATE PREPARE legalsynq_selling_stmt;

SET @legalsynq_selling_ddl = IF((
    SELECT COUNT(*) FROM information_schema.columns
    WHERE table_schema = DATABASE() AND table_name = 'liens_Cases'
      AND column_name = 'ClientState') = 0,
    'ALTER TABLE `liens_Cases` ADD COLUMN `ClientState` varchar(100) CHARACTER SET utf8mb4 NULL',
    'SELECT 1');
PREPARE legalsynq_selling_stmt FROM @legalsynq_selling_ddl;
EXECUTE legalsynq_selling_stmt;
DEALLOCATE PREPARE legalsynq_selling_stmt;

SET @legalsynq_selling_ddl = IF((
    SELECT COUNT(*) FROM information_schema.columns
    WHERE table_schema = DATABASE() AND table_name = 'liens_Cases'
      AND column_name = 'CurrentMedicalStatus') = 0,
    'ALTER TABLE `liens_Cases` ADD COLUMN `CurrentMedicalStatus` varchar(50) CHARACTER SET utf8mb4 NULL',
    'SELECT 1');
PREPARE legalsynq_selling_stmt FROM @legalsynq_selling_ddl;
EXECUTE legalsynq_selling_stmt;
DEALLOCATE PREPARE legalsynq_selling_stmt;

SET @legalsynq_selling_ddl = IF((
    SELECT COUNT(*) FROM information_schema.columns
    WHERE table_schema = DATABASE() AND table_name = 'liens_Cases'
      AND column_name = 'ImportedCreatedByName') = 0,
    'ALTER TABLE `liens_Cases` ADD COLUMN `ImportedCreatedByName` varchar(100) CHARACTER SET utf8mb4 NULL',
    'SELECT 1');
PREPARE legalsynq_selling_stmt FROM @legalsynq_selling_ddl;
EXECUTE legalsynq_selling_stmt;
DEALLOCATE PREPARE legalsynq_selling_stmt;

SET @legalsynq_selling_ddl = IF((
    SELECT COUNT(*) FROM information_schema.columns
    WHERE table_schema = DATABASE() AND table_name = 'liens_Cases'
      AND column_name = 'IncidentState') = 0,
    'ALTER TABLE `liens_Cases` ADD COLUMN `IncidentState` varchar(100) CHARACTER SET utf8mb4 NULL',
    'SELECT 1');
PREPARE legalsynq_selling_stmt FROM @legalsynq_selling_ddl;
EXECUTE legalsynq_selling_stmt;
DEALLOCATE PREPARE legalsynq_selling_stmt;

SET @legalsynq_selling_ddl = IF((
    SELECT COUNT(*) FROM information_schema.columns
    WHERE table_schema = DATABASE() AND table_name = 'liens_Cases'
      AND column_name = 'MinorComp') = 0,
    'ALTER TABLE `liens_Cases` ADD COLUMN `MinorComp` tinyint(1) NULL',
    'SELECT 1');
PREPARE legalsynq_selling_stmt FROM @legalsynq_selling_ddl;
EXECUTE legalsynq_selling_stmt;
DEALLOCATE PREPARE legalsynq_selling_stmt;

SET @legalsynq_selling_ddl = IF((
    SELECT COUNT(*) FROM information_schema.columns
    WHERE table_schema = DATABASE() AND table_name = 'liens_Cases'
      AND column_name = 'TrackingFollowUpDate') = 0,
    'ALTER TABLE `liens_Cases` ADD COLUMN `TrackingFollowUpDate` date NULL',
    'SELECT 1');
PREPARE legalsynq_selling_stmt FROM @legalsynq_selling_ddl;
EXECUTE legalsynq_selling_stmt;
DEALLOCATE PREPARE legalsynq_selling_stmt;

CREATE TABLE IF NOT EXISTS `liens_LegacyFieldMigrationStates` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `TenantId` char(36) COLLATE ascii_general_ci NOT NULL,
    `SourceSystem` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `SourceTable` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `LegacyId` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `MappingVersion` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `FieldGroup` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `TargetEntity` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `TargetId` char(36) COLLATE ascii_general_ci NOT NULL,
    `SourceHash` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
    `TargetPreimageHash` varchar(128) CHARACTER SET utf8mb4 NULL,
    `AppliedValueHash` varchar(128) CHARACTER SET utf8mb4 NULL,
    `Status` varchar(30) CHARACTER SET utf8mb4 NOT NULL,
    `ImportRunId` char(36) COLLATE ascii_general_ci NOT NULL,
    `AppliedAtUtc` datetime(6) NULL,
    `CreatedAtUtc` datetime(6) NOT NULL,
    CONSTRAINT `PK_liens_LegacyFieldMigrationStates` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_LegacyFieldMigrationStates_ImportRun`
        FOREIGN KEY (`ImportRunId`) REFERENCES `liens_LegacyImportRuns` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;

SET @legalsynq_selling_ddl = IF((
    SELECT COUNT(*) FROM information_schema.statistics
    WHERE table_schema = DATABASE() AND table_name = 'liens_Cases'
      AND index_name = 'IX_Cases_AttorneyContactPersonId') = 0,
    'CREATE INDEX `IX_Cases_AttorneyContactPersonId` ON `liens_Cases` (`AttorneyContactPersonId`)',
    'SELECT 1');
PREPARE legalsynq_selling_stmt FROM @legalsynq_selling_ddl;
EXECUTE legalsynq_selling_stmt;
DEALLOCATE PREPARE legalsynq_selling_stmt;

SET @legalsynq_selling_ddl = IF((
    SELECT COUNT(*) FROM information_schema.statistics
    WHERE table_schema = DATABASE() AND table_name = 'liens_LegacyFieldMigrationStates'
      AND index_name = 'IX_LegacyFieldMigrationStates_ImportRunId') = 0,
    'CREATE INDEX `IX_LegacyFieldMigrationStates_ImportRunId` ON `liens_LegacyFieldMigrationStates` (`ImportRunId`)',
    'SELECT 1');
PREPARE legalsynq_selling_stmt FROM @legalsynq_selling_ddl;
EXECUTE legalsynq_selling_stmt;
DEALLOCATE PREPARE legalsynq_selling_stmt;

SET @legalsynq_selling_ddl = IF((
    SELECT COUNT(*) FROM information_schema.statistics
    WHERE table_schema = DATABASE() AND table_name = 'liens_LegacyFieldMigrationStates'
      AND index_name = 'UX_LegacyFieldMigrationStates_Source_FieldGroup') = 0,
    'CREATE UNIQUE INDEX `UX_LegacyFieldMigrationStates_Source_FieldGroup` ON `liens_LegacyFieldMigrationStates` (`TenantId`, `SourceSystem`, `SourceTable`, `LegacyId`, `MappingVersion`, `FieldGroup`)',
    'SELECT 1');
PREPARE legalsynq_selling_stmt FROM @legalsynq_selling_ddl;
EXECUTE legalsynq_selling_stmt;
DEALLOCATE PREPARE legalsynq_selling_stmt;

SET @legalsynq_selling_ddl = IF((
    SELECT COUNT(*) FROM information_schema.table_constraints
    WHERE constraint_schema = DATABASE() AND table_name = 'liens_Cases'
      AND constraint_name = 'FK_Cases_AttorneyContactPerson'
      AND constraint_type = 'FOREIGN KEY') = 0,
    'ALTER TABLE `liens_Cases` ADD CONSTRAINT `FK_Cases_AttorneyContactPerson` FOREIGN KEY (`AttorneyContactPersonId`) REFERENCES `liens_CompanyContactPersons` (`Id`) ON DELETE RESTRICT',
    'SELECT 1');
PREPARE legalsynq_selling_stmt FROM @legalsynq_selling_ddl;
EXECUTE legalsynq_selling_stmt;
DEALLOCATE PREPARE legalsynq_selling_stmt;

SELECT
    @target_schema AS TargetSchema,
    (SELECT COUNT(*) FROM information_schema.columns
     WHERE table_schema = DATABASE() AND table_name = 'liens_Cases'
       AND column_name IN (
           'ClientAddressLine1', 'ClientCity', 'ClientState', 'ClientPostalCode',
           'IncidentState', 'CurrentMedicalStatus', 'TrackingFollowUpDate',
           'MinorComp', 'CaseDropped', 'ImportedCreatedByName')) AS ReportColumnsInstalled,
    (SELECT COUNT(*) FROM information_schema.tables
     WHERE table_schema = DATABASE()
       AND table_name = 'liens_LegacyFieldMigrationStates') AS MigrationLedgerInstalled;
