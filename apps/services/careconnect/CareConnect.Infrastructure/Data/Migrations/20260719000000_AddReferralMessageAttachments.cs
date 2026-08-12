using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareConnect.Infrastructure.Data.Migrations
{
    [DbContext(typeof(CareConnectDbContext))]
    [Migration("20260719000000_AddReferralMessageAttachments")]
    public partial class AddReferralMessageAttachments : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
SET @s = IF(
  (SELECT COUNT(*) FROM information_schema.columns
   WHERE table_schema = DATABASE()
     AND table_name = 'cc_ReferralAttachments'
     AND column_name = 'ReferralCommentId') = 0,
  'ALTER TABLE `cc_ReferralAttachments` ADD `ReferralCommentId` char(36) COLLATE ascii_general_ci NULL',
  'SELECT 1');
PREPARE stmt FROM @s; EXECUTE stmt; DEALLOCATE PREPARE stmt;");

            migrationBuilder.Sql(@"
SET @comment_id_collation = (
  SELECT COLLATION_NAME FROM information_schema.columns
  WHERE table_schema = DATABASE()
    AND table_name = 'cc_ReferralComments'
    AND column_name = 'Id'
);
SET @existing_fk_count = (
  SELECT COUNT(*) FROM information_schema.table_constraints
  WHERE constraint_schema = DATABASE()
    AND table_name = 'cc_ReferralAttachments'
    AND constraint_name = 'FK_cc_ReferralAttachments_cc_ReferralComments_ReferralCommentId'
    AND constraint_type = 'FOREIGN KEY'
);
SET @s = IF(
  @comment_id_collation IS NOT NULL
  AND COALESCE(@comment_id_collation, '') <> 'ascii_general_ci'
  AND @existing_fk_count = 0,
  'ALTER TABLE `cc_ReferralComments` MODIFY `Id` char(36) COLLATE ascii_general_ci NOT NULL',
  'SELECT 1');
PREPARE stmt FROM @s; EXECUTE stmt; DEALLOCATE PREPARE stmt;");

            migrationBuilder.Sql(@"
SET @attachment_comment_collation = (
  SELECT COLLATION_NAME FROM information_schema.columns
  WHERE table_schema = DATABASE()
    AND table_name = 'cc_ReferralAttachments'
    AND column_name = 'ReferralCommentId'
);
SET @existing_fk_count = (
  SELECT COUNT(*) FROM information_schema.table_constraints
  WHERE constraint_schema = DATABASE()
    AND table_name = 'cc_ReferralAttachments'
    AND constraint_name = 'FK_cc_ReferralAttachments_cc_ReferralComments_ReferralCommentId'
    AND constraint_type = 'FOREIGN KEY'
);
SET @s = IF(
  @attachment_comment_collation IS NOT NULL
  AND COALESCE(@attachment_comment_collation, '') <> 'ascii_general_ci'
  AND @existing_fk_count = 0,
  'ALTER TABLE `cc_ReferralAttachments` MODIFY `ReferralCommentId` char(36) COLLATE ascii_general_ci NULL',
  'SELECT 1');
PREPARE stmt FROM @s; EXECUTE stmt; DEALLOCATE PREPARE stmt;");

            migrationBuilder.Sql(@"
SET @s = IF(
  (SELECT COUNT(*) FROM information_schema.statistics
   WHERE table_schema = DATABASE()
     AND table_name = 'cc_ReferralAttachments'
     AND index_name = 'IX_cc_ReferralAttachments_ReferralCommentId') = 0,
  'CREATE INDEX `IX_cc_ReferralAttachments_ReferralCommentId` ON `cc_ReferralAttachments` (`ReferralCommentId`)',
  'SELECT 1');
PREPARE stmt FROM @s; EXECUTE stmt; DEALLOCATE PREPARE stmt;");

            migrationBuilder.Sql(@"
SET @s = IF(
  (SELECT COUNT(*) FROM information_schema.statistics
   WHERE table_schema = DATABASE()
     AND table_name = 'cc_ReferralAttachments'
     AND index_name = 'IX_cc_ReferralAttachments_ReferralComment') = 0,
  'CREATE INDEX `IX_cc_ReferralAttachments_ReferralComment` ON `cc_ReferralAttachments` (`TenantId`, `ReferralId`, `ReferralCommentId`, `CreatedAtUtc`)',
  'SELECT 1');
PREPARE stmt FROM @s; EXECUTE stmt; DEALLOCATE PREPARE stmt;");

            migrationBuilder.Sql(@"
SET @attachment_comment_collation = (
  SELECT COLLATION_NAME FROM information_schema.columns
  WHERE table_schema = DATABASE()
    AND table_name = 'cc_ReferralAttachments'
    AND column_name = 'ReferralCommentId'
);
SET @comment_id_collation = (
  SELECT COLLATION_NAME FROM information_schema.columns
  WHERE table_schema = DATABASE()
    AND table_name = 'cc_ReferralComments'
    AND column_name = 'Id'
);
SET @message_attachment_count = (
  SELECT COUNT(*) FROM `cc_ReferralAttachments`
  WHERE `ReferralCommentId` IS NOT NULL
);
SET @s = IF(
  (SELECT COUNT(*) FROM information_schema.table_constraints
   WHERE constraint_schema = DATABASE()
     AND table_name = 'cc_ReferralAttachments'
     AND constraint_name = 'FK_cc_ReferralAttachments_cc_ReferralComments_ReferralCommentId'
     AND constraint_type = 'FOREIGN KEY') = 0
  AND @message_attachment_count = 0
  AND COALESCE(@attachment_comment_collation, '') = COALESCE(@comment_id_collation, ''),
  'ALTER TABLE `cc_ReferralAttachments` ADD CONSTRAINT `FK_cc_ReferralAttachments_cc_ReferralComments_ReferralCommentId` FOREIGN KEY (`ReferralCommentId`) REFERENCES `cc_ReferralComments` (`Id`) ON DELETE CASCADE',
  'SELECT 1');
PREPARE stmt FROM @s; EXECUTE stmt; DEALLOCATE PREPARE stmt;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
SET @s = IF(
  (SELECT COUNT(*) FROM information_schema.table_constraints
   WHERE constraint_schema = DATABASE()
     AND table_name = 'cc_ReferralAttachments'
     AND constraint_name = 'FK_cc_ReferralAttachments_cc_ReferralComments_ReferralCommentId'
     AND constraint_type = 'FOREIGN KEY') > 0,
  'ALTER TABLE `cc_ReferralAttachments` DROP FOREIGN KEY `FK_cc_ReferralAttachments_cc_ReferralComments_ReferralCommentId`',
  'SELECT 1');
PREPARE stmt FROM @s; EXECUTE stmt; DEALLOCATE PREPARE stmt;");

            migrationBuilder.Sql(@"
SET @s = IF(
  (SELECT COUNT(*) FROM information_schema.statistics
   WHERE table_schema = DATABASE()
     AND table_name = 'cc_ReferralAttachments'
     AND index_name = 'IX_cc_ReferralAttachments_ReferralComment') > 0,
  'ALTER TABLE `cc_ReferralAttachments` DROP INDEX `IX_cc_ReferralAttachments_ReferralComment`',
  'SELECT 1');
PREPARE stmt FROM @s; EXECUTE stmt; DEALLOCATE PREPARE stmt;");

            migrationBuilder.Sql(@"
SET @s = IF(
  (SELECT COUNT(*) FROM information_schema.statistics
   WHERE table_schema = DATABASE()
     AND table_name = 'cc_ReferralAttachments'
     AND index_name = 'IX_cc_ReferralAttachments_ReferralCommentId') > 0,
  'ALTER TABLE `cc_ReferralAttachments` DROP INDEX `IX_cc_ReferralAttachments_ReferralCommentId`',
  'SELECT 1');
PREPARE stmt FROM @s; EXECUTE stmt; DEALLOCATE PREPARE stmt;");

            migrationBuilder.Sql(@"
SET @s = IF(
  (SELECT COUNT(*) FROM information_schema.columns
   WHERE table_schema = DATABASE()
     AND table_name = 'cc_ReferralAttachments'
     AND column_name = 'ReferralCommentId') > 0,
  'ALTER TABLE `cc_ReferralAttachments` DROP COLUMN `ReferralCommentId`',
  'SELECT 1');
PREPARE stmt FROM @s; EXECUTE stmt; DEALLOCATE PREPARE stmt;");
        }
    }
}
