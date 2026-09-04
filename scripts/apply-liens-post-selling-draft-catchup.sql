-- SQL-only Liens migration catch-up for databases whose latest recorded
-- migration is 20260827100000_AddSellingCaseDraftConcurrencyToken.
--
-- Applies and reconciles, in order:
--   20260829120000_AddLegacyUpdateEvents
--   20260831010000_OptimizeCaseNoteReportQueries
--   20260831130318_AddSellingPortalMessageAttachments
--   20260902020000_ReserveCaseNumbers
--   20260902030000_ExpandLienStatusHistoryDescription
--   20260903010000_AddCaseUpdateHistory
--   20260904010000_AddContactPhoneExtension
--
-- Stop the Liens API and back up the database before running this script.
-- Run it against the Liens database, for example:
--   mysql --defaults-extra-file=/secure/liens.cnf liens < scripts/apply-liens-post-selling-draft-catchup.sql
--
-- Every DDL operation is restart-safe. A migration-history row is inserted
-- only after its live contract is valid and its immediate predecessor is
-- recorded. MigrationId comparisons use binary casts so different utf8mb4
-- collations on the session and __EFMigrationsHistory cannot conflict.

SET @catchup_base_present = EXISTS (
    SELECT 1
    FROM `__EFMigrationsHistory`
    WHERE CAST(`MigrationId` AS BINARY) =
          CAST('20260827100000_AddSellingCaseDraftConcurrencyToken' AS BINARY)
);

-- 20260829120000_AddLegacyUpdateEvents
CREATE TABLE IF NOT EXISTS `liens_LegacyUpdateEvents` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `TenantId` char(36) COLLATE ascii_general_ci NOT NULL,
    `OrgId` char(36) COLLATE ascii_general_ci NOT NULL,
    `CaseId` char(36) COLLATE ascii_general_ci NOT NULL,
    `LienId` char(36) COLLATE ascii_general_ci NULL,
    `Scope` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
    `Action` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `Description` text CHARACTER SET utf8mb4 NULL,
    `ActorDisplayName` varchar(255) CHARACTER SET utf8mb4 NULL,
    `OccurredAtUtc` datetime(6) NOT NULL,
    `ImportedAtUtc` datetime(6) NOT NULL,
    `ImportRunId` char(36) COLLATE ascii_general_ci NOT NULL,
    `SourceSystem` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `SourceTable` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `LegacyId` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `LegacySequence` bigint NOT NULL,
    CONSTRAINT `PK_liens_LegacyUpdateEvents` PRIMARY KEY (`Id`),
    CONSTRAINT `CK_LegacyUpdateEvents_Scope`
        CHECK (`Scope` IN ('Case', 'Lien')),
    CONSTRAINT `CK_LegacyUpdateEvents_ScopeLien`
        CHECK ((`Scope` = 'Case' AND `LienId` IS NULL)
            OR (`Scope` = 'Lien' AND `LienId` IS NOT NULL)),
    CONSTRAINT `FK_liens_LegacyUpdateEvents_liens_LegacyImportRuns_ImportRunId`
        FOREIGN KEY (`ImportRunId`) REFERENCES `liens_LegacyImportRuns` (`Id`) ON DELETE RESTRICT
) CHARACTER SET=utf8mb4;

SET @catchup_sql = IF(
    (SELECT COUNT(*) FROM information_schema.STATISTICS
     WHERE TABLE_SCHEMA = DATABASE()
       AND TABLE_NAME = 'liens_LegacyUpdateEvents'
       AND INDEX_NAME = 'IX_LegacyUpdateEvents_CaseTimeline') = 0,
    'CREATE INDEX `IX_LegacyUpdateEvents_CaseTimeline` ON `liens_LegacyUpdateEvents` (`TenantId`, `CaseId`, `Scope`, `OccurredAtUtc` DESC, `LegacySequence` DESC)',
    'SELECT 1');
PREPARE catchup_statement FROM @catchup_sql;
EXECUTE catchup_statement;
DEALLOCATE PREPARE catchup_statement;

SET @catchup_sql = IF(
    (SELECT COUNT(*) FROM information_schema.STATISTICS
     WHERE TABLE_SCHEMA = DATABASE()
       AND TABLE_NAME = 'liens_LegacyUpdateEvents'
       AND INDEX_NAME = 'IX_LegacyUpdateEvents_ImportRunId') = 0,
    'CREATE INDEX `IX_LegacyUpdateEvents_ImportRunId` ON `liens_LegacyUpdateEvents` (`ImportRunId`)',
    'SELECT 1');
PREPARE catchup_statement FROM @catchup_sql;
EXECUTE catchup_statement;
DEALLOCATE PREPARE catchup_statement;

SET @catchup_sql = IF(
    (SELECT COUNT(*) FROM information_schema.STATISTICS
     WHERE TABLE_SCHEMA = DATABASE()
       AND TABLE_NAME = 'liens_LegacyUpdateEvents'
       AND INDEX_NAME = 'IX_LegacyUpdateEvents_LienTimeline') = 0,
    'CREATE INDEX `IX_LegacyUpdateEvents_LienTimeline` ON `liens_LegacyUpdateEvents` (`TenantId`, `LienId`, `OccurredAtUtc` DESC, `LegacySequence` DESC)',
    'SELECT 1');
PREPARE catchup_statement FROM @catchup_sql;
EXECUTE catchup_statement;
DEALLOCATE PREPARE catchup_statement;

SET @catchup_sql = IF(
    (SELECT COUNT(*) FROM information_schema.STATISTICS
     WHERE TABLE_SCHEMA = DATABASE()
       AND TABLE_NAME = 'liens_LegacyUpdateEvents'
       AND INDEX_NAME = 'UX_LegacyUpdateEvents_Tenant_Source_Table_Key') = 0,
    'CREATE UNIQUE INDEX `UX_LegacyUpdateEvents_Tenant_Source_Table_Key` ON `liens_LegacyUpdateEvents` (`TenantId`, `SourceSystem`, `SourceTable`, `LegacyId`)',
    'SELECT 1');
PREPARE catchup_statement FROM @catchup_sql;
EXECUTE catchup_statement;
DEALLOCATE PREPARE catchup_statement;

SET @legacy_update_events_contract_valid =
    (SELECT COUNT(*) FROM information_schema.COLUMNS
     WHERE TABLE_SCHEMA = DATABASE()
       AND TABLE_NAME = 'liens_LegacyUpdateEvents'
       AND COLUMN_NAME IN (
           'Id', 'TenantId', 'OrgId', 'CaseId', 'LienId', 'Scope', 'Action',
           'Description', 'ActorDisplayName', 'OccurredAtUtc', 'ImportedAtUtc',
           'ImportRunId', 'SourceSystem', 'SourceTable', 'LegacyId', 'LegacySequence')) = 16
    AND
    (SELECT COUNT(DISTINCT INDEX_NAME) FROM information_schema.STATISTICS
     WHERE TABLE_SCHEMA = DATABASE()
       AND TABLE_NAME = 'liens_LegacyUpdateEvents'
       AND INDEX_NAME IN (
           'PRIMARY',
           'IX_LegacyUpdateEvents_CaseTimeline',
           'IX_LegacyUpdateEvents_ImportRunId',
           'IX_LegacyUpdateEvents_LienTimeline',
           'UX_LegacyUpdateEvents_Tenant_Source_Table_Key')) = 5
    AND
    (SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS
     WHERE CONSTRAINT_SCHEMA = DATABASE()
       AND TABLE_NAME = 'liens_LegacyUpdateEvents'
       AND CONSTRAINT_NAME IN (
           'PRIMARY',
           'CK_LegacyUpdateEvents_Scope',
           'CK_LegacyUpdateEvents_ScopeLien',
           'FK_liens_LegacyUpdateEvents_liens_LegacyImportRuns_ImportRunId')) = 4;

INSERT IGNORE INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
SELECT '20260829120000_AddLegacyUpdateEvents', '8.0.2'
WHERE @catchup_base_present = 1
  AND @legacy_update_events_contract_valid = 1;

-- 20260831010000_OptimizeCaseNoteReportQueries
UPDATE `liens_CaseNotes`
SET `Category` = CASE LOWER(TRIM(`Category`))
    WHEN 'general' THEN 'general'
    WHEN 'feed' THEN 'feed'
    WHEN 'internal' THEN 'internal'
    WHEN 'follow-up' THEN 'follow-up'
    WHEN 'case created' THEN 'Case Created'
    WHEN 'settlement history' THEN 'Settlement History'
    ELSE `Category`
END
WHERE LOWER(TRIM(`Category`)) IN (
    'general', 'feed', 'internal', 'follow-up',
    'case created', 'settlement history')
  AND BINARY `Category` <> BINARY CASE LOWER(TRIM(`Category`))
    WHEN 'general' THEN 'general'
    WHEN 'feed' THEN 'feed'
    WHEN 'internal' THEN 'internal'
    WHEN 'follow-up' THEN 'follow-up'
    WHEN 'case created' THEN 'Case Created'
    WHEN 'settlement history' THEN 'Settlement History'
    ELSE `Category`
  END;

SET @catchup_sql = IF(
    (SELECT COUNT(*) FROM information_schema.STATISTICS
     WHERE TABLE_SCHEMA = DATABASE()
       AND TABLE_NAME = 'liens_CaseNotes'
       AND INDEX_NAME = 'IX_CaseNotes_ReportLookup') = 0,
    'CREATE INDEX `IX_CaseNotes_ReportLookup` ON `liens_CaseNotes` (`TenantId`, `CaseId`, `IsDeleted`, `Category`, `CreatedAtUtc` DESC, `Id` DESC)',
    'SELECT 1');
PREPARE catchup_statement FROM @catchup_sql;
EXECUTE catchup_statement;
DEALLOCATE PREPARE catchup_statement;

SET @case_note_report_contract_valid =
    (SELECT COUNT(*) FROM information_schema.STATISTICS
     WHERE TABLE_SCHEMA = DATABASE()
       AND TABLE_NAME = 'liens_CaseNotes'
       AND INDEX_NAME = 'IX_CaseNotes_ReportLookup') = 6
    AND NOT EXISTS (
        SELECT 1
        FROM `liens_CaseNotes`
        WHERE LOWER(TRIM(`Category`)) IN (
            'general', 'feed', 'internal', 'follow-up',
            'case created', 'settlement history')
          AND BINARY `Category` <> BINARY CASE LOWER(TRIM(`Category`))
            WHEN 'general' THEN 'general'
            WHEN 'feed' THEN 'feed'
            WHEN 'internal' THEN 'internal'
            WHEN 'follow-up' THEN 'follow-up'
            WHEN 'case created' THEN 'Case Created'
            WHEN 'settlement history' THEN 'Settlement History'
            ELSE `Category`
          END
    );

INSERT IGNORE INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
SELECT '20260831010000_OptimizeCaseNoteReportQueries', '8.0.2'
WHERE @case_note_report_contract_valid = 1
  AND EXISTS (
      SELECT 1 FROM `__EFMigrationsHistory`
      WHERE CAST(`MigrationId` AS BINARY) =
            CAST('20260829120000_AddLegacyUpdateEvents' AS BINARY));

-- 20260831130318_AddSellingPortalMessageAttachments
CREATE TABLE IF NOT EXISTS `liens_SellingPortalMessageAttachments` (
    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
    `TenantId` char(36) COLLATE ascii_general_ci NOT NULL,
    `LienId` char(36) COLLATE ascii_general_ci NOT NULL,
    `SellerOrgId` char(36) COLLATE ascii_general_ci NOT NULL,
    `BuyerOrgId` char(36) COLLATE ascii_general_ci NOT NULL,
    `BuyerContactId` char(36) COLLATE ascii_general_ci NOT NULL,
    `AccessLinkId` char(36) COLLATE ascii_general_ci NOT NULL,
    `MessageId` char(36) COLLATE ascii_general_ci NOT NULL,
    `DocumentId` char(36) COLLATE ascii_general_ci NOT NULL,
    `FileName` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
    `ContentType` varchar(160) CHARACTER SET utf8mb4 NOT NULL,
    `FileSizeBytes` bigint NOT NULL,
    `CreatedAtUtc` datetime(6) NOT NULL,
    `UpdatedAtUtc` datetime(6) NOT NULL,
    `CreatedByUserId` char(36) COLLATE ascii_general_ci NOT NULL,
    `UpdatedByUserId` char(36) COLLATE ascii_general_ci NULL,
    CONSTRAINT `PK_liens_SellingPortalMessageAttachments` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_liens_SellingPortalMessageAttachments_liens_Liens_LienId`
        FOREIGN KEY (`LienId`) REFERENCES `liens_Liens` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_liens_SellingPortalMessageAttachments_liens_SellingBuyerAcce~`
        FOREIGN KEY (`AccessLinkId`) REFERENCES `liens_SellingBuyerAccessLinks` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_liens_SellingPortalMessageAttachments_liens_SellingPortalMes~`
        FOREIGN KEY (`MessageId`) REFERENCES `liens_SellingPortalMessages` (`Id`) ON DELETE CASCADE
) CHARACTER SET=utf8mb4;

SET @catchup_sql = IF(
    (SELECT COUNT(*) FROM information_schema.STATISTICS
     WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'liens_SellingPortalMessageAttachments'
       AND INDEX_NAME = 'IX_liens_SellingPortalMessageAttachments_AccessLinkId') = 0,
    'CREATE INDEX `IX_liens_SellingPortalMessageAttachments_AccessLinkId` ON `liens_SellingPortalMessageAttachments` (`AccessLinkId`)',
    'SELECT 1');
PREPARE catchup_statement FROM @catchup_sql;
EXECUTE catchup_statement;
DEALLOCATE PREPARE catchup_statement;

SET @catchup_sql = IF(
    (SELECT COUNT(*) FROM information_schema.STATISTICS
     WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'liens_SellingPortalMessageAttachments'
       AND INDEX_NAME = 'IX_liens_SellingPortalMessageAttachments_LienId') = 0,
    'CREATE INDEX `IX_liens_SellingPortalMessageAttachments_LienId` ON `liens_SellingPortalMessageAttachments` (`LienId`)',
    'SELECT 1');
PREPARE catchup_statement FROM @catchup_sql;
EXECUTE catchup_statement;
DEALLOCATE PREPARE catchup_statement;

SET @catchup_sql = IF(
    (SELECT COUNT(*) FROM information_schema.STATISTICS
     WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'liens_SellingPortalMessageAttachments'
       AND INDEX_NAME = 'IX_liens_SellingPortalMessageAttachments_MessageId') = 0,
    'CREATE INDEX `IX_liens_SellingPortalMessageAttachments_MessageId` ON `liens_SellingPortalMessageAttachments` (`MessageId`)',
    'SELECT 1');
PREPARE catchup_statement FROM @catchup_sql;
EXECUTE catchup_statement;
DEALLOCATE PREPARE catchup_statement;

SET @catchup_sql = IF(
    (SELECT COUNT(*) FROM information_schema.STATISTICS
     WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'liens_SellingPortalMessageAttachments'
       AND INDEX_NAME = 'IX_SellingPortalMessageAttachments_Tenant_Document') = 0,
    'CREATE INDEX `IX_SellingPortalMessageAttachments_Tenant_Document` ON `liens_SellingPortalMessageAttachments` (`TenantId`, `DocumentId`)',
    'SELECT 1');
PREPARE catchup_statement FROM @catchup_sql;
EXECUTE catchup_statement;
DEALLOCATE PREPARE catchup_statement;

SET @catchup_sql = IF(
    (SELECT COUNT(*) FROM information_schema.STATISTICS
     WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'liens_SellingPortalMessageAttachments'
       AND INDEX_NAME = 'IX_SellingPortalMessageAttachments_Tenant_Lien_Participants') = 0,
    'CREATE INDEX `IX_SellingPortalMessageAttachments_Tenant_Lien_Participants` ON `liens_SellingPortalMessageAttachments` (`TenantId`, `LienId`, `SellerOrgId`, `BuyerOrgId`, `BuyerContactId`)',
    'SELECT 1');
PREPARE catchup_statement FROM @catchup_sql;
EXECUTE catchup_statement;
DEALLOCATE PREPARE catchup_statement;

SET @catchup_sql = IF(
    (SELECT COUNT(*) FROM information_schema.STATISTICS
     WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'liens_SellingPortalMessageAttachments'
       AND INDEX_NAME = 'IX_SellingPortalMessageAttachments_Tenant_Message_Created') = 0,
    'CREATE INDEX `IX_SellingPortalMessageAttachments_Tenant_Message_Created` ON `liens_SellingPortalMessageAttachments` (`TenantId`, `MessageId`, `CreatedAtUtc`)',
    'SELECT 1');
PREPARE catchup_statement FROM @catchup_sql;
EXECUTE catchup_statement;
DEALLOCATE PREPARE catchup_statement;

SET @message_attachments_contract_valid =
    (SELECT COUNT(*) FROM information_schema.COLUMNS
     WHERE TABLE_SCHEMA = DATABASE()
       AND TABLE_NAME = 'liens_SellingPortalMessageAttachments'
       AND COLUMN_NAME IN (
           'Id', 'TenantId', 'LienId', 'SellerOrgId', 'BuyerOrgId',
           'BuyerContactId', 'AccessLinkId', 'MessageId', 'DocumentId',
           'FileName', 'ContentType', 'FileSizeBytes', 'CreatedAtUtc',
           'UpdatedAtUtc', 'CreatedByUserId', 'UpdatedByUserId')) = 16
    AND
    (SELECT COUNT(DISTINCT INDEX_NAME) FROM information_schema.STATISTICS
     WHERE TABLE_SCHEMA = DATABASE()
       AND TABLE_NAME = 'liens_SellingPortalMessageAttachments'
       AND INDEX_NAME IN (
           'PRIMARY',
           'IX_liens_SellingPortalMessageAttachments_AccessLinkId',
           'IX_liens_SellingPortalMessageAttachments_LienId',
           'IX_liens_SellingPortalMessageAttachments_MessageId',
           'IX_SellingPortalMessageAttachments_Tenant_Document',
           'IX_SellingPortalMessageAttachments_Tenant_Lien_Participants',
           'IX_SellingPortalMessageAttachments_Tenant_Message_Created')) = 7
    AND
    (SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS
     WHERE CONSTRAINT_SCHEMA = DATABASE()
       AND TABLE_NAME = 'liens_SellingPortalMessageAttachments'
       AND CONSTRAINT_TYPE = 'FOREIGN KEY'
       AND CONSTRAINT_NAME IN (
           'FK_liens_SellingPortalMessageAttachments_liens_Liens_LienId',
           'FK_liens_SellingPortalMessageAttachments_liens_SellingBuyerAcce~',
           'FK_liens_SellingPortalMessageAttachments_liens_SellingPortalMes~')) = 3;

INSERT IGNORE INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
SELECT '20260831130318_AddSellingPortalMessageAttachments', '8.0.2'
WHERE @message_attachments_contract_valid = 1
  AND EXISTS (
      SELECT 1 FROM `__EFMigrationsHistory`
      WHERE CAST(`MigrationId` AS BINARY) =
            CAST('20260831010000_OptimizeCaseNoteReportQueries' AS BINARY));

-- 20260902020000_ReserveCaseNumbers
CREATE TABLE IF NOT EXISTS `liens_CaseNumberReservations` (
    `TenantId` char(36) COLLATE ascii_general_ci NOT NULL,
    `CaseNumber` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `ReservedAtUtc` datetime(6) NOT NULL,
    CONSTRAINT `PK_liens_CaseNumberReservations` PRIMARY KEY (`TenantId`, `CaseNumber`)
) CHARACTER SET=utf8mb4;

START TRANSACTION;

INSERT IGNORE INTO `liens_CaseNumberReservations`
    (`TenantId`, `CaseNumber`, `ReservedAtUtc`)
SELECT `TenantId`, `CaseNumber`, `CreatedAtUtc`
FROM `liens_Cases`
WHERE `CaseNumber` <> '';

INSERT IGNORE INTO `liens_CaseNumberReservations`
    (`TenantId`, `CaseNumber`, `ReservedAtUtc`)
SELECT `TenantId`, SUBSTRING_INDEX(`LienNumber`, '-', 2), MIN(`CreatedAtUtc`)
FROM `liens_Liens`
WHERE `LienNumber` REGEXP '^[0-9]{2}-[0-9]{5,6}-[0-9]+$'
GROUP BY `TenantId`, SUBSTRING_INDEX(`LienNumber`, '-', 2);

COMMIT;

SET @reservation_contract_valid =
    (SELECT COUNT(*) FROM information_schema.COLUMNS
     WHERE TABLE_SCHEMA = DATABASE()
       AND TABLE_NAME = 'liens_CaseNumberReservations'
       AND COLUMN_NAME IN ('TenantId', 'CaseNumber', 'ReservedAtUtc')) = 3
    AND
    (SELECT COUNT(*) FROM information_schema.KEY_COLUMN_USAGE
     WHERE CONSTRAINT_SCHEMA = DATABASE()
       AND TABLE_NAME = 'liens_CaseNumberReservations'
       AND CONSTRAINT_NAME = 'PRIMARY') = 2
    AND NOT EXISTS (
        SELECT 1 FROM `liens_Cases` cases
        LEFT JOIN `liens_CaseNumberReservations` reservations
          ON reservations.`TenantId` = cases.`TenantId`
         AND reservations.`CaseNumber` = cases.`CaseNumber`
        WHERE cases.`CaseNumber` <> '' AND reservations.`TenantId` IS NULL)
    AND NOT EXISTS (
        SELECT 1
        FROM (
            SELECT `TenantId`, SUBSTRING_INDEX(`LienNumber`, '-', 2) AS `CaseNumber`
            FROM `liens_Liens`
            WHERE `LienNumber` REGEXP '^[0-9]{2}-[0-9]{5,6}-[0-9]+$'
            GROUP BY `TenantId`, SUBSTRING_INDEX(`LienNumber`, '-', 2)
        ) legacy
        LEFT JOIN `liens_CaseNumberReservations` reservations
          ON reservations.`TenantId` = legacy.`TenantId`
         AND reservations.`CaseNumber` = legacy.`CaseNumber`
        WHERE reservations.`TenantId` IS NULL);

INSERT IGNORE INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
SELECT '20260902020000_ReserveCaseNumbers', '8.0.2'
WHERE @reservation_contract_valid = 1
  AND EXISTS (
      SELECT 1 FROM `__EFMigrationsHistory`
      WHERE CAST(`MigrationId` AS BINARY) =
            CAST('20260831130318_AddSellingPortalMessageAttachments' AS BINARY));

-- 20260902030000_ExpandLienStatusHistoryDescription
ALTER TABLE `liens_LienStatusHistory`
    MODIFY COLUMN `Description` text CHARACTER SET utf8mb4 NOT NULL;

SET @lien_history_contract_valid = EXISTS (
    SELECT 1 FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'liens_LienStatusHistory'
      AND COLUMN_NAME = 'Description'
      AND DATA_TYPE = 'text'
      AND IS_NULLABLE = 'NO');

INSERT IGNORE INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
SELECT '20260902030000_ExpandLienStatusHistoryDescription', '8.0.2'
WHERE @lien_history_contract_valid = 1
  AND EXISTS (
      SELECT 1 FROM `__EFMigrationsHistory`
      WHERE CAST(`MigrationId` AS BINARY) =
            CAST('20260902020000_ReserveCaseNumbers' AS BINARY));

-- 20260903010000_AddCaseUpdateHistory
CREATE TABLE IF NOT EXISTS `liens_CaseUpdateHistory` (
    `Id` char(36) NOT NULL,
    `TenantId` char(36) NOT NULL,
    `CaseId` char(36) NOT NULL,
    `Action` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
    `Description` text CHARACTER SET utf8mb4 NOT NULL,
    `ActorUserId` char(36) NOT NULL,
    `OccurredAtUtc` datetime(6) NOT NULL,
    `CreatedByUserId` char(36) NOT NULL,
    `UpdatedByUserId` char(36) NULL,
    `CreatedAtUtc` datetime(6) NOT NULL,
    `UpdatedAtUtc` datetime(6) NOT NULL,
    CONSTRAINT `PK_liens_CaseUpdateHistory` PRIMARY KEY (`Id`)
) CHARACTER SET=utf8mb4;

SET @catchup_sql = IF(
    (SELECT COUNT(*) FROM information_schema.STATISTICS
     WHERE TABLE_SCHEMA = DATABASE()
       AND TABLE_NAME = 'liens_CaseUpdateHistory'
       AND INDEX_NAME = 'IX_CaseUpdateHistory_TenantId_CaseId_OccurredAtUtc') = 0,
    'CREATE INDEX `IX_CaseUpdateHistory_TenantId_CaseId_OccurredAtUtc` ON `liens_CaseUpdateHistory` (`TenantId`, `CaseId`, `OccurredAtUtc`)',
    'SELECT 1');
PREPARE catchup_statement FROM @catchup_sql;
EXECUTE catchup_statement;
DEALLOCATE PREPARE catchup_statement;

SET @case_history_contract_valid =
    (SELECT COUNT(*) FROM information_schema.COLUMNS
     WHERE TABLE_SCHEMA = DATABASE()
       AND TABLE_NAME = 'liens_CaseUpdateHistory'
       AND COLUMN_NAME IN (
           'Id', 'TenantId', 'CaseId', 'Action', 'Description', 'ActorUserId',
           'OccurredAtUtc', 'CreatedByUserId', 'UpdatedByUserId',
           'CreatedAtUtc', 'UpdatedAtUtc')) = 11
    AND
    (SELECT COUNT(*) FROM information_schema.STATISTICS
     WHERE TABLE_SCHEMA = DATABASE()
       AND TABLE_NAME = 'liens_CaseUpdateHistory'
       AND INDEX_NAME = 'IX_CaseUpdateHistory_TenantId_CaseId_OccurredAtUtc') = 3;

INSERT IGNORE INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
SELECT '20260903010000_AddCaseUpdateHistory', '8.0.2'
WHERE @case_history_contract_valid = 1
  AND EXISTS (
      SELECT 1 FROM `__EFMigrationsHistory`
      WHERE CAST(`MigrationId` AS BINARY) =
            CAST('20260902030000_ExpandLienStatusHistoryDescription' AS BINARY));

-- 20260904010000_AddContactPhoneExtension
SET @catchup_sql = IF(
    (SELECT COUNT(*) FROM information_schema.COLUMNS
     WHERE TABLE_SCHEMA = DATABASE()
       AND TABLE_NAME = 'liens_Contacts'
       AND COLUMN_NAME = 'PhoneExtension') = 0,
    'ALTER TABLE `liens_Contacts` ADD COLUMN `PhoneExtension` varchar(20) CHARACTER SET utf8mb4 NULL',
    'SELECT 1');
PREPARE catchup_statement FROM @catchup_sql;
EXECUTE catchup_statement;
DEALLOCATE PREPARE catchup_statement;

SET @contact_phone_extension_contract_valid = EXISTS (
    SELECT 1 FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'liens_Contacts'
      AND COLUMN_NAME = 'PhoneExtension'
      AND COLUMN_TYPE = 'varchar(20)'
      AND IS_NULLABLE = 'YES'
      AND CHARACTER_SET_NAME = 'utf8mb4');

INSERT IGNORE INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
SELECT '20260904010000_AddContactPhoneExtension', '8.0.2'
WHERE @contact_phone_extension_contract_valid = 1
  AND EXISTS (
      SELECT 1 FROM `__EFMigrationsHistory`
      WHERE CAST(`MigrationId` AS BINARY) =
            CAST('20260903010000_AddCaseUpdateHistory' AS BINARY));

-- Every row must report READY before the Liens API is restarted.
SELECT
    expected.`MigrationId`,
    IF(history.`MigrationId` IS NULL, 'NOT_RECORDED', 'RECORDED') AS `HistoryStatus`,
    IF(expected.`ContractValid` = 1, 'VALID', 'INVALID') AS `ContractStatus`,
    IF(history.`MigrationId` IS NOT NULL AND expected.`ContractValid` = 1,
       'READY', 'NOT_READY') AS `Status`
FROM (
    SELECT '20260829120000_AddLegacyUpdateEvents' AS `MigrationId`,
           @legacy_update_events_contract_valid AS `ContractValid`
    UNION ALL
    SELECT '20260831010000_OptimizeCaseNoteReportQueries',
           @case_note_report_contract_valid
    UNION ALL
    SELECT '20260831130318_AddSellingPortalMessageAttachments',
           @message_attachments_contract_valid
    UNION ALL
    SELECT '20260902020000_ReserveCaseNumbers',
           @reservation_contract_valid
    UNION ALL
    SELECT '20260902030000_ExpandLienStatusHistoryDescription',
           @lien_history_contract_valid
    UNION ALL
    SELECT '20260903010000_AddCaseUpdateHistory',
           @case_history_contract_valid
    UNION ALL
    SELECT '20260904010000_AddContactPhoneExtension',
           @contact_phone_extension_contract_valid
) expected
LEFT JOIN `__EFMigrationsHistory` history
  ON CAST(history.`MigrationId` AS BINARY) = CAST(expected.`MigrationId` AS BINARY)
ORDER BY expected.`MigrationId`;
