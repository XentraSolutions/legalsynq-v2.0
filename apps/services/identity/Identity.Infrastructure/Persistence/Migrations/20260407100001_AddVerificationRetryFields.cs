using Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(IdentityDbContext))]
    [Migration("20260407100001_AddVerificationRetryFields")]
    public partial class AddVerificationRetryFields : Migration
    {
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
SET @tenantDomainsTable = CASE
    WHEN EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.TABLES
        WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'TenantDomains'
    ) THEN 'TenantDomains'
    WHEN EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.TABLES
        WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'idt_TenantDomains'
    ) THEN 'idt_TenantDomains'
    ELSE NULL
END;

SET @sql = IF(@tenantsTable IS NOT NULL AND NOT EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = @db AND TABLE_NAME = @tenantsTable AND COLUMN_NAME = 'VerificationAttemptCount'
    ),
    CONCAT('ALTER TABLE `', @tenantsTable, '` ADD COLUMN `VerificationAttemptCount` int NOT NULL DEFAULT 0'),
    'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql = IF(@tenantsTable IS NOT NULL AND NOT EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = @db AND TABLE_NAME = @tenantsTable AND COLUMN_NAME = 'LastVerificationAttemptUtc'
    ),
    CONCAT('ALTER TABLE `', @tenantsTable, '` ADD COLUMN `LastVerificationAttemptUtc` datetime(6) NULL'),
    'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql = IF(@tenantsTable IS NOT NULL AND NOT EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = @db AND TABLE_NAME = @tenantsTable AND COLUMN_NAME = 'NextVerificationRetryAtUtc'
    ),
    CONCAT('ALTER TABLE `', @tenantsTable, '` ADD COLUMN `NextVerificationRetryAtUtc` datetime(6) NULL'),
    'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql = IF(@tenantsTable IS NOT NULL AND NOT EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = @db AND TABLE_NAME = @tenantsTable AND COLUMN_NAME = 'IsVerificationRetryExhausted'
    ),
    CONCAT('ALTER TABLE `', @tenantsTable, '` ADD COLUMN `IsVerificationRetryExhausted` tinyint(1) NOT NULL DEFAULT FALSE'),
    'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql = IF(@tenantsTable IS NOT NULL AND NOT EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = @db AND TABLE_NAME = @tenantsTable AND COLUMN_NAME = 'ProvisioningFailureStage'
    ),
    CONCAT('ALTER TABLE `', @tenantsTable, '` ADD COLUMN `ProvisioningFailureStage` varchar(30) CHARACTER SET utf8mb4 NOT NULL DEFAULT ''None'''),
    'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql = IF(@tenantDomainsTable IS NOT NULL AND NOT EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = @db AND TABLE_NAME = @tenantDomainsTable AND COLUMN_NAME = 'VerifiedAtUtc'
    ),
    CONCAT('ALTER TABLE `', @tenantDomainsTable, '` ADD COLUMN `VerifiedAtUtc` datetime(6) NULL'),
    'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "VerificationAttemptCount", table: "Tenants");
            migrationBuilder.DropColumn(name: "LastVerificationAttemptUtc", table: "Tenants");
            migrationBuilder.DropColumn(name: "NextVerificationRetryAtUtc", table: "Tenants");
            migrationBuilder.DropColumn(name: "IsVerificationRetryExhausted", table: "Tenants");
            migrationBuilder.DropColumn(name: "ProvisioningFailureStage", table: "Tenants");
            migrationBuilder.DropColumn(name: "VerifiedAtUtc", table: "TenantDomains");
        }
    }
}
