using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Liens.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSellingCompanyDirectory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // MySQL DDL is not fully transactional. These guards let EF resume this
            // migration after a production rollout stopped between statements.
            SellingSchemaMigrationGuards.CreateTableIfMissing(migrationBuilder, """
                CREATE TABLE IF NOT EXISTS `liens_CompanyTypes` (
                    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
                    `Code` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
                    `Name` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
                    `SortOrder` int NOT NULL,
                    `IsActive` tinyint(1) NOT NULL,
                    `CreatedAtUtc` datetime(6) NOT NULL,
                    `UpdatedAtUtc` datetime(6) NOT NULL,
                    `CreatedByUserId` char(36) COLLATE ascii_general_ci NOT NULL,
                    `UpdatedByUserId` char(36) COLLATE ascii_general_ci NULL,
                    CONSTRAINT `PK_liens_CompanyTypes` PRIMARY KEY (`Id`)
                ) CHARACTER SET=utf8mb4
                """);

            SellingSchemaMigrationGuards.CreateTableIfMissing(migrationBuilder, """
                CREATE TABLE IF NOT EXISTS `liens_Companies` (
                    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
                    `TenantId` char(36) COLLATE ascii_general_ci NOT NULL,
                    `OrgId` char(36) COLLATE ascii_general_ci NOT NULL,
                    `LinkedTenantId` char(36) COLLATE ascii_general_ci NULL,
                    `CompanyTypeId` char(36) COLLATE ascii_general_ci NOT NULL,
                    `Name` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
                    `NormalizedName` varchar(200) CHARACTER SET utf8mb4 NOT NULL,
                    `AddressLine1` varchar(300) CHARACTER SET utf8mb4 NULL,
                    `City` varchar(100) CHARACTER SET utf8mb4 NULL,
                    `State` varchar(100) CHARACTER SET utf8mb4 NULL,
                    `PostalCode` varchar(20) CHARACTER SET utf8mb4 NULL,
                    `Phone` varchar(30) CHARACTER SET utf8mb4 NULL,
                    `Email` varchar(320) CHARACTER SET utf8mb4 NULL,
                    `IsActive` tinyint(1) NOT NULL,
                    `CreatedAtUtc` datetime(6) NOT NULL,
                    `UpdatedAtUtc` datetime(6) NOT NULL,
                    `CreatedByUserId` char(36) COLLATE ascii_general_ci NOT NULL,
                    `UpdatedByUserId` char(36) COLLATE ascii_general_ci NULL,
                    CONSTRAINT `PK_liens_Companies` PRIMARY KEY (`Id`),
                    CONSTRAINT `FK_liens_Companies_liens_CompanyTypes_CompanyTypeId`
                        FOREIGN KEY (`CompanyTypeId`) REFERENCES `liens_CompanyTypes` (`Id`) ON DELETE RESTRICT
                ) CHARACTER SET=utf8mb4
                """);

            SellingSchemaMigrationGuards.CreateTableIfMissing(migrationBuilder, """
                CREATE TABLE IF NOT EXISTS `liens_ContactPersonTypes` (
                    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
                    `CompanyTypeId` char(36) COLLATE ascii_general_ci NOT NULL,
                    `Code` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
                    `Name` varchar(150) CHARACTER SET utf8mb4 NOT NULL,
                    `SortOrder` int NOT NULL,
                    `IsActive` tinyint(1) NOT NULL,
                    `CreatedAtUtc` datetime(6) NOT NULL,
                    `UpdatedAtUtc` datetime(6) NOT NULL,
                    `CreatedByUserId` char(36) COLLATE ascii_general_ci NOT NULL,
                    `UpdatedByUserId` char(36) COLLATE ascii_general_ci NULL,
                    CONSTRAINT `PK_liens_ContactPersonTypes` PRIMARY KEY (`Id`),
                    CONSTRAINT `FK_liens_ContactPersonTypes_liens_CompanyTypes_CompanyTypeId`
                        FOREIGN KEY (`CompanyTypeId`) REFERENCES `liens_CompanyTypes` (`Id`) ON DELETE RESTRICT
                ) CHARACTER SET=utf8mb4
                """);

            SellingSchemaMigrationGuards.CreateTableIfMissing(migrationBuilder, """
                CREATE TABLE IF NOT EXISTS `liens_CompanyContactPersons` (
                    `Id` char(36) COLLATE ascii_general_ci NOT NULL,
                    `TenantId` char(36) COLLATE ascii_general_ci NOT NULL,
                    `CompanyId` char(36) COLLATE ascii_general_ci NOT NULL,
                    `ContactPersonTypeId` char(36) COLLATE ascii_general_ci NOT NULL,
                    `FirstName` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
                    `LastName` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
                    `AddressLine1` varchar(300) CHARACTER SET utf8mb4 NULL,
                    `City` varchar(100) CHARACTER SET utf8mb4 NULL,
                    `State` varchar(100) CHARACTER SET utf8mb4 NULL,
                    `PostalCode` varchar(20) CHARACTER SET utf8mb4 NULL,
                    `Phone` varchar(30) CHARACTER SET utf8mb4 NULL,
                    `Email` varchar(320) CHARACTER SET utf8mb4 NULL,
                    `IsActive` tinyint(1) NOT NULL,
                    `CreatedAtUtc` datetime(6) NOT NULL,
                    `UpdatedAtUtc` datetime(6) NOT NULL,
                    `CreatedByUserId` char(36) COLLATE ascii_general_ci NOT NULL,
                    `UpdatedByUserId` char(36) COLLATE ascii_general_ci NULL,
                    CONSTRAINT `PK_liens_CompanyContactPersons` PRIMARY KEY (`Id`),
                    CONSTRAINT `FK_liens_CompanyContactPersons_liens_Companies_CompanyId`
                        FOREIGN KEY (`CompanyId`) REFERENCES `liens_Companies` (`Id`) ON DELETE RESTRICT,
                    CONSTRAINT `FK_liens_CompanyContactPersons_liens_ContactPersonTypes_Contact~`
                        FOREIGN KEY (`ContactPersonTypeId`) REFERENCES `liens_ContactPersonTypes` (`Id`) ON DELETE RESTRICT
                ) CHARACTER SET=utf8mb4
                """);

            SellingSchemaMigrationGuards.AddForeignKeyIfMissing(
                migrationBuilder,
                "liens_Companies",
                "FK_liens_Companies_liens_CompanyTypes_CompanyTypeId",
                "FOREIGN KEY (`CompanyTypeId`) REFERENCES `liens_CompanyTypes` (`Id`) ON DELETE RESTRICT");
            SellingSchemaMigrationGuards.AddForeignKeyIfMissing(
                migrationBuilder,
                "liens_ContactPersonTypes",
                "FK_liens_ContactPersonTypes_liens_CompanyTypes_CompanyTypeId",
                "FOREIGN KEY (`CompanyTypeId`) REFERENCES `liens_CompanyTypes` (`Id`) ON DELETE RESTRICT");
            SellingSchemaMigrationGuards.AddForeignKeyIfMissing(
                migrationBuilder,
                "liens_CompanyContactPersons",
                "FK_liens_CompanyContactPersons_liens_Companies_CompanyId",
                "FOREIGN KEY (`CompanyId`) REFERENCES `liens_Companies` (`Id`) ON DELETE RESTRICT");
            SellingSchemaMigrationGuards.AddForeignKeyIfMissing(
                migrationBuilder,
                "liens_CompanyContactPersons",
                "FK_liens_CompanyContactPersons_liens_ContactPersonTypes_Contact~",
                "FOREIGN KEY (`ContactPersonTypeId`) REFERENCES `liens_ContactPersonTypes` (`Id`) ON DELETE RESTRICT");

            var indexes = new[]
            {
                ("liens_Companies", "IX_Companies_LinkedTenantId", "", "(`LinkedTenantId`)"),
                ("liens_Companies", "IX_Companies_TenantId_OrgId_CompanyTypeId_IsActive", "", "(`TenantId`, `OrgId`, `CompanyTypeId`, `IsActive`)"),
                ("liens_Companies", "IX_liens_Companies_CompanyTypeId", "", "(`CompanyTypeId`)"),
                ("liens_Companies", "UX_Companies_TenantId_OrgId_CompanyTypeId_NormalizedName", "UNIQUE", "(`TenantId`, `OrgId`, `CompanyTypeId`, `NormalizedName`)"),
                ("liens_CompanyContactPersons", "IX_CompanyContactPersons_CompanyId_ContactPersonTypeId", "", "(`CompanyId`, `ContactPersonTypeId`)"),
                ("liens_CompanyContactPersons", "IX_CompanyContactPersons_TenantId_CompanyId_IsActive_Name", "", "(`TenantId`, `CompanyId`, `IsActive`, `LastName`, `FirstName`)"),
                ("liens_CompanyContactPersons", "IX_liens_CompanyContactPersons_ContactPersonTypeId", "", "(`ContactPersonTypeId`)"),
                ("liens_CompanyTypes", "UX_CompanyTypes_Code", "UNIQUE", "(`Code`)"),
                ("liens_ContactPersonTypes", "IX_ContactPersonTypes_CompanyTypeId_IsActive_SortOrder", "", "(`CompanyTypeId`, `IsActive`, `SortOrder`)"),
                ("liens_ContactPersonTypes", "UX_ContactPersonTypes_CompanyTypeId_Code", "UNIQUE", "(`CompanyTypeId`, `Code`)"),
            };

            foreach (var (table, name, uniqueness, columns) in indexes)
            {
                SellingSchemaMigrationGuards.CreateIndexIfMissing(
                    migrationBuilder,
                    table,
                    name,
                    columns,
                    uniqueness == "UNIQUE");
            }

            var companyTypes = new[]
            {
                ("10000000-0000-0000-0000-000000000001", "LawFirm", "Law Firm", 1),
                ("10000000-0000-0000-0000-000000000002", "FundingCompany", "Funding Company", 2),
                ("10000000-0000-0000-0000-000000000003", "MedicalProvider", "Medical Provider", 3),
                ("10000000-0000-0000-0000-000000000004", "MedicalFacility", "Medical Facility", 4),
            };

            foreach (var (id, code, name, sortOrder) in companyTypes)
            {
                SellingSchemaMigrationGuards.ExecuteSql(migrationBuilder, $"""
                    INSERT IGNORE INTO `liens_CompanyTypes`
                        (`Id`, `Code`, `Name`, `SortOrder`, `IsActive`, `CreatedAtUtc`, `UpdatedAtUtc`, `CreatedByUserId`, `UpdatedByUserId`)
                    VALUES
                        ('{id}', '{code}', '{name}', {sortOrder}, 1, '2026-08-09 00:00:00', '2026-08-09 00:00:00',
                         '00000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-000000000001')
                    """);
            }

            var contactTypes = new[]
            {
                ("20000000-0000-0000-0000-000000000001", "10000000-0000-0000-0000-000000000001", "Attorney", "Attorney", 1),
                ("20000000-0000-0000-0000-000000000002", "10000000-0000-0000-0000-000000000001", "Paralegal", "Paralegal", 2),
                ("20000000-0000-0000-0000-000000000003", "10000000-0000-0000-0000-000000000001", "CaseManager", "Case Manager", 3),
                ("20000000-0000-0000-0000-000000000004", "10000000-0000-0000-0000-000000000001", "IntakeSpecialist", "Intake Specialist", 4),
                ("20000000-0000-0000-0000-000000000005", "10000000-0000-0000-0000-000000000001", "LegalAssistant", "Legal Assistant", 5),
                ("20000000-0000-0000-0000-000000000006", "10000000-0000-0000-0000-000000000001", "BillingSpecialist", "Billing Specialist", 6),
                ("20000000-0000-0000-0000-000000000007", "10000000-0000-0000-0000-000000000001", "FirmAdministrator", "Firm Administrator", 7),
                ("20000000-0000-0000-0000-000000000008", "10000000-0000-0000-0000-000000000002", "Underwriter", "Underwriter", 1),
                ("20000000-0000-0000-0000-000000000009", "10000000-0000-0000-0000-000000000002", "FundingSpecialist", "Funding Specialist", 2),
                ("20000000-0000-0000-0000-000000000010", "10000000-0000-0000-0000-000000000002", "AccountManager", "Account Manager", 3),
                ("20000000-0000-0000-0000-000000000011", "10000000-0000-0000-0000-000000000002", "CollectionsSpecialist", "Collections Specialist", 4),
                ("20000000-0000-0000-0000-000000000012", "10000000-0000-0000-0000-000000000002", "ComplianceOfficer", "Compliance Officer", 5),
                ("20000000-0000-0000-0000-000000000013", "10000000-0000-0000-0000-000000000002", "FinanceManager", "Finance Manager", 6),
                ("20000000-0000-0000-0000-000000000014", "10000000-0000-0000-0000-000000000002", "CompanyAdministrator", "Company Administrator", 7),
                ("20000000-0000-0000-0000-000000000015", "10000000-0000-0000-0000-000000000003", "Physician", "Physician", 1),
                ("20000000-0000-0000-0000-000000000016", "10000000-0000-0000-0000-000000000003", "Chiropractor", "Chiropractor", 2),
                ("20000000-0000-0000-0000-000000000017", "10000000-0000-0000-0000-000000000003", "Therapist", "Therapist", 3),
                ("20000000-0000-0000-0000-000000000018", "10000000-0000-0000-0000-000000000003", "NursePractitioner", "Nurse Practitioner", 4),
                ("20000000-0000-0000-0000-000000000019", "10000000-0000-0000-0000-000000000003", "ProviderRepresentative", "Provider Representative", 5),
                ("20000000-0000-0000-0000-000000000020", "10000000-0000-0000-0000-000000000003", "BillingSpecialist", "Billing Specialist", 6),
                ("20000000-0000-0000-0000-000000000021", "10000000-0000-0000-0000-000000000003", "MedicalRecordsCoordinator", "Medical Records Coordinator", 7),
                ("20000000-0000-0000-0000-000000000022", "10000000-0000-0000-0000-000000000004", "FacilityAdministrator", "Facility Administrator", 1),
                ("20000000-0000-0000-0000-000000000023", "10000000-0000-0000-0000-000000000004", "PracticeManager", "Practice Manager", 2),
                ("20000000-0000-0000-0000-000000000024", "10000000-0000-0000-0000-000000000004", "FrontDeskIntakeStaff", "Front Desk/Intake Staff", 3),
                ("20000000-0000-0000-0000-000000000025", "10000000-0000-0000-0000-000000000004", "Scheduler", "Scheduler", 4),
                ("20000000-0000-0000-0000-000000000026", "10000000-0000-0000-0000-000000000004", "CareCoordinator", "Care Coordinator", 5),
                ("20000000-0000-0000-0000-000000000027", "10000000-0000-0000-0000-000000000004", "BillingSpecialist", "Billing Specialist", 6),
                ("20000000-0000-0000-0000-000000000028", "10000000-0000-0000-0000-000000000004", "MedicalRecordsSpecialist", "Medical Records Specialist", 7),
            };

            foreach (var (id, companyTypeId, code, name, sortOrder) in contactTypes)
            {
                SellingSchemaMigrationGuards.ExecuteSql(migrationBuilder, $"""
                    INSERT IGNORE INTO `liens_ContactPersonTypes`
                        (`Id`, `CompanyTypeId`, `Code`, `Name`, `SortOrder`, `IsActive`, `CreatedAtUtc`, `UpdatedAtUtc`, `CreatedByUserId`, `UpdatedByUserId`)
                    VALUES
                        ('{id}', '{companyTypeId}', '{code}', '{name}', {sortOrder}, 1, '2026-08-09 00:00:00', '2026-08-09 00:00:00',
                         '00000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-000000000001')
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "liens_CompanyContactPersons");

            migrationBuilder.DropTable(
                name: "liens_Companies");

            migrationBuilder.DropTable(
                name: "liens_ContactPersonTypes");

            migrationBuilder.DropTable(
                name: "liens_CompanyTypes");
        }
    }
}
