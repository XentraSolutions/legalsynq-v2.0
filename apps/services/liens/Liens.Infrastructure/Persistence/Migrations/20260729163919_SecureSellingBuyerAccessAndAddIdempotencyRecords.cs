using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Liens.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SecureSellingBuyerAccessAndAddIdempotencyRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Phase 1 rollout compatibility: retain the legacy Token column so
            // old binaries can run while new binaries exclusively persist and
            // resolve TokenHash. A later post-drain migration may remove Token
            // only after the maximum issued-link lifetime has elapsed.

            // Selling V2 does not expose Draft as a seller workflow state. Core
            // Lien.Status remains unchanged; SellerStatus is classified from the
            // legacy lifecycle before ordinary intake rows fall back to Pending.
            migrationBuilder.Sql("""
                UPDATE `liens_Liens` AS lien
                LEFT JOIN (
                    SELECT `TenantId`, `LienId`, MIN(`CreatedAtUtc`) AS `FirstIssuedAtUtc`
                    FROM `liens_SellingBuyerAccessLinks`
                    GROUP BY `TenantId`, `LienId`
                ) AS access_link
                    ON access_link.`TenantId` = lien.`TenantId`
                   AND access_link.`LienId` = lien.`Id`
                SET lien.`SellerStatus` = CASE
                        WHEN lien.`Status` IN ('Sold', 'Active', 'Settled') OR lien.`SoldAtUtc` IS NOT NULL THEN 'Sold'
                        WHEN lien.`Status` = 'Withdrawn' OR lien.`WithdrawnAtUtc` IS NOT NULL THEN 'Withdrawn'
                        WHEN lien.`Status` IN ('Declined', 'Cancelled') THEN 'Declined'
                        WHEN lien.`Status` = 'Accepted' THEN 'Accepted'
                        WHEN lien.`Status` IN ('Offered', 'UnderReview') OR access_link.`LienId` IS NOT NULL THEN 'SubmittedForSale'
                        ELSE 'Pending'
                    END,
                    lien.`SubmittedForSaleAtUtc` = CASE
                        WHEN lien.`Status` IN ('Offered', 'UnderReview') OR access_link.`LienId` IS NOT NULL
                            THEN COALESCE(lien.`SubmittedForSaleAtUtc`, access_link.`FirstIssuedAtUtc`, lien.`UpdatedAtUtc`, lien.`CreatedAtUtc`)
                        ELSE lien.`SubmittedForSaleAtUtc`
                    END,
                    lien.`SoldAtUtc` = CASE
                        WHEN lien.`Status` IN ('Sold', 'Active', 'Settled')
                            THEN COALESCE(lien.`SoldAtUtc`, lien.`ClosedAtUtc`, lien.`UpdatedAtUtc`, lien.`CreatedAtUtc`)
                        ELSE lien.`SoldAtUtc`
                    END,
                    lien.`WithdrawnAtUtc` = CASE
                        WHEN lien.`Status` = 'Withdrawn'
                            THEN COALESCE(lien.`WithdrawnAtUtc`, lien.`ClosedAtUtc`, lien.`UpdatedAtUtc`, lien.`CreatedAtUtc`)
                        ELSE lien.`WithdrawnAtUtc`
                    END
                WHERE lien.`SellerStatus` IS NULL
                   OR lien.`SellerStatus` = 'Draft';
                """);

            AddColumnIfMissing(
                migrationBuilder,
                "liens_Liens",
                "BuyerMessage",
                "varchar(4000) CHARACTER SET utf8mb4 NULL");

            AddColumnIfMissing(
                migrationBuilder,
                "liens_SellingBuyerAccessLinks",
                "TokenHash",
                "varchar(64) CHARACTER SET utf8mb4 NULL");

            AddColumnIfMissing(
                migrationBuilder,
                "liens_SellingBuyerAccessLinks",
                "Route",
                "varchar(180) CHARACTER SET utf8mb4 NULL");

            // Preserve existing issued links by backfilling their digest. The
            // plaintext column remains during this compatibility deployment.
            migrationBuilder.Sql("""
                UPDATE `liens_SellingBuyerAccessLinks`
                SET `TokenHash` = LOWER(SHA2(`Token`, 256)),
                    `Route` = '/api/liens/selling/liens/{lienId}/confirm-sale'
                WHERE `TokenHash` IS NULL
                   OR `Route` IS NULL;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Token",
                table: "liens_SellingBuyerAccessLinks",
                type: "varchar(128)",
                maxLength: 128,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(128)",
                oldMaxLength: 128)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS `liens_SellingIdempotencyRecords` (
                    `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
                    `TenantId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
                    `SubjectType` varchar(32) CHARACTER SET utf8mb4 NOT NULL,
                    `SubjectId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
                    `Route` varchar(180) CHARACTER SET utf8mb4 NOT NULL,
                    `ResourceType` varchar(80) CHARACTER SET utf8mb4 NOT NULL,
                    `ResourceKey` varchar(180) CHARACTER SET utf8mb4 NOT NULL,
                    `IdempotencyKey` varchar(280) CHARACTER SET utf8mb4 NOT NULL,
                    `IdempotencyKeyHash` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
                    `RequestHash` varchar(64) CHARACTER SET utf8mb4 NOT NULL,
                    `ProcessingState` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
                    `ResponseStatusCode` int NULL,
                    `ResponseContentType` varchar(100) CHARACTER SET utf8mb4 NULL,
                    `ResponseBody` longtext CHARACTER SET utf8mb4 NULL,
                    `CompletedAtUtc` datetime(6) NULL,
                    `CreatedAtUtc` datetime(6) NOT NULL,
                    `UpdatedAtUtc` datetime(6) NOT NULL,
                    `CreatedByUserId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
                    `UpdatedByUserId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NULL,
                    CONSTRAINT `PK_liens_SellingIdempotencyRecords` PRIMARY KEY (`Id`)
                ) CHARACTER SET=utf8mb4;
                """);

            CreateIndexIfMissing(
                migrationBuilder,
                "liens_SellingBuyerAccessLinks",
                "UX_SellingBuyerAccessLinks_Tenant_Scope_IdempotencyKey",
                "CREATE UNIQUE INDEX `UX_SellingBuyerAccessLinks_Tenant_Scope_IdempotencyKey` ON `liens_SellingBuyerAccessLinks` (`TenantId`, `SellerOrgId`, `LienId`, `BuyerOrgId`, `BuyerContactId`, `CreatedByUserId`, `Route`, `IdempotencyKey`)");

            CreateIndexIfMissing(
                migrationBuilder,
                "liens_SellingBuyerAccessLinks",
                "UX_SellingBuyerAccessLinks_TokenHash",
                "CREATE UNIQUE INDEX `UX_SellingBuyerAccessLinks_TokenHash` ON `liens_SellingBuyerAccessLinks` (`TokenHash`)");

            // Old binaries still write Token only. These triggers keep their
            // inserts discoverable by the new hash-only lookup without making
            // new application writes persist plaintext tokens.
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS `TRG_SellingBuyerAccessLink_HashToken_BI`;");

            migrationBuilder.Sql("""
                CREATE TRIGGER `TRG_SellingBuyerAccessLink_HashToken_BI`
                BEFORE INSERT ON `liens_SellingBuyerAccessLinks`
                FOR EACH ROW
                SET NEW.`TokenHash` = CASE
                    WHEN NEW.`TokenHash` IS NULL AND NEW.`Token` IS NOT NULL THEN LOWER(SHA2(NEW.`Token`, 256))
                    ELSE NEW.`TokenHash`
                END;
                """);

            migrationBuilder.Sql("DROP TRIGGER IF EXISTS `TRG_SellingBuyerAccessLink_HashToken_BU`;");

            migrationBuilder.Sql("""
                CREATE TRIGGER `TRG_SellingBuyerAccessLink_HashToken_BU`
                BEFORE UPDATE ON `liens_SellingBuyerAccessLinks`
                FOR EACH ROW
                SET NEW.`TokenHash` = CASE
                    WHEN (NEW.`TokenHash` IS NULL OR NEW.`TokenHash` = '') AND NEW.`Token` IS NOT NULL THEN LOWER(SHA2(NEW.`Token`, 256))
                    ELSE NEW.`TokenHash`
                END;
                """);

            CreateIndexIfMissing(
                migrationBuilder,
                "liens_SellingIdempotencyRecords",
                "IX_SellingIdem_Tenant_CreatedAtUtc",
                "CREATE INDEX `IX_SellingIdem_Tenant_CreatedAtUtc` ON `liens_SellingIdempotencyRecords` (`TenantId`, `CreatedAtUtc`)");

            CreateIndexIfMissing(
                migrationBuilder,
                "liens_SellingIdempotencyRecords",
                "UX_SellingIdem_Tenant_Subject_Route_Resource_Key",
                "CREATE UNIQUE INDEX `UX_SellingIdem_Tenant_Subject_Route_Resource_Key` ON `liens_SellingIdempotencyRecords` (`TenantId`, `SubjectType`, `SubjectId`, `Route`, `ResourceType`, `ResourceKey`, `IdempotencyKeyHash`)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new InvalidOperationException(
                "This security migration is forward-only. New buyer links retain only TokenHash, so plaintext Token values cannot be restored safely. " +
                "Use an audited database restore taken before this migration together with the application rollback, or deploy a forward corrective migration; do not mark this migration reverted in place.");
        }

        private static void AddColumnIfMissing(
            MigrationBuilder migrationBuilder,
            string tableName,
            string columnName,
            string columnDefinition)
        {
            migrationBuilder.Sql($"""
                SET @db = DATABASE();
                SET @ddl = IF((SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=@db AND TABLE_NAME='{tableName}' AND COLUMN_NAME='{columnName}')=0,
                    'ALTER TABLE `{tableName}` ADD COLUMN `{columnName}` {columnDefinition}',
                    'SELECT 1');
                PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;
                """);
        }

        private static void CreateIndexIfMissing(
            MigrationBuilder migrationBuilder,
            string tableName,
            string indexName,
            string createIndexSql)
        {
            migrationBuilder.Sql($"""
                SET @db = DATABASE();
                SET @ddl = IF((SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS WHERE TABLE_SCHEMA=@db AND TABLE_NAME='{tableName}' AND INDEX_NAME='{indexName}')=0,
                    '{createIndexSql}',
                    'SELECT 1');
                PREPARE stmt FROM @ddl; EXECUTE stmt; DEALLOCATE PREPARE stmt;
                """);
        }
    }
}
