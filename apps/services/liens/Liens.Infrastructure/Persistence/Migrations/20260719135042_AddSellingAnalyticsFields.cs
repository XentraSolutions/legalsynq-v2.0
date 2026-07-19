using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Liens.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSellingAnalyticsFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAtUtc",
                table: "liens_Liens",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArchivedReason",
                table: "liens_Liens",
                type: "varchar(1000)",
                maxLength: 1000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<decimal>(
                name: "AskAmount",
                table: "liens_Liens",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FundingCompanyContactId",
                table: "liens_Liens",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "FundingCompanyId",
                table: "liens_Liens",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<decimal>(
                name: "HighestBidAmount",
                table: "liens_Liens",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ListingVisibility",
                table: "liens_Liens",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "SellerStatus",
                table: "liens_Liens",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "SoldAtUtc",
                table: "liens_Liens",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmittedForSaleAtUtc",
                table: "liens_Liens",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WithdrawnAtUtc",
                table: "liens_Liens",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE `liens_Liens`
                SET `SellerStatus` = CASE
                    WHEN `Status` = 'Sold' THEN 'Sold'
                    WHEN `Status` = 'Withdrawn' THEN 'Withdrawn'
                    WHEN `Status` IN ('Offered', 'UnderReview') THEN 'SubmittedForSale'
                    ELSE 'Draft'
                END
                WHERE `SellerStatus` IS NULL;
                """);

            migrationBuilder.Sql("""
                UPDATE `liens_Liens`
                SET `ListingVisibility` = 'Private'
                WHERE `ListingVisibility` IS NULL;
                """);

            migrationBuilder.Sql("""
                UPDATE `liens_Liens`
                SET `AskAmount` = `OfferPrice`
                WHERE `AskAmount` IS NULL
                  AND `OfferPrice` IS NOT NULL;
                """);

            migrationBuilder.Sql("""
                UPDATE `liens_Liens`
                SET `SubmittedForSaleAtUtc` = COALESCE(`UpdatedAtUtc`, `CreatedAtUtc`)
                WHERE `SubmittedForSaleAtUtc` IS NULL
                  AND `Status` IN ('Offered', 'UnderReview');
                """);

            migrationBuilder.Sql("""
                UPDATE `liens_Liens`
                SET `SoldAtUtc` = COALESCE(`ClosedAtUtc`, `UpdatedAtUtc`, `CreatedAtUtc`)
                WHERE `SoldAtUtc` IS NULL
                  AND `Status` = 'Sold';
                """);

            migrationBuilder.Sql("""
                UPDATE `liens_Liens`
                SET `WithdrawnAtUtc` = COALESCE(`ClosedAtUtc`, `UpdatedAtUtc`, `CreatedAtUtc`)
                WHERE `WithdrawnAtUtc` IS NULL
                  AND `Status` = 'Withdrawn';
                """);

            migrationBuilder.Sql("""
                UPDATE `liens_Liens` l
                JOIN (
                    SELECT `LienId`, MAX(`OfferAmount`) AS `HighestBid`
                    FROM `liens_LienOffers`
                    WHERE `Status` NOT IN ('Rejected', 'Withdrawn', 'Expired')
                      AND (`ExpiresAtUtc` IS NULL OR `ExpiresAtUtc` > UTC_TIMESTAMP(6))
                    GROUP BY `LienId`
                ) o ON o.`LienId` = l.`Id`
                SET l.`HighestBidAmount` = o.`HighestBid`
                WHERE l.`HighestBidAmount` IS NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Liens_Tenant_Seller_Funding_Status",
                table: "liens_Liens",
                columns: new[] { "TenantId", "SellingOrgId", "FundingCompanyId", "SellerStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_Liens_Tenant_Seller_InitialService",
                table: "liens_Liens",
                columns: new[] { "TenantId", "SellingOrgId", "InitialServiceDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Liens_Tenant_Seller_Status_Sold",
                table: "liens_Liens",
                columns: new[] { "TenantId", "SellingOrgId", "SellerStatus", "SoldAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Liens_Tenant_Seller_Status_Submitted",
                table: "liens_Liens",
                columns: new[] { "TenantId", "SellingOrgId", "SellerStatus", "SubmittedForSaleAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Liens_Tenant_SellerStatus_Visibility",
                table: "liens_Liens",
                columns: new[] { "TenantId", "SellerStatus", "ListingVisibility" });

            migrationBuilder.CreateIndex(
                name: "IX_Liens_TenantId_SellingOrgId_SellerStatus",
                table: "liens_Liens",
                columns: new[] { "TenantId", "SellingOrgId", "SellerStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_LienOffers_Tenant_Seller_OfferedAt",
                table: "liens_LienOffers",
                columns: new[] { "TenantId", "SellerOrgId", "OfferedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Liens_Tenant_Seller_Funding_Status",
                table: "liens_Liens");

            migrationBuilder.DropIndex(
                name: "IX_Liens_Tenant_Seller_InitialService",
                table: "liens_Liens");

            migrationBuilder.DropIndex(
                name: "IX_Liens_Tenant_Seller_Status_Sold",
                table: "liens_Liens");

            migrationBuilder.DropIndex(
                name: "IX_Liens_Tenant_Seller_Status_Submitted",
                table: "liens_Liens");

            migrationBuilder.DropIndex(
                name: "IX_Liens_Tenant_SellerStatus_Visibility",
                table: "liens_Liens");

            migrationBuilder.DropIndex(
                name: "IX_Liens_TenantId_SellingOrgId_SellerStatus",
                table: "liens_Liens");

            migrationBuilder.DropIndex(
                name: "IX_LienOffers_Tenant_Seller_OfferedAt",
                table: "liens_LienOffers");

            migrationBuilder.DropColumn(
                name: "ArchivedAtUtc",
                table: "liens_Liens");

            migrationBuilder.DropColumn(
                name: "ArchivedReason",
                table: "liens_Liens");

            migrationBuilder.DropColumn(
                name: "AskAmount",
                table: "liens_Liens");

            migrationBuilder.DropColumn(
                name: "FundingCompanyContactId",
                table: "liens_Liens");

            migrationBuilder.DropColumn(
                name: "FundingCompanyId",
                table: "liens_Liens");

            migrationBuilder.DropColumn(
                name: "HighestBidAmount",
                table: "liens_Liens");

            migrationBuilder.DropColumn(
                name: "ListingVisibility",
                table: "liens_Liens");

            migrationBuilder.DropColumn(
                name: "SellerStatus",
                table: "liens_Liens");

            migrationBuilder.DropColumn(
                name: "SoldAtUtc",
                table: "liens_Liens");

            migrationBuilder.DropColumn(
                name: "SubmittedForSaleAtUtc",
                table: "liens_Liens");

            migrationBuilder.DropColumn(
                name: "WithdrawnAtUtc",
                table: "liens_Liens");
        }
    }
}
