using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareConnect.Infrastructure.Data.Migrations;

public partial class AddNotificationDedupeKey : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            SET @dbname = DATABASE();
            SET @tbl = IF(
              (SELECT COUNT(*) FROM information_schema.tables
               WHERE TABLE_SCHEMA=@dbname AND TABLE_NAME='cc_CareConnectNotifications') > 0,
              'cc_CareConnectNotifications',
              'CareConnectNotifications');
            SET @s = IF(
              (SELECT COUNT(*) FROM information_schema.columns
               WHERE TABLE_SCHEMA=@dbname AND TABLE_NAME=@tbl
                 AND COLUMN_NAME='DedupeKey') = 0,
              CONCAT('ALTER TABLE `', @tbl, '` ADD COLUMN `DedupeKey` varchar(500) CHARACTER SET utf8mb4 NULL'),
              'SELECT 1');
            PREPARE stmt FROM @s; EXECUTE stmt; DEALLOCATE PREPARE stmt;");

        migrationBuilder.Sql(@"
            SET @dbname = DATABASE();
            SET @tbl = IF(
              (SELECT COUNT(*) FROM information_schema.tables
               WHERE TABLE_SCHEMA=@dbname AND TABLE_NAME='cc_CareConnectNotifications') > 0,
              'cc_CareConnectNotifications',
              'CareConnectNotifications');
            SET @s = IF(
              (SELECT COUNT(*) FROM information_schema.statistics
               WHERE TABLE_SCHEMA=@dbname AND TABLE_NAME=@tbl
                 AND INDEX_NAME='IX_CareConnectNotifications_DedupeKey') = 0,
              CONCAT('CREATE UNIQUE INDEX `IX_CareConnectNotifications_DedupeKey` ON `', @tbl, '` (`DedupeKey`)'),
              'SELECT 1');
            PREPARE stmt FROM @s; EXECUTE stmt; DEALLOCATE PREPARE stmt;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            SET @dbname = DATABASE();
            SET @tbl = IF(
              (SELECT COUNT(*) FROM information_schema.tables
               WHERE TABLE_SCHEMA=@dbname AND TABLE_NAME='cc_CareConnectNotifications') > 0,
              'cc_CareConnectNotifications',
              'CareConnectNotifications');
            SET @s = IF(
              (SELECT COUNT(*) FROM information_schema.statistics
               WHERE TABLE_SCHEMA=@dbname AND TABLE_NAME=@tbl
                 AND INDEX_NAME='IX_CareConnectNotifications_DedupeKey') > 0,
              CONCAT('DROP INDEX `IX_CareConnectNotifications_DedupeKey` ON `', @tbl, '`'),
              'SELECT 1');
            PREPARE stmt FROM @s; EXECUTE stmt; DEALLOCATE PREPARE stmt;");

        migrationBuilder.Sql(@"
            SET @dbname = DATABASE();
            SET @tbl = IF(
              (SELECT COUNT(*) FROM information_schema.tables
               WHERE TABLE_SCHEMA=@dbname AND TABLE_NAME='cc_CareConnectNotifications') > 0,
              'cc_CareConnectNotifications',
              'CareConnectNotifications');
            SET @s = IF(
              (SELECT COUNT(*) FROM information_schema.columns
               WHERE TABLE_SCHEMA=@dbname AND TABLE_NAME=@tbl
                 AND COLUMN_NAME='DedupeKey') > 0,
              CONCAT('ALTER TABLE `', @tbl, '` DROP COLUMN `DedupeKey`'),
              'SELECT 1');
            PREPARE stmt FROM @s; EXECUTE stmt; DEALLOCATE PREPARE stmt;");
    }
}
