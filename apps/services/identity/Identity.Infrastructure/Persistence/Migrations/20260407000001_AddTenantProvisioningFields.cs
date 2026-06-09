using Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(IdentityDbContext))]
    [Migration("20260407000001_AddTenantProvisioningFields")]
    public partial class AddTenantProvisioningFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
SET @db = DATABASE();
SET @tenantsTable = CASE
    WHEN EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.TABLES
        WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'Tenants'
    ) THEN 'Tenants'
    WHEN EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.TABLES
        WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'idt_Tenants'
    ) THEN 'idt_Tenants'
    ELSE NULL
END;

SET @sql = IF(@tenantsTable IS NOT NULL AND NOT EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = @db AND TABLE_NAME = @tenantsTable AND COLUMN_NAME = 'Subdomain'
    ),
    CONCAT('ALTER TABLE `', @tenantsTable, '` ADD COLUMN `Subdomain` varchar(63) CHARACTER SET utf8mb4 NULL'),
    'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql = IF(@tenantsTable IS NOT NULL AND NOT EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = @db AND TABLE_NAME = @tenantsTable AND COLUMN_NAME = 'ProvisioningStatus'
    ),
    CONCAT('ALTER TABLE `', @tenantsTable, '` ADD COLUMN `ProvisioningStatus` varchar(20) CHARACTER SET utf8mb4 NOT NULL DEFAULT ''Pending'''),
    'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql = IF(@tenantsTable IS NOT NULL AND NOT EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = @db AND TABLE_NAME = @tenantsTable AND COLUMN_NAME = 'LastProvisioningAttemptUtc'
    ),
    CONCAT('ALTER TABLE `', @tenantsTable, '` ADD COLUMN `LastProvisioningAttemptUtc` datetime(6) NULL'),
    'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql = IF(@tenantsTable IS NOT NULL AND NOT EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = @db AND TABLE_NAME = @tenantsTable AND COLUMN_NAME = 'ProvisioningFailureReason'
    ),
    CONCAT('ALTER TABLE `', @tenantsTable, '` ADD COLUMN `ProvisioningFailureReason` varchar(500) CHARACTER SET utf8mb4 NULL'),
    'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @subdomainIndex = IF(@tenantsTable = 'idt_Tenants', 'IX_idt_Tenants_Subdomain', 'IX_Tenants_Subdomain');
SET @sql = IF(@tenantsTable IS NOT NULL AND NOT EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.STATISTICS
        WHERE TABLE_SCHEMA = @db AND TABLE_NAME = @tenantsTable AND INDEX_NAME = @subdomainIndex
    ),
    CONCAT('CREATE UNIQUE INDEX `', @subdomainIndex, '` ON `', @tenantsTable, '` (`Subdomain`)'),
    'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql = IF(@tenantsTable IS NOT NULL,
    CONCAT('UPDATE `', @tenantsTable, '` SET `ProvisioningStatus` = ''Active'' WHERE `Code` = ''LEGALSYNQ'''),
    'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tenants_Subdomain",
                table: "Tenants");

            migrationBuilder.DropColumn(name: "Subdomain", table: "Tenants");
            migrationBuilder.DropColumn(name: "ProvisioningStatus", table: "Tenants");
            migrationBuilder.DropColumn(name: "LastProvisioningAttemptUtc", table: "Tenants");
            migrationBuilder.DropColumn(name: "ProvisioningFailureReason", table: "Tenants");
        }
    }
}
