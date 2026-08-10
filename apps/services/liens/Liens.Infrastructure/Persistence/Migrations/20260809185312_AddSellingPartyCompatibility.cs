using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Liens.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSellingPartyCompatibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var columns = new[]
            {
                ("liens_SellingPortfolioBuyers", "BuyerCompanyId"),
                ("liens_SellingBuyerAccessLinks", "BuyerCompanyContactPersonId"),
                ("liens_SellingBuyerAccessLinks", "BuyerCompanyId"),
                ("liens_Liens", "FundingCompanyCompanyId"),
                ("liens_Liens", "FundingCompanyContactPersonId"),
                ("liens_Liens", "MedicalFacilityCompanyId"),
                ("liens_Liens", "MedicalProviderCompanyId"),
                ("liens_LienOffers", "BuyerCompanyId"),
                ("liens_Cases", "CaseManagerContactPersonId"),
                ("liens_Cases", "HandlingLawFirmCompanyId"),
            };

            foreach (var (table, column) in columns)
            {
                SellingSchemaMigrationGuards.AddColumnIfMissing(
                    migrationBuilder,
                    table,
                    column,
                    "char(36) COLLATE ascii_general_ci NULL");
            }

            SellingSchemaMigrationGuards.CreateTableIfMissing(migrationBuilder, """
                CREATE TABLE IF NOT EXISTS `liens_SellingPartyAliases` (
                    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
                    `TenantId` char(36) COLLATE ascii_general_ci NOT NULL,
                    `ScopeKind` varchar(30) CHARACTER SET utf8mb4 NOT NULL,
                    `ScopeId` char(36) COLLATE ascii_general_ci NOT NULL,
                    `Namespace` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
                    `WorkflowProvenance` varchar(80) CHARACTER SET utf8mb4 NOT NULL,
                    `ExternalId` char(36) COLLATE ascii_general_ci NOT NULL,
                    `CompanyId` char(36) COLLATE ascii_general_ci NULL,
                    `CompanyContactPersonId` char(36) COLLATE ascii_general_ci NULL,
                    `IsPreferred` tinyint(1) NOT NULL,
                    `PreferredCompanyKey` char(36) COLLATE ascii_general_ci
                        AS (CASE WHEN `IsPreferred` = 1 THEN `CompanyId` ELSE NULL END) STORED NULL,
                    `PreferredContactPersonKey` char(36) COLLATE ascii_general_ci
                        AS (CASE WHEN `IsPreferred` = 1 THEN `CompanyContactPersonId` ELSE NULL END) STORED NULL,
                    `CreatedAtUtc` datetime(6) NOT NULL,
                    `UpdatedAtUtc` datetime(6) NOT NULL,
                    `CreatedByUserId` char(36) COLLATE ascii_general_ci NOT NULL,
                    `UpdatedByUserId` char(36) COLLATE ascii_general_ci NULL,
                    CONSTRAINT `PK_liens_SellingPartyAliases` PRIMARY KEY (`Id`),
                    CONSTRAINT `CK_SellingPartyAliases_ExactlyOneTarget`
                        CHECK ((`CompanyId` IS NOT NULL AND `CompanyContactPersonId` IS NULL)
                            OR (`CompanyId` IS NULL AND `CompanyContactPersonId` IS NOT NULL)),
                    CONSTRAINT `FK_liens_SellingPartyAliases_liens_Companies_CompanyId`
                        FOREIGN KEY (`CompanyId`) REFERENCES `liens_Companies` (`Id`) ON DELETE RESTRICT,
                    CONSTRAINT `FK_liens_SellingPartyAliases_liens_CompanyContactPersons_Compan~`
                        FOREIGN KEY (`CompanyContactPersonId`) REFERENCES `liens_CompanyContactPersons` (`Id`) ON DELETE RESTRICT
                ) CHARACTER SET=utf8mb4
                """);

            SellingSchemaMigrationGuards.CreateTableIfMissing(migrationBuilder, """
                CREATE TABLE IF NOT EXISTS `liens_SellingPartyBackfillCheckpoints` (
                    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
                    `TenantId` char(36) COLLATE ascii_general_ci NOT NULL,
                    `Workflow` varchar(80) CHARACTER SET utf8mb4 NOT NULL,
                    `LastExternalId` char(36) COLLATE ascii_general_ci NOT NULL,
                    `Status` varchar(30) CHARACTER SET utf8mb4 NOT NULL,
                    `ProcessedCount` int NOT NULL,
                    `QuarantinedCount` int NOT NULL,
                    `LastError` varchar(2000) CHARACTER SET utf8mb4 NULL,
                    `CreatedAtUtc` datetime(6) NOT NULL,
                    `UpdatedAtUtc` datetime(6) NOT NULL,
                    `CreatedByUserId` char(36) COLLATE ascii_general_ci NOT NULL,
                    `UpdatedByUserId` char(36) COLLATE ascii_general_ci NULL,
                    CONSTRAINT `PK_liens_SellingPartyBackfillCheckpoints` PRIMARY KEY (`Id`)
                ) CHARACTER SET=utf8mb4
                """);

            SellingSchemaMigrationGuards.CreateTableIfMissing(migrationBuilder, """
                CREATE TABLE IF NOT EXISTS `liens_SellingPartyBackfillQuarantines` (
                    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
                    `TenantId` char(36) COLLATE ascii_general_ci NOT NULL,
                    `Namespace` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
                    `WorkflowProvenance` varchar(80) CHARACTER SET utf8mb4 NOT NULL,
                    `ExternalId` char(36) COLLATE ascii_general_ci NOT NULL,
                    `ReasonCode` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
                    `Details` varchar(4000) CHARACTER SET utf8mb4 NOT NULL,
                    `CreatedAtUtc` datetime(6) NOT NULL,
                    `UpdatedAtUtc` datetime(6) NOT NULL,
                    `CreatedByUserId` char(36) COLLATE ascii_general_ci NOT NULL,
                    `UpdatedByUserId` char(36) COLLATE ascii_general_ci NULL,
                    CONSTRAINT `PK_liens_SellingPartyBackfillQuarantines` PRIMARY KEY (`Id`)
                ) CHARACTER SET=utf8mb4
                """);

            var indexes = new[]
            {
                ("liens_SellingPortfolioBuyers", "IX_liens_SellingPortfolioBuyers_BuyerCompanyId", "", "(`BuyerCompanyId`)"),
                ("liens_SellingBuyerAccessLinks", "IX_liens_SellingBuyerAccessLinks_BuyerCompanyContactPersonId", "", "(`BuyerCompanyContactPersonId`)"),
                ("liens_SellingBuyerAccessLinks", "IX_liens_SellingBuyerAccessLinks_BuyerCompanyId", "", "(`BuyerCompanyId`)"),
                ("liens_Liens", "IX_liens_Liens_FundingCompanyCompanyId", "", "(`FundingCompanyCompanyId`)"),
                ("liens_Liens", "IX_liens_Liens_FundingCompanyContactPersonId", "", "(`FundingCompanyContactPersonId`)"),
                ("liens_Liens", "IX_liens_Liens_MedicalFacilityCompanyId", "", "(`MedicalFacilityCompanyId`)"),
                ("liens_Liens", "IX_liens_Liens_MedicalProviderCompanyId", "", "(`MedicalProviderCompanyId`)"),
                ("liens_LienOffers", "IX_liens_LienOffers_BuyerCompanyId", "", "(`BuyerCompanyId`)"),
                ("liens_Cases", "IX_liens_Cases_CaseManagerContactPersonId", "", "(`CaseManagerContactPersonId`)"),
                ("liens_Cases", "IX_liens_Cases_HandlingLawFirmCompanyId", "", "(`HandlingLawFirmCompanyId`)"),
                ("liens_SellingPartyAliases", "IX_liens_SellingPartyAliases_CompanyContactPersonId", "", "(`CompanyContactPersonId`)"),
                ("liens_SellingPartyAliases", "IX_liens_SellingPartyAliases_CompanyId", "", "(`CompanyId`)"),
                ("liens_SellingPartyAliases", "UX_SellingPartyAliases_ExternalScope", "UNIQUE", "(`TenantId`, `ScopeKind`, `ScopeId`, `Namespace`, `WorkflowProvenance`, `ExternalId`)"),
                ("liens_SellingPartyAliases", "UX_SellingPartyAliases_PreferredCompany", "UNIQUE", "(`TenantId`, `ScopeKind`, `ScopeId`, `Namespace`, `WorkflowProvenance`, `PreferredCompanyKey`)"),
                ("liens_SellingPartyAliases", "UX_SellingPartyAliases_PreferredContact", "UNIQUE", "(`TenantId`, `ScopeKind`, `ScopeId`, `Namespace`, `WorkflowProvenance`, `PreferredContactPersonKey`)"),
                ("liens_SellingPartyBackfillCheckpoints", "UX_SellingPartyBackfillCheckpoints_Tenant_Workflow", "UNIQUE", "(`TenantId`, `Workflow`)"),
                ("liens_SellingPartyBackfillQuarantines", "UX_SellingPartyBackfillQuarantines_SourceReason", "UNIQUE", "(`TenantId`, `Namespace`, `WorkflowProvenance`, `ExternalId`, `ReasonCode`)"),
            };

            foreach (var (table, name, uniqueness, indexColumns) in indexes)
            {
                SellingSchemaMigrationGuards.CreateIndexIfMissing(
                    migrationBuilder,
                    table,
                    name,
                    indexColumns,
                    uniqueness == "UNIQUE");
            }

            var foreignKeys = new[]
            {
                ("liens_Cases", "FK_liens_Cases_liens_Companies_HandlingLawFirmCompanyId", "FOREIGN KEY (`HandlingLawFirmCompanyId`) REFERENCES `liens_Companies` (`Id`) ON DELETE RESTRICT"),
                ("liens_Cases", "FK_liens_Cases_liens_CompanyContactPersons_CaseManagerContactPe~", "FOREIGN KEY (`CaseManagerContactPersonId`) REFERENCES `liens_CompanyContactPersons` (`Id`) ON DELETE RESTRICT"),
                ("liens_LienOffers", "FK_liens_LienOffers_liens_Companies_BuyerCompanyId", "FOREIGN KEY (`BuyerCompanyId`) REFERENCES `liens_Companies` (`Id`) ON DELETE RESTRICT"),
                ("liens_Liens", "FK_liens_Liens_liens_Companies_FundingCompanyCompanyId", "FOREIGN KEY (`FundingCompanyCompanyId`) REFERENCES `liens_Companies` (`Id`) ON DELETE RESTRICT"),
                ("liens_Liens", "FK_liens_Liens_liens_Companies_MedicalFacilityCompanyId", "FOREIGN KEY (`MedicalFacilityCompanyId`) REFERENCES `liens_Companies` (`Id`) ON DELETE RESTRICT"),
                ("liens_Liens", "FK_liens_Liens_liens_Companies_MedicalProviderCompanyId", "FOREIGN KEY (`MedicalProviderCompanyId`) REFERENCES `liens_Companies` (`Id`) ON DELETE RESTRICT"),
                ("liens_Liens", "FK_liens_Liens_liens_CompanyContactPersons_FundingCompanyContac~", "FOREIGN KEY (`FundingCompanyContactPersonId`) REFERENCES `liens_CompanyContactPersons` (`Id`) ON DELETE RESTRICT"),
                ("liens_SellingBuyerAccessLinks", "FK_liens_SellingBuyerAccessLinks_liens_Companies_BuyerCompanyId", "FOREIGN KEY (`BuyerCompanyId`) REFERENCES `liens_Companies` (`Id`) ON DELETE RESTRICT"),
                ("liens_SellingBuyerAccessLinks", "FK_liens_SellingBuyerAccessLinks_liens_CompanyContactPersons_Bu~", "FOREIGN KEY (`BuyerCompanyContactPersonId`) REFERENCES `liens_CompanyContactPersons` (`Id`) ON DELETE RESTRICT"),
                ("liens_SellingPortfolioBuyers", "FK_liens_SellingPortfolioBuyers_liens_Companies_BuyerCompanyId", "FOREIGN KEY (`BuyerCompanyId`) REFERENCES `liens_Companies` (`Id`) ON DELETE RESTRICT"),
                ("liens_SellingPartyAliases", "FK_liens_SellingPartyAliases_liens_Companies_CompanyId", "FOREIGN KEY (`CompanyId`) REFERENCES `liens_Companies` (`Id`) ON DELETE RESTRICT"),
                ("liens_SellingPartyAliases", "FK_liens_SellingPartyAliases_liens_CompanyContactPersons_Compan~", "FOREIGN KEY (`CompanyContactPersonId`) REFERENCES `liens_CompanyContactPersons` (`Id`) ON DELETE RESTRICT"),
            };

            foreach (var (table, name, definition) in foreignKeys)
            {
                SellingSchemaMigrationGuards.AddForeignKeyIfMissing(
                    migrationBuilder,
                    table,
                    name,
                    definition);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_liens_Cases_liens_Companies_HandlingLawFirmCompanyId",
                table: "liens_Cases");

            migrationBuilder.DropForeignKey(
                name: "FK_liens_Cases_liens_CompanyContactPersons_CaseManagerContactPe~",
                table: "liens_Cases");

            migrationBuilder.DropForeignKey(
                name: "FK_liens_LienOffers_liens_Companies_BuyerCompanyId",
                table: "liens_LienOffers");

            migrationBuilder.DropForeignKey(
                name: "FK_liens_Liens_liens_Companies_FundingCompanyCompanyId",
                table: "liens_Liens");

            migrationBuilder.DropForeignKey(
                name: "FK_liens_Liens_liens_Companies_MedicalFacilityCompanyId",
                table: "liens_Liens");

            migrationBuilder.DropForeignKey(
                name: "FK_liens_Liens_liens_Companies_MedicalProviderCompanyId",
                table: "liens_Liens");

            migrationBuilder.DropForeignKey(
                name: "FK_liens_Liens_liens_CompanyContactPersons_FundingCompanyContac~",
                table: "liens_Liens");

            migrationBuilder.DropForeignKey(
                name: "FK_liens_SellingBuyerAccessLinks_liens_Companies_BuyerCompanyId",
                table: "liens_SellingBuyerAccessLinks");

            migrationBuilder.DropForeignKey(
                name: "FK_liens_SellingBuyerAccessLinks_liens_CompanyContactPersons_Bu~",
                table: "liens_SellingBuyerAccessLinks");

            migrationBuilder.DropForeignKey(
                name: "FK_liens_SellingPortfolioBuyers_liens_Companies_BuyerCompanyId",
                table: "liens_SellingPortfolioBuyers");

            migrationBuilder.DropTable(
                name: "liens_SellingPartyAliases");

            migrationBuilder.DropTable(
                name: "liens_SellingPartyBackfillCheckpoints");

            migrationBuilder.DropTable(
                name: "liens_SellingPartyBackfillQuarantines");

            migrationBuilder.DropIndex(
                name: "IX_liens_SellingPortfolioBuyers_BuyerCompanyId",
                table: "liens_SellingPortfolioBuyers");

            migrationBuilder.DropIndex(
                name: "IX_liens_SellingBuyerAccessLinks_BuyerCompanyContactPersonId",
                table: "liens_SellingBuyerAccessLinks");

            migrationBuilder.DropIndex(
                name: "IX_liens_SellingBuyerAccessLinks_BuyerCompanyId",
                table: "liens_SellingBuyerAccessLinks");

            migrationBuilder.DropIndex(
                name: "IX_liens_Liens_FundingCompanyCompanyId",
                table: "liens_Liens");

            migrationBuilder.DropIndex(
                name: "IX_liens_Liens_FundingCompanyContactPersonId",
                table: "liens_Liens");

            migrationBuilder.DropIndex(
                name: "IX_liens_Liens_MedicalFacilityCompanyId",
                table: "liens_Liens");

            migrationBuilder.DropIndex(
                name: "IX_liens_Liens_MedicalProviderCompanyId",
                table: "liens_Liens");

            migrationBuilder.DropIndex(
                name: "IX_liens_LienOffers_BuyerCompanyId",
                table: "liens_LienOffers");

            migrationBuilder.DropIndex(
                name: "IX_liens_Cases_CaseManagerContactPersonId",
                table: "liens_Cases");

            migrationBuilder.DropIndex(
                name: "IX_liens_Cases_HandlingLawFirmCompanyId",
                table: "liens_Cases");

            migrationBuilder.DropColumn(
                name: "BuyerCompanyId",
                table: "liens_SellingPortfolioBuyers");

            migrationBuilder.DropColumn(
                name: "BuyerCompanyContactPersonId",
                table: "liens_SellingBuyerAccessLinks");

            migrationBuilder.DropColumn(
                name: "BuyerCompanyId",
                table: "liens_SellingBuyerAccessLinks");

            migrationBuilder.DropColumn(
                name: "FundingCompanyCompanyId",
                table: "liens_Liens");

            migrationBuilder.DropColumn(
                name: "FundingCompanyContactPersonId",
                table: "liens_Liens");

            migrationBuilder.DropColumn(
                name: "MedicalFacilityCompanyId",
                table: "liens_Liens");

            migrationBuilder.DropColumn(
                name: "MedicalProviderCompanyId",
                table: "liens_Liens");

            migrationBuilder.DropColumn(
                name: "BuyerCompanyId",
                table: "liens_LienOffers");

            migrationBuilder.DropColumn(
                name: "CaseManagerContactPersonId",
                table: "liens_Cases");

            migrationBuilder.DropColumn(
                name: "HandlingLawFirmCompanyId",
                table: "liens_Cases");
        }
    }
}
