-- ============================================================================
-- Add Synq Selling to the Identity product catalog.
--
-- Run this script against the Identity database. It is idempotent: rerunning it
-- keeps the existing product ID and refreshes the catalog metadata.
--
-- Example:
--   mysql --defaults-extra-file=/secure/identity.cnf legalsynq_identity \
--     < scripts/add-synq-selling-product.sql
-- ============================================================================

START TRANSACTION;

INSERT INTO `idt_Products`
    (`Id`, `Name`, `Code`, `Description`, `IsActive`, `CreatedAtUtc`)
VALUES
    (UUID(), 'SynqSelling', 'SYNQ_SELLING',
     'Portfolio sales, buyer engagement, and transaction workflows', 1, UTC_TIMESTAMP(6))
ON DUPLICATE KEY UPDATE
    `Name` = VALUES(`Name`),
    `Description` = VALUES(`Description`),
    `IsActive` = VALUES(`IsActive`);

COMMIT;

SELECT `Id`, `Name`, `Code`, `Description`, `IsActive`, `CreatedAtUtc`
FROM `idt_Products`
WHERE `Code` = 'SYNQ_SELLING';
