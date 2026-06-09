using Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations;

/// <summary>
/// Supports explicit global CareConnect organizations and stops relying on
/// inferred primary organization assignment for onboarding.
/// </summary>
[DbContext(typeof(IdentityDbContext))]
[Migration("20260606000001_CareConnectGlobalOrganizationOnboarding")]
public partial class CareConnectGlobalOrganizationOnboarding : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            SET @dbName = DATABASE();
            SET @dropFks = (
                SELECT GROUP_CONCAT(CONCAT('DROP FOREIGN KEY `', kcu.`CONSTRAINT_NAME`, '`') SEPARATOR ', ')
                FROM information_schema.key_column_usage kcu
                WHERE kcu.table_schema = @dbName
                  AND kcu.table_name   = 'idt_Organizations'
                  AND kcu.column_name  = 'TenantId'
                  AND kcu.referenced_table_name IS NOT NULL
            );

            SET @sql = IF(@dropFks IS NOT NULL,
                CONCAT('ALTER TABLE `idt_Organizations` ', @dropFks),
                'SELECT 1'
            );
            PREPARE stmt FROM @sql;
            EXECUTE stmt;
            DEALLOCATE PREPARE stmt;
            """);

        migrationBuilder.Sql("""
            SET @dbName = DATABASE();
            SET @tenantIdColumnType = (
                SELECT COLUMN_TYPE
                FROM information_schema.columns
                WHERE table_schema = @dbName
                  AND table_name   = 'idt_Tenants'
                  AND column_name  = 'Id'
                LIMIT 1
            );
            SET @tenantIdCharacterSet = (
                SELECT CHARACTER_SET_NAME
                FROM information_schema.columns
                WHERE table_schema = @dbName
                  AND table_name   = 'idt_Tenants'
                  AND column_name  = 'Id'
                LIMIT 1
            );
            SET @tenantIdCollation = (
                SELECT COLLATION_NAME
                FROM information_schema.columns
                WHERE table_schema = @dbName
                  AND table_name   = 'idt_Tenants'
                  AND column_name  = 'Id'
                LIMIT 1
            );
            SET @sql = CONCAT(
                'ALTER TABLE `idt_Organizations` MODIFY COLUMN `TenantId` ',
                @tenantIdColumnType,
                IF(@tenantIdCharacterSet IS NULL, '', CONCAT(' CHARACTER SET ', @tenantIdCharacterSet)),
                IF(@tenantIdCollation IS NULL, '', CONCAT(' COLLATE ', @tenantIdCollation)),
                ' NULL'
            );
            PREPARE stmt FROM @sql;
            EXECUTE stmt;
            DEALLOCATE PREPARE stmt;
            """);

        migrationBuilder.Sql("""
            SET @dbName = DATABASE();
            SET @hasFk = (
                SELECT COUNT(*)
                FROM information_schema.key_column_usage kcu
                WHERE kcu.table_schema          = @dbName
                  AND kcu.table_name            = 'idt_Organizations'
                  AND kcu.column_name           = 'TenantId'
                  AND kcu.referenced_table_name = 'idt_Tenants'
            );

            SET @sql = IF(@hasFk = 0,
                'ALTER TABLE `idt_Organizations`
                    ADD CONSTRAINT `FK_idt_Organizations_idt_Tenants_TenantId`
                    FOREIGN KEY (`TenantId`) REFERENCES `idt_Tenants` (`Id`)
                    ON DELETE RESTRICT',
                'SELECT 1'
            );
            PREPARE stmt FROM @sql;
            EXECUTE stmt;
            DEALLOCATE PREPARE stmt;
            """);

        migrationBuilder.Sql("""
            UPDATE `idt_UserOrganizationMemberships` uom
            INNER JOIN `idt_Organizations` o ON o.`Id` = uom.`OrganizationId`
            SET uom.`IsPrimary` = 0
            WHERE o.`OrgType` IN ('PROVIDER', 'LAW_FIRM')
              AND uom.`IsPrimary` = 1;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE uom
            FROM `idt_UserOrganizationMemberships` uom
            INNER JOIN `idt_Organizations` o ON o.`Id` = uom.`OrganizationId`
            WHERE o.`TenantId` IS NULL;
            """);

        migrationBuilder.Sql("""
            DELETE FROM `idt_Organizations`
            WHERE `TenantId` IS NULL;
            """);

        migrationBuilder.Sql("""
            SET @dbName = DATABASE();
            SET @dropFks = (
                SELECT GROUP_CONCAT(CONCAT('DROP FOREIGN KEY `', kcu.`CONSTRAINT_NAME`, '`') SEPARATOR ', ')
                FROM information_schema.key_column_usage kcu
                WHERE kcu.table_schema = @dbName
                  AND kcu.table_name   = 'idt_Organizations'
                  AND kcu.column_name  = 'TenantId'
                  AND kcu.referenced_table_name IS NOT NULL
            );

            SET @sql = IF(@dropFks IS NOT NULL,
                CONCAT('ALTER TABLE `idt_Organizations` ', @dropFks),
                'SELECT 1'
            );
            PREPARE stmt FROM @sql;
            EXECUTE stmt;
            DEALLOCATE PREPARE stmt;
            """);

        migrationBuilder.Sql("""
            SET @dbName = DATABASE();
            SET @tenantIdColumnType = (
                SELECT COLUMN_TYPE
                FROM information_schema.columns
                WHERE table_schema = @dbName
                  AND table_name   = 'idt_Tenants'
                  AND column_name  = 'Id'
                LIMIT 1
            );
            SET @tenantIdCharacterSet = (
                SELECT CHARACTER_SET_NAME
                FROM information_schema.columns
                WHERE table_schema = @dbName
                  AND table_name   = 'idt_Tenants'
                  AND column_name  = 'Id'
                LIMIT 1
            );
            SET @tenantIdCollation = (
                SELECT COLLATION_NAME
                FROM information_schema.columns
                WHERE table_schema = @dbName
                  AND table_name   = 'idt_Tenants'
                  AND column_name  = 'Id'
                LIMIT 1
            );
            SET @sql = CONCAT(
                'ALTER TABLE `idt_Organizations` MODIFY COLUMN `TenantId` ',
                @tenantIdColumnType,
                IF(@tenantIdCharacterSet IS NULL, '', CONCAT(' CHARACTER SET ', @tenantIdCharacterSet)),
                IF(@tenantIdCollation IS NULL, '', CONCAT(' COLLATE ', @tenantIdCollation)),
                ' NOT NULL'
            );
            PREPARE stmt FROM @sql;
            EXECUTE stmt;
            DEALLOCATE PREPARE stmt;
            """);

        migrationBuilder.Sql("""
            SET @dbName = DATABASE();
            SET @hasFk = (
                SELECT COUNT(*)
                FROM information_schema.key_column_usage kcu
                WHERE kcu.table_schema          = @dbName
                  AND kcu.table_name            = 'idt_Organizations'
                  AND kcu.column_name           = 'TenantId'
                  AND kcu.referenced_table_name = 'idt_Tenants'
            );

            SET @sql = IF(@hasFk = 0,
                'ALTER TABLE `idt_Organizations`
                    ADD CONSTRAINT `FK_idt_Organizations_idt_Tenants_TenantId`
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
