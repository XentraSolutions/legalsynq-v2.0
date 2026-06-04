using Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(IdentityDbContext))]
    [Migration("20260414100001_AddOrganizationProviderMode")]
    public partial class AddOrganizationProviderMode : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
SET @db = DATABASE();
SET @organizationsTable = CASE
    WHEN EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.TABLES
        WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'Organizations'
    ) THEN 'Organizations'
    WHEN EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.TABLES
        WHERE TABLE_SCHEMA = @db AND TABLE_NAME = 'idt_Organizations'
    ) THEN 'idt_Organizations'
    ELSE NULL
END;

SET @sql = IF(@organizationsTable IS NOT NULL AND NOT EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = @db AND TABLE_NAME = @organizationsTable AND COLUMN_NAME = 'ProviderMode'
    ),
    CONCAT('ALTER TABLE `', @organizationsTable, '` ADD COLUMN `ProviderMode` varchar(20) CHARACTER SET utf8mb4 NOT NULL DEFAULT ''sell'''),
    'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;

SET @sql = IF(@organizationsTable IS NOT NULL,
    CONCAT('UPDATE `', @organizationsTable, '` SET `ProviderMode` = ''sell'' WHERE `ProviderMode` IS NULL OR `ProviderMode` = '''''),
    'SELECT 1');
PREPARE stmt FROM @sql; EXECUTE stmt; DEALLOCATE PREPARE stmt;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProviderMode",
                table: "idt_Organizations");
        }
    }
}
