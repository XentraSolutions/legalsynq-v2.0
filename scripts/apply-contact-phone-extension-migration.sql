-- Manual application for Liens migration:
--   20260904010000_AddContactPhoneExtension
--
-- Stop the Liens API and back up the database before running this script.
-- Run it against the Liens database, for example:
--   mysql --defaults-extra-file=/secure/liens.cnf liens < scripts/apply-contact-phone-extension-migration.sql
--
-- The script is restart-safe. It only adds the nullable column when the
-- preceding EF migration is recorded, and records this migration only after
-- verifying the resulting schema contract.

SET @contact_phone_extension_migration_id =
    '20260904010000_AddContactPhoneExtension';
SET @contact_phone_extension_predecessor_id =
    '20260903010000_AddCaseUpdateHistory';

SET @contact_phone_extension_predecessor_present = EXISTS (
    SELECT 1
    FROM `__EFMigrationsHistory`
    WHERE CAST(`MigrationId` AS BINARY) =
          CAST(@contact_phone_extension_predecessor_id AS BINARY)
);

SET @contacts_table_present = EXISTS (
    SELECT 1
    FROM information_schema.TABLES
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'liens_Contacts'
);

SET @contact_phone_extension_column_present = EXISTS (
    SELECT 1
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'liens_Contacts'
      AND COLUMN_NAME = 'PhoneExtension'
);

SET @add_contact_phone_extension_sql = IF(
    @contact_phone_extension_predecessor_present = 1
    AND @contacts_table_present = 1
    AND @contact_phone_extension_column_present = 0,
    'ALTER TABLE `liens_Contacts` ADD COLUMN `PhoneExtension` varchar(20) CHARACTER SET utf8mb4 NULL',
    'SELECT 1'
);

PREPARE add_contact_phone_extension_statement
    FROM @add_contact_phone_extension_sql;
EXECUTE add_contact_phone_extension_statement;
DEALLOCATE PREPARE add_contact_phone_extension_statement;

SET @contact_phone_extension_contract_valid = EXISTS (
    SELECT 1
    FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'liens_Contacts'
      AND COLUMN_NAME = 'PhoneExtension'
      AND COLUMN_TYPE = 'varchar(20)'
      AND IS_NULLABLE = 'YES'
      AND CHARACTER_SET_NAME = 'utf8mb4'
);

INSERT IGNORE INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
SELECT @contact_phone_extension_migration_id, '8.0.2'
WHERE @contact_phone_extension_predecessor_present = 1
  AND @contact_phone_extension_contract_valid = 1;

SELECT
    @contact_phone_extension_migration_id AS `MigrationId`,
    IF(@contact_phone_extension_predecessor_present = 1,
       'RECORDED', 'NOT_RECORDED') AS `PredecessorStatus`,
    IF(@contact_phone_extension_contract_valid = 1,
       'VALID', 'INVALID') AS `ContractStatus`,
    IF(EXISTS (
        SELECT 1
        FROM `__EFMigrationsHistory`
        WHERE CAST(`MigrationId` AS BINARY) =
              CAST(@contact_phone_extension_migration_id AS BINARY)
    ), 'RECORDED', 'NOT_RECORDED') AS `HistoryStatus`,
    IF(
        @contact_phone_extension_predecessor_present = 1
        AND @contact_phone_extension_contract_valid = 1
        AND EXISTS (
            SELECT 1
            FROM `__EFMigrationsHistory`
            WHERE CAST(`MigrationId` AS BINARY) =
                  CAST(@contact_phone_extension_migration_id AS BINARY)
        ),
        'READY', 'NOT_READY'
    ) AS `Status`;

SELECT
    COLUMN_NAME,
    COLUMN_TYPE,
    IS_NULLABLE,
    CHARACTER_SET_NAME
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'liens_Contacts'
  AND COLUMN_NAME = 'PhoneExtension';
