using Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations;

/// <summary>
/// Removes the legacy idt_Users.TenantId ownership column.
///
/// idt_UserTenants is now the only source of user-to-tenant membership.
/// </summary>
[DbContext(typeof(IdentityDbContext))]
[Migration("20260602000001_DropLegacyUserTenantId")]
public partial class DropLegacyUserTenantId : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            SET @dbName = DATABASE();
            SET @hasTenantId = (
                SELECT COUNT(*)
                FROM information_schema.columns
                WHERE table_schema = @dbName
                  AND table_name   = 'idt_Users'
                  AND column_name  = 'TenantId'
            );

            SET @sql = IF(@hasTenantId > 0,
                'INSERT INTO `idt_UserTenants` (`Id`, `UserId`, `TenantId`, `IsActive`, `JoinedAtUtc`)
                 SELECT UUID(), u.`Id`, u.`TenantId`, 1, u.`CreatedAtUtc`
                 FROM `idt_Users` u
                 WHERE u.`TenantId` IS NOT NULL
                   AND NOT EXISTS (
                       SELECT 1
                       FROM `idt_UserTenants` ut
                       WHERE ut.`UserId` = u.`Id`
                         AND ut.`TenantId` = u.`TenantId`
                   )',
                'SELECT 1'
            );
            PREPARE stmt FROM @sql;
            EXECUTE stmt;
            DEALLOCATE PREPARE stmt;
            """);

        migrationBuilder.Sql("""
            SET @dbName = DATABASE();
            SET @dropFks = (
                SELECT GROUP_CONCAT(CONCAT('DROP FOREIGN KEY `', kcu.`CONSTRAINT_NAME`, '`') SEPARATOR ', ')
                FROM information_schema.key_column_usage kcu
                WHERE kcu.table_schema = @dbName
                  AND kcu.table_name = 'idt_Users'
                  AND kcu.column_name = 'TenantId'
                  AND kcu.referenced_table_name IS NOT NULL
            );

            SET @sql = IF(@dropFks IS NOT NULL,
                CONCAT('ALTER TABLE `idt_Users` ', @dropFks),
                'SELECT 1'
            );
            PREPARE stmt FROM @sql;
            EXECUTE stmt;
            DEALLOCATE PREPARE stmt;
            """);

        migrationBuilder.Sql("""
            SET @dbName = DATABASE();
            SET @dropIndexes = (
                SELECT GROUP_CONCAT(CONCAT('DROP INDEX `', idx.`INDEX_NAME`, '`') SEPARATOR ', ')
                FROM (
                    SELECT DISTINCT s.`INDEX_NAME`
                    FROM information_schema.statistics s
                    WHERE s.table_schema = @dbName
                      AND s.table_name = 'idt_Users'
                      AND s.column_name = 'TenantId'
                      AND s.index_name <> 'PRIMARY'
                ) idx
            );

            SET @sql = IF(@dropIndexes IS NOT NULL,
                CONCAT('ALTER TABLE `idt_Users` ', @dropIndexes),
                'SELECT 1'
            );
            PREPARE stmt FROM @sql;
            EXECUTE stmt;
            DEALLOCATE PREPARE stmt;
            """);

        migrationBuilder.Sql("""
            SET @dbName = DATABASE();
            SET @hasTenantId = (
                SELECT COUNT(*)
                FROM information_schema.columns
                WHERE table_schema = @dbName
                  AND table_name   = 'idt_Users'
                  AND column_name  = 'TenantId'
            );

            SET @sql = IF(@hasTenantId > 0,
                'ALTER TABLE `idt_Users` DROP COLUMN `TenantId`',
                'SELECT 1'
            );
            PREPARE stmt FROM @sql;
            EXECUTE stmt;
            DEALLOCATE PREPARE stmt;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            SET @dbName = DATABASE();
            SET @hasTenantId = (
                SELECT COUNT(*)
                FROM information_schema.columns
                WHERE table_schema = @dbName
                  AND table_name   = 'idt_Users'
                  AND column_name  = 'TenantId'
            );

            SET @sql = IF(@hasTenantId = 0,
                'ALTER TABLE `idt_Users` ADD COLUMN `TenantId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NULL',
                'SELECT 1'
            );
            PREPARE stmt FROM @sql;
            EXECUTE stmt;
            DEALLOCATE PREPARE stmt;
            """);

        migrationBuilder.Sql("""
            UPDATE `idt_Users` u
            INNER JOIN (
                SELECT picked.`UserId`, picked.`TenantId`
                FROM `idt_UserTenants` picked
                INNER JOIN (
                    SELECT `UserId`, MIN(`JoinedAtUtc`) AS `EarliestJoinedAtUtc`
                    FROM `idt_UserTenants`
                    WHERE `IsActive` = 1
                    GROUP BY `UserId`
                ) earliest
                    ON earliest.`UserId` = picked.`UserId`
                   AND earliest.`EarliestJoinedAtUtc` = picked.`JoinedAtUtc`
                WHERE picked.`IsActive` = 1
            ) firstTenant ON firstTenant.`UserId` = u.`Id`
            SET u.`TenantId` = firstTenant.`TenantId`
            WHERE u.`TenantId` IS NULL;
            """);

        migrationBuilder.Sql("""
            SET @dbName = DATABASE();
            SET @hasIndex = (
                SELECT COUNT(*)
                FROM information_schema.statistics
                WHERE table_schema = @dbName
                  AND table_name   = 'idt_Users'
                  AND index_name   = 'IX_idt_Users_TenantId'
            );

            SET @sql = IF(@hasIndex = 0,
                'CREATE INDEX `IX_idt_Users_TenantId` ON `idt_Users` (`TenantId`)',
                'SELECT 1'
            );
            PREPARE stmt FROM @sql;
            EXECUTE stmt;
            DEALLOCATE PREPARE stmt;
            """);

        migrationBuilder.Sql("""
            SET @dbName = DATABASE();
            SET @hasFk = (
                SELECT COUNT(*)
                FROM information_schema.table_constraints
                WHERE table_schema = @dbName
                  AND table_name = 'idt_Users'
                  AND constraint_name = 'FK_idt_Users_idt_Tenants_TenantId'
                  AND constraint_type = 'FOREIGN KEY'
            );

            SET @sql = IF(@hasFk = 0,
                'ALTER TABLE `idt_Users`
                 ADD CONSTRAINT `FK_idt_Users_idt_Tenants_TenantId`
                 FOREIGN KEY (`TenantId`) REFERENCES `idt_Tenants` (`Id`)
                 ON DELETE RESTRICT',
                'SELECT 1'
            );
            PREPARE stmt FROM @sql;
            EXECUTE stmt;
            DEALLOCATE PREPARE stmt;
            """);
    }
}
