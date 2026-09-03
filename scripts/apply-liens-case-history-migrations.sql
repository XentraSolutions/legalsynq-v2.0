-- Manual recovery for the Liens case-number and native-history migrations:
--   20260902020000_ReserveCaseNumbers
--   20260902030000_ExpandLienStatusHistoryDescription
--   20260903010000_AddCaseUpdateHistory
--
-- Run this against the Liens database while the Liens API is stopped. The
-- schema and backfill operations are idempotent. Each EF migration-history row
-- is inserted only after its schema/data contract and the preceding migration
-- history have been verified.

-- 20260902020000_ReserveCaseNumbers
CREATE TABLE IF NOT EXISTS `liens_CaseNumberReservations` (
    `TenantId` char(36) COLLATE ascii_general_ci NOT NULL,
    `CaseNumber` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
    `ReservedAtUtc` datetime(6) NOT NULL,
    CONSTRAINT `PK_liens_CaseNumberReservations`
        PRIMARY KEY (`TenantId`, `CaseNumber`)
) CHARACTER SET=utf8mb4;

START TRANSACTION;

INSERT IGNORE INTO `liens_CaseNumberReservations`
    (`TenantId`, `CaseNumber`, `ReservedAtUtc`)
SELECT `TenantId`, `CaseNumber`, `CreatedAtUtc`
FROM `liens_Cases`
WHERE `CaseNumber` <> '';

-- Deleted legacy cases may only be represented by retained lien numbers.
INSERT IGNORE INTO `liens_CaseNumberReservations`
    (`TenantId`, `CaseNumber`, `ReservedAtUtc`)
SELECT
    `TenantId`,
    SUBSTRING_INDEX(`LienNumber`, '-', 2),
    MIN(`CreatedAtUtc`)
FROM `liens_Liens`
WHERE `LienNumber` REGEXP '^[0-9]{2}-[0-9]{5,6}-[0-9]+$'
GROUP BY `TenantId`, SUBSTRING_INDEX(`LienNumber`, '-', 2);

COMMIT;

-- 20260902030000_ExpandLienStatusHistoryDescription
ALTER TABLE `liens_LienStatusHistory`
    MODIFY COLUMN `Description` text CHARACTER SET utf8mb4 NOT NULL;

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

SET @case_history_index_sql = IF(
    (SELECT COUNT(*)
     FROM information_schema.statistics
     WHERE table_schema = DATABASE()
       AND table_name = 'liens_CaseUpdateHistory'
       AND index_name = 'IX_CaseUpdateHistory_TenantId_CaseId_OccurredAtUtc') = 0,
    'CREATE INDEX `IX_CaseUpdateHistory_TenantId_CaseId_OccurredAtUtc` ON `liens_CaseUpdateHistory` (`TenantId`, `CaseId`, `OccurredAtUtc`)',
    'SELECT 1');
PREPARE case_history_index_statement FROM @case_history_index_sql;
EXECUTE case_history_index_statement;
DEALLOCATE PREPARE case_history_index_statement;

-- Record each migration only when its immediate predecessor is present and
-- the manually applied contract has been verified. If the preceding migration
-- is missing, repair that older migration chain before restarting the API.
SET @reservation_contract_valid = (
    SELECT COUNT(*)
    FROM information_schema.columns
    WHERE table_schema = DATABASE()
      AND table_name = 'liens_CaseNumberReservations'
      AND (
          (`column_name` = 'TenantId' AND `column_type` = 'char(36)' AND `is_nullable` = 'NO')
          OR (`column_name` = 'CaseNumber' AND `column_type` = 'varchar(50)' AND `is_nullable` = 'NO')
          OR (`column_name` = 'ReservedAtUtc' AND `column_type` = 'datetime(6)' AND `is_nullable` = 'NO')
      )
) = 3 AND (
    SELECT COUNT(*)
    FROM information_schema.key_column_usage
    WHERE constraint_schema = DATABASE()
      AND table_name = 'liens_CaseNumberReservations'
      AND constraint_name = 'PRIMARY'
      AND ((column_name = 'TenantId' AND ordinal_position = 1)
        OR (column_name = 'CaseNumber' AND ordinal_position = 2))
) = 2
AND NOT EXISTS (
    SELECT 1
    FROM `liens_Cases` AS cases
    LEFT JOIN `liens_CaseNumberReservations` AS reservations
        ON reservations.`TenantId` = cases.`TenantId`
       AND reservations.`CaseNumber` = cases.`CaseNumber`
    WHERE cases.`CaseNumber` <> ''
      AND reservations.`TenantId` IS NULL
)
AND NOT EXISTS (
    SELECT 1
    FROM (
        SELECT
            `TenantId`,
            SUBSTRING_INDEX(`LienNumber`, '-', 2) AS `CaseNumber`
        FROM `liens_Liens`
        WHERE `LienNumber` REGEXP '^[0-9]{2}-[0-9]{5,6}-[0-9]+$'
        GROUP BY `TenantId`, SUBSTRING_INDEX(`LienNumber`, '-', 2)
    ) AS legacy
    LEFT JOIN `liens_CaseNumberReservations` AS reservations
        ON reservations.`TenantId` = legacy.`TenantId`
       AND reservations.`CaseNumber` = legacy.`CaseNumber`
    WHERE reservations.`TenantId` IS NULL
);

SET @lien_history_contract_valid = EXISTS (
    SELECT 1
    FROM information_schema.columns
    WHERE table_schema = DATABASE()
      AND table_name = 'liens_LienStatusHistory'
      AND column_name = 'Description'
      AND data_type = 'text'
      AND is_nullable = 'NO'
);

SET @case_history_contract_valid = (
    SELECT COUNT(*)
    FROM information_schema.columns
    WHERE table_schema = DATABASE()
      AND table_name = 'liens_CaseUpdateHistory'
      AND (
          (`column_name` = 'Id' AND `column_type` = 'char(36)' AND `is_nullable` = 'NO')
          OR (`column_name` = 'TenantId' AND `column_type` = 'char(36)' AND `is_nullable` = 'NO')
          OR (`column_name` = 'CaseId' AND `column_type` = 'char(36)' AND `is_nullable` = 'NO')
          OR (`column_name` = 'Action' AND `column_type` = 'varchar(100)' AND `is_nullable` = 'NO')
          OR (`column_name` = 'Description' AND `data_type` = 'text' AND `is_nullable` = 'NO')
          OR (`column_name` = 'ActorUserId' AND `column_type` = 'char(36)' AND `is_nullable` = 'NO')
          OR (`column_name` = 'OccurredAtUtc' AND `column_type` = 'datetime(6)' AND `is_nullable` = 'NO')
          OR (`column_name` = 'CreatedByUserId' AND `column_type` = 'char(36)' AND `is_nullable` = 'NO')
          OR (`column_name` = 'UpdatedByUserId' AND `column_type` = 'char(36)' AND `is_nullable` = 'YES')
          OR (`column_name` = 'CreatedAtUtc' AND `column_type` = 'datetime(6)' AND `is_nullable` = 'NO')
          OR (`column_name` = 'UpdatedAtUtc' AND `column_type` = 'datetime(6)' AND `is_nullable` = 'NO')
      )
) = 11
AND EXISTS (
    SELECT 1
    FROM information_schema.key_column_usage
    WHERE constraint_schema = DATABASE()
      AND table_name = 'liens_CaseUpdateHistory'
      AND constraint_name = 'PRIMARY'
      AND column_name = 'Id'
      AND ordinal_position = 1
)
AND (
    SELECT COUNT(*)
    FROM information_schema.statistics
    WHERE table_schema = DATABASE()
      AND table_name = 'liens_CaseUpdateHistory'
      AND index_name = 'IX_CaseUpdateHistory_TenantId_CaseId_OccurredAtUtc'
      AND non_unique = 1
      AND ((column_name = 'TenantId' AND seq_in_index = 1)
        OR (column_name = 'CaseId' AND seq_in_index = 2)
        OR (column_name = 'OccurredAtUtc' AND seq_in_index = 3))
) = 3;

INSERT IGNORE INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
SELECT '20260902020000_ReserveCaseNumbers', '8.0.2'
WHERE @reservation_contract_valid = 1
  AND EXISTS (
      SELECT 1
      FROM `__EFMigrationsHistory`
      WHERE `MigrationId` = '20260831130318_AddSellingPortalMessageAttachments'
  );

INSERT IGNORE INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
SELECT '20260902030000_ExpandLienStatusHistoryDescription', '8.0.2'
WHERE @lien_history_contract_valid = 1
  AND EXISTS (
      SELECT 1
      FROM `__EFMigrationsHistory`
      WHERE `MigrationId` = '20260902020000_ReserveCaseNumbers'
  );

INSERT IGNORE INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
SELECT '20260903010000_AddCaseUpdateHistory', '8.0.2'
WHERE @case_history_contract_valid = 1
  AND EXISTS (
      SELECT 1
      FROM `__EFMigrationsHistory`
      WHERE `MigrationId` = '20260902030000_ExpandLienStatusHistoryDescription'
  );

-- All three rows must report READY before the Liens API is restarted. This
-- checks both EF history and the live contract, including environments where
-- history was previously recorded without its DDL.
SELECT expected.`MigrationId`,
       IF(history.`MigrationId` IS NULL, 'NOT_RECORDED', 'RECORDED') AS `HistoryStatus`,
       IF(expected.`ContractValid` = 1, 'VALID', 'INVALID') AS `ContractStatus`,
       IF(history.`MigrationId` IS NOT NULL AND expected.`ContractValid` = 1,
          'READY', 'NOT_READY') AS `Status`
FROM (
    SELECT '20260902020000_ReserveCaseNumbers' AS `MigrationId`,
           @reservation_contract_valid AS `ContractValid`
    UNION ALL
    SELECT '20260902030000_ExpandLienStatusHistoryDescription',
           @lien_history_contract_valid
    UNION ALL
    SELECT '20260903010000_AddCaseUpdateHistory',
           @case_history_contract_valid
) AS expected
LEFT JOIN `__EFMigrationsHistory` AS history
    ON history.`MigrationId` = expected.`MigrationId`
ORDER BY expected.`MigrationId`;

SELECT COUNT(*) AS `ReservationCount`
FROM `liens_CaseNumberReservations`;

SELECT COUNT(*) AS `CaseUpdateHistoryCount`
FROM `liens_CaseUpdateHistory`;
