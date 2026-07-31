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

            migrationBuilder.AddColumn<string>(
                name: "BuyerMessage",
                table: "liens_Liens",
                type: "varchar(4000)",
                maxLength: 4000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "TokenHash",
                table: "liens_SellingBuyerAccessLinks",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Route",
                table: "liens_SellingBuyerAccessLinks",
                type: "varchar(180)",
                maxLength: 180,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

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

            migrationBuilder.CreateTable(
                name: "liens_SellingIdempotencyRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    SubjectType = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SubjectId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Route = table.Column<string>(type: "varchar(180)", maxLength: 180, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ResourceType = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                ResourceKey = table.Column<string>(type: "varchar(180)", maxLength: 180, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                IdempotencyKey = table.Column<string>(type: "varchar(280)", maxLength: 280, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                IdempotencyKeyHash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                RequestHash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ProcessingState = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ResponseStatusCode = table.Column<int>(type: "int", nullable: true),
                    ResponseContentType = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ResponseBody = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_liens_SellingIdempotencyRecords", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "UX_SellingBuyerAccessLinks_Tenant_Scope_IdempotencyKey",
                table: "liens_SellingBuyerAccessLinks",
                columns: new[] { "TenantId", "SellerOrgId", "LienId", "BuyerOrgId", "BuyerContactId", "CreatedByUserId", "Route", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_SellingBuyerAccessLinks_TokenHash",
                table: "liens_SellingBuyerAccessLinks",
                column: "TokenHash",
                unique: true);

            // Old binaries still write Token only. These triggers keep their
            // inserts discoverable by the new hash-only lookup without making
            // new application writes persist plaintext tokens.
            migrationBuilder.Sql("""
                CREATE TRIGGER `TRG_SellingBuyerAccessLink_HashToken_BI`
                BEFORE INSERT ON `liens_SellingBuyerAccessLinks`
                FOR EACH ROW
                SET NEW.`TokenHash` = CASE
                    WHEN NEW.`TokenHash` IS NULL AND NEW.`Token` IS NOT NULL THEN LOWER(SHA2(NEW.`Token`, 256))
                    ELSE NEW.`TokenHash`
                END;
                """);

            migrationBuilder.Sql("""
                CREATE TRIGGER `TRG_SellingBuyerAccessLink_HashToken_BU`
                BEFORE UPDATE ON `liens_SellingBuyerAccessLinks`
                FOR EACH ROW
                SET NEW.`TokenHash` = CASE
                    WHEN (NEW.`TokenHash` IS NULL OR NEW.`TokenHash` = '') AND NEW.`Token` IS NOT NULL THEN LOWER(SHA2(NEW.`Token`, 256))
                    ELSE NEW.`TokenHash`
                END;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_SellingIdem_Tenant_CreatedAtUtc",
                table: "liens_SellingIdempotencyRecords",
                columns: new[] { "TenantId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_SellingIdem_Tenant_Subject_Route_Resource_Key",
                table: "liens_SellingIdempotencyRecords",
                columns: new[] { "TenantId", "SubjectType", "SubjectId", "Route", "ResourceType", "ResourceKey", "IdempotencyKeyHash" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new InvalidOperationException(
                "This security migration is forward-only. New buyer links retain only TokenHash, so plaintext Token values cannot be restored safely. " +
                "Use an audited database restore taken before this migration together with the application rollback, or deploy a forward corrective migration; do not mark this migration reverted in place.");
        }
    }
}
