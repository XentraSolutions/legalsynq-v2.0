-- Emergency recovery for environments where the Liens migration chain is
-- blocked before 20260902020000_ReserveCaseNumbers.
--
-- Run this against the Liens database. The script is idempotent: it creates
-- the reservation table when absent and backfills historical case numbers
-- with INSERT IGNORE. It intentionally does not modify __EFMigrationsHistory;
-- the blocked EF migrations must still be repaired and applied normally.

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

-- All three counts should be zero except ReservationCount, which reports the
-- number of case numbers now protected from reuse.
SELECT COUNT(*) AS `ReservationCount`
FROM `liens_CaseNumberReservations`;

SELECT COUNT(*) AS `UnreservedCaseCount`
FROM `liens_Cases` AS cases
LEFT JOIN `liens_CaseNumberReservations` AS reservations
    ON reservations.`TenantId` = cases.`TenantId`
   AND reservations.`CaseNumber` = cases.`CaseNumber`
WHERE cases.`CaseNumber` <> ''
  AND reservations.`TenantId` IS NULL;

SELECT COUNT(*) AS `UnreservedLegacyLienCaseNumberCount`
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
WHERE reservations.`TenantId` IS NULL;
