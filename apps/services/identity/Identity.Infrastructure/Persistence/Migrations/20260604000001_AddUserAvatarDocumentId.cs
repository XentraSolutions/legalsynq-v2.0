using Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations;

[DbContext(typeof(IdentityDbContext))]
[Migration("20260604000001_AddUserAvatarDocumentId")]
public partial class AddUserAvatarDocumentId : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
SET @db = DATABASE();
SET @col = IF((SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
               WHERE TABLE_SCHEMA = @db
               AND   TABLE_NAME  = 'idt_Users'
               AND   COLUMN_NAME = 'AvatarDocumentId') = 0,
    'ALTER TABLE `idt_Users` ADD COLUMN `AvatarDocumentId` varchar(36) NULL',
    'SELECT 1');
PREPARE stmt FROM @col; EXECUTE stmt; DEALLOCATE PREPARE stmt;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
SET @db = DATABASE();
SET @col = IF((SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
               WHERE TABLE_SCHEMA = @db
               AND   TABLE_NAME  = 'idt_Users'
               AND   COLUMN_NAME = 'AvatarDocumentId') > 0,
    'ALTER TABLE `idt_Users` DROP COLUMN `AvatarDocumentId`',
    'SELECT 1');
PREPARE stmt FROM @col; EXECUTE stmt; DEALLOCATE PREPARE stmt;");
    }
}
