using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Liens.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSellingCaseDraft : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // MySQL auto-commits DDL. Creating the table separately from its
            // foreign keys makes this migration retryable if a prior startup
            // failed while the Selling schema was being introduced.
            SellingSchemaMigrationGuards.CreateTableIfMissing(
                migrationBuilder,
                """
                CREATE TABLE IF NOT EXISTS `liens_SellingCaseDrafts` (
                    `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
                    `TenantId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
                    `OrgId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
                    `CaseStatus` varchar(50) NOT NULL,
                    `AccidentTypeId` varchar(100) NULL,
                    `AccidentState` varchar(100) NULL,
                    `DateOfLoss` date NULL,
                    `HandlingLawFirmCompanyId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NULL,
                    `CaseManagerContactPersonId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NULL,
                    `CaseTrackingNotes` varchar(4000) NULL,
                    `CaseId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NULL,
                    `FinalizedAtUtc` datetime(6) NULL,
                    `CreatedAtUtc` datetime(6) NOT NULL,
                    `UpdatedAtUtc` datetime(6) NOT NULL,
                    `CreatedByUserId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
                    `UpdatedByUserId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NULL,
                    CONSTRAINT `PK_liens_SellingCaseDrafts` PRIMARY KEY (`Id`)
                ) CHARACTER SET=utf8mb4
                """);

            SellingSchemaMigrationGuards.CreateIndexIfMissing(
                migrationBuilder, "liens_SellingCaseDrafts", "IX_liens_SellingCaseDrafts_CaseManagerContactPersonId",
                "(`CaseManagerContactPersonId`)");
            SellingSchemaMigrationGuards.CreateIndexIfMissing(
                migrationBuilder, "liens_SellingCaseDrafts", "IX_liens_SellingCaseDrafts_HandlingLawFirmCompanyId",
                "(`HandlingLawFirmCompanyId`)");
            SellingSchemaMigrationGuards.CreateIndexIfMissing(
                migrationBuilder, "liens_SellingCaseDrafts", "IX_SellingCaseDrafts_Tenant_Org_CreatedAtUtc",
                "(`TenantId`, `OrgId`, `CreatedAtUtc`)");
            SellingSchemaMigrationGuards.CreateIndexIfMissing(
                migrationBuilder, "liens_SellingCaseDrafts", "IX_SellingCaseDrafts_Tenant_Org_FinalizedAtUtc",
                "(`TenantId`, `OrgId`, `FinalizedAtUtc`)");
            SellingSchemaMigrationGuards.CreateIndexIfMissing(
                migrationBuilder, "liens_SellingCaseDrafts", "UX_SellingCaseDrafts_CaseId", "(`CaseId`)", unique: true);

            SellingSchemaMigrationGuards.AddForeignKeyIfMissing(
                migrationBuilder, "liens_SellingCaseDrafts", "FK_liens_SellingCaseDrafts_liens_Cases_CaseId",
                "FOREIGN KEY (`CaseId`) REFERENCES `liens_Cases` (`Id`) ON DELETE RESTRICT");
            SellingSchemaMigrationGuards.AddForeignKeyIfMissing(
                migrationBuilder, "liens_SellingCaseDrafts", "FK_liens_SellingCaseDrafts_liens_Companies_HandlingLawFirmCompa~",
                "FOREIGN KEY (`HandlingLawFirmCompanyId`) REFERENCES `liens_Companies` (`Id`) ON DELETE RESTRICT");
            SellingSchemaMigrationGuards.AddForeignKeyIfMissing(
                migrationBuilder, "liens_SellingCaseDrafts", "FK_liens_SellingCaseDrafts_liens_CompanyContactPersons_CaseMana~",
                "FOREIGN KEY (`CaseManagerContactPersonId`) REFERENCES `liens_CompanyContactPersons` (`Id`) ON DELETE RESTRICT");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new InvalidOperationException(
                "This migration is forward-only because selling case drafts may contain plaintiff PII. " +
                "Use an audited data export and a forward corrective migration rather than dropping the draft table.");
        }
    }
}
