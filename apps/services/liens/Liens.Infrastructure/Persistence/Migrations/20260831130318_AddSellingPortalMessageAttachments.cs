using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Liens.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSellingPortalMessageAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            SellingSchemaMigrationGuards.CreateTableIfMissing(
                migrationBuilder,
                """
                CREATE TABLE IF NOT EXISTS `liens_SellingPortalMessageAttachments` (
                    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
                    `TenantId` char(36) COLLATE ascii_general_ci NOT NULL,
                    `LienId` char(36) COLLATE ascii_general_ci NOT NULL,
                    `SellerOrgId` char(36) COLLATE ascii_general_ci NOT NULL,
                    `BuyerOrgId` char(36) COLLATE ascii_general_ci NOT NULL,
                    `BuyerContactId` char(36) COLLATE ascii_general_ci NOT NULL,
                    `AccessLinkId` char(36) COLLATE ascii_general_ci NOT NULL,
                    `MessageId` char(36) COLLATE ascii_general_ci NOT NULL,
                    `DocumentId` char(36) COLLATE ascii_general_ci NOT NULL,
                    `FileName` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
                    `ContentType` varchar(160) CHARACTER SET utf8mb4 NOT NULL,
                    `FileSizeBytes` bigint NOT NULL,
                    `CreatedAtUtc` datetime(6) NOT NULL,
                    `UpdatedAtUtc` datetime(6) NOT NULL,
                    `CreatedByUserId` char(36) COLLATE ascii_general_ci NOT NULL,
                    `UpdatedByUserId` char(36) COLLATE ascii_general_ci NULL,
                    CONSTRAINT `PK_liens_SellingPortalMessageAttachments` PRIMARY KEY (`Id`),
                    CONSTRAINT `FK_liens_SellingPortalMessageAttachments_liens_Liens_LienId`
                        FOREIGN KEY (`LienId`) REFERENCES `liens_Liens` (`Id`) ON DELETE RESTRICT,
                    CONSTRAINT `FK_liens_SellingPortalMessageAttachments_liens_SellingBuyerAcce~`
                        FOREIGN KEY (`AccessLinkId`) REFERENCES `liens_SellingBuyerAccessLinks` (`Id`) ON DELETE RESTRICT,
                    CONSTRAINT `FK_liens_SellingPortalMessageAttachments_liens_SellingPortalMes~`
                        FOREIGN KEY (`MessageId`) REFERENCES `liens_SellingPortalMessages` (`Id`) ON DELETE CASCADE
                ) CHARACTER SET=utf8mb4
                """);

            SellingSchemaMigrationGuards.CreateIndexIfMissing(
                migrationBuilder,
                "liens_SellingPortalMessageAttachments",
                "IX_liens_SellingPortalMessageAttachments_AccessLinkId",
                "(`AccessLinkId`)");

            SellingSchemaMigrationGuards.CreateIndexIfMissing(
                migrationBuilder,
                "liens_SellingPortalMessageAttachments",
                "IX_liens_SellingPortalMessageAttachments_LienId",
                "(`LienId`)");

            SellingSchemaMigrationGuards.CreateIndexIfMissing(
                migrationBuilder,
                "liens_SellingPortalMessageAttachments",
                "IX_liens_SellingPortalMessageAttachments_MessageId",
                "(`MessageId`)");

            SellingSchemaMigrationGuards.CreateIndexIfMissing(
                migrationBuilder,
                "liens_SellingPortalMessageAttachments",
                "IX_SellingPortalMessageAttachments_Tenant_Document",
                "(`TenantId`, `DocumentId`)");

            SellingSchemaMigrationGuards.CreateIndexIfMissing(
                migrationBuilder,
                "liens_SellingPortalMessageAttachments",
                "IX_SellingPortalMessageAttachments_Tenant_Lien_Participants",
                "(`TenantId`, `LienId`, `SellerOrgId`, `BuyerOrgId`, `BuyerContactId`)");

            SellingSchemaMigrationGuards.CreateIndexIfMissing(
                migrationBuilder,
                "liens_SellingPortalMessageAttachments",
                "IX_SellingPortalMessageAttachments_Tenant_Message_Created",
                "(`TenantId`, `MessageId`, `CreatedAtUtc`)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "liens_SellingPortalMessageAttachments");
        }
    }
}
