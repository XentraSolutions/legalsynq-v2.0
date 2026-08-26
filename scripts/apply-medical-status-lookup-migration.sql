-- Repairs MedicalStatus lookup data when migration
-- 20260825170000_BackfillMedicalStatusLookupValues is unavailable or was
-- recorded without applying its data changes.
--
-- Run against the Liens database. This script is idempotent and does not
-- record EF migration history; leave the application migration in place so
-- future deployments remain consistent.

START TRANSACTION;

-- These global values are visible to every tenant through /lookup/all.
-- Keep these as individual inserts for compatibility with the QA MySQL server.
INSERT INTO `liens_LookupValues`
    (`Id`, `TenantId`, `Category`, `Code`, `Name`, `Description`,
     `SortOrder`, `IsActive`, `IsSystem`,
     `CreatedByUserId`, `UpdatedByUserId`, `CreatedAtUtc`, `UpdatedAtUtc`)
SELECT UUID(), NULL, 'MedicalStatus', 'TREATING', 'Plaintiff Treating', NULL,
       1, 1, 1, '00000000-0000-0000-0000-000000000001', NULL,
       UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
WHERE NOT EXISTS (
    SELECT 1
    FROM `liens_LookupValues`
    WHERE `TenantId` IS NULL
      AND `Category` = 'MedicalStatus'
      AND `Code` = 'TREATING'
);

INSERT INTO `liens_LookupValues`
    (`Id`, `TenantId`, `Category`, `Code`, `Name`, `Description`,
     `SortOrder`, `IsActive`, `IsSystem`,
     `CreatedByUserId`, `UpdatedByUserId`, `CreatedAtUtc`, `UpdatedAtUtc`)
SELECT UUID(), NULL, 'MedicalStatus', 'DONE_TREATING', 'Plaintiff Done Treating', NULL,
       2, 1, 1, '00000000-0000-0000-0000-000000000001', NULL,
       UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
WHERE NOT EXISTS (
    SELECT 1
    FROM `liens_LookupValues`
    WHERE `TenantId` IS NULL
      AND `Category` = 'MedicalStatus'
      AND `Code` = 'DONE_TREATING'
);

INSERT INTO `liens_LookupValues`
    (`Id`, `TenantId`, `Category`, `Code`, `Name`, `Description`,
     `SortOrder`, `IsActive`, `IsSystem`,
     `CreatedByUserId`, `UpdatedByUserId`, `CreatedAtUtc`, `UpdatedAtUtc`)
SELECT UUID(), NULL, 'MedicalStatus', 'UNKNOWN', 'Unknown', NULL,
       3, 1, 1, '00000000-0000-0000-0000-000000000001', NULL,
       UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
WHERE NOT EXISTS (
    SELECT 1
    FROM `liens_LookupValues`
    WHERE `TenantId` IS NULL
      AND `Category` = 'MedicalStatus'
      AND `Code` = 'UNKNOWN'
);

-- Preserve any distinct medical-status values already used by tenant cases.
-- Older environments may not yet have CurrentMedicalStatus, so skip this
-- backfill safely when that schema column is absent.
SET @medical_status_backfill_sql = IF(
    (SELECT COUNT(*)
     FROM information_schema.COLUMNS
     WHERE TABLE_SCHEMA = DATABASE()
       AND TABLE_NAME = 'liens_Cases'
       AND COLUMN_NAME = 'CurrentMedicalStatus') = 1,
    'INSERT INTO `liens_LookupValues`
        (`Id`, `TenantId`, `Category`, `Code`, `Name`, `Description`,
         `SortOrder`, `IsActive`, `IsSystem`,
         `CreatedByUserId`, `UpdatedByUserId`, `CreatedAtUtc`, `UpdatedAtUtc`)
     SELECT UUID(), source.`TenantId`, ''MedicalStatus'', source.`CurrentMedicalStatus`, source.`CurrentMedicalStatus`, NULL,
            100, 1, 0, ''00000000-0000-0000-0000-000000000001'', NULL,
            UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
     FROM (
         SELECT DISTINCT `TenantId`, TRIM(`CurrentMedicalStatus`) AS `CurrentMedicalStatus`
         FROM `liens_Cases`
         WHERE `CurrentMedicalStatus` IS NOT NULL
           AND TRIM(`CurrentMedicalStatus`) <> ''''
     ) AS source
     WHERE NOT EXISTS (
         SELECT 1
         FROM `liens_LookupValues` existing
         WHERE existing.`TenantId` = source.`TenantId`
           AND existing.`Category` = ''MedicalStatus''
           AND existing.`Code` = source.`CurrentMedicalStatus`
     )',
    'SELECT 1');

PREPARE medical_status_backfill_statement FROM @medical_status_backfill_sql;
EXECUTE medical_status_backfill_statement;
DEALLOCATE PREPARE medical_status_backfill_statement;

COMMIT;

SELECT `Id`, `TenantId`, `Code`, `Name`, `IsActive`, `IsSystem`
FROM `liens_LookupValues`
WHERE `Category` = 'MedicalStatus'
ORDER BY `TenantId`, `SortOrder`, `Name`;
