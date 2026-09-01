using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Liens.Infrastructure.Persistence.Migrations;

[DbContext(typeof(LiensDbContext))]
[Migration("20260825160000_AddLegacyReportParityFields")]
public partial class AddLegacyReportParityFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        var columns = new[]
        {
            ("AttorneyContactPersonId", "char(36) COLLATE ascii_general_ci NULL"),
            ("CaseDropped", "tinyint(1) NULL"),
            ("ClientAddressLine1", "varchar(300) CHARACTER SET utf8mb4 NULL"),
            ("ClientCity", "varchar(100) CHARACTER SET utf8mb4 NULL"),
            ("ClientPostalCode", "varchar(20) CHARACTER SET utf8mb4 NULL"),
            ("ClientState", "varchar(100) CHARACTER SET utf8mb4 NULL"),
            ("CurrentMedicalStatus", "varchar(50) CHARACTER SET utf8mb4 NULL"),
            ("ImportedCreatedByName", "varchar(100) CHARACTER SET utf8mb4 NULL"),
            ("IncidentState", "varchar(100) CHARACTER SET utf8mb4 NULL"),
            ("MinorComp", "tinyint(1) NULL"),
            ("TrackingFollowUpDate", "date NULL"),
        };

        foreach (var (column, definition) in columns)
            SellingSchemaMigrationGuards.AddColumnIfMissing(migrationBuilder, "liens_Cases", column, definition);

        SellingSchemaMigrationGuards.CreateTableIfMissing(migrationBuilder, """
            CREATE TABLE IF NOT EXISTS `liens_LegacyFieldMigrationStates` (
                `Id` char(36) COLLATE ascii_general_ci NOT NULL,
                `TenantId` char(36) COLLATE ascii_general_ci NOT NULL,
                `SourceSystem` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
                `SourceTable` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
                `LegacyId` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
                `MappingVersion` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
                `FieldGroup` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
                `TargetEntity` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
                `TargetId` char(36) COLLATE ascii_general_ci NOT NULL,
                `SourceHash` varchar(128) CHARACTER SET utf8mb4 NOT NULL,
                `TargetPreimageHash` varchar(128) CHARACTER SET utf8mb4 NULL,
                `AppliedValueHash` varchar(128) CHARACTER SET utf8mb4 NULL,
                `Status` varchar(30) CHARACTER SET utf8mb4 NOT NULL,
                `ImportRunId` char(36) COLLATE ascii_general_ci NOT NULL,
                `AppliedAtUtc` datetime(6) NULL,
                `CreatedAtUtc` datetime(6) NOT NULL,
                CONSTRAINT `PK_liens_LegacyFieldMigrationStates` PRIMARY KEY (`Id`),
                CONSTRAINT `FK_LegacyFieldMigrationStates_ImportRun`
                    FOREIGN KEY (`ImportRunId`) REFERENCES `liens_LegacyImportRuns` (`Id`) ON DELETE RESTRICT
            ) CHARACTER SET=utf8mb4
            """);

        SellingSchemaMigrationGuards.CreateIndexIfMissing(
            migrationBuilder, "liens_Cases", "IX_Cases_AttorneyContactPersonId", "(`AttorneyContactPersonId`)");
        SellingSchemaMigrationGuards.CreateIndexIfMissing(
            migrationBuilder, "liens_LegacyFieldMigrationStates", "IX_LegacyFieldMigrationStates_ImportRunId", "(`ImportRunId`)");
        SellingSchemaMigrationGuards.CreateIndexIfMissing(
            migrationBuilder,
            "liens_LegacyFieldMigrationStates",
            "UX_LegacyFieldMigrationStates_Source_FieldGroup",
            "(`TenantId`, `SourceSystem`, `SourceTable`, `LegacyId`, `MappingVersion`, `FieldGroup`)",
            unique: true);
        SellingSchemaMigrationGuards.AddForeignKeyIfMissing(
            migrationBuilder,
            "liens_Cases",
            "FK_Cases_AttorneyContactPerson",
            "FOREIGN KEY (`AttorneyContactPersonId`) REFERENCES `liens_CompanyContactPersons` (`Id`) ON DELETE RESTRICT");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "liens_LegacyFieldMigrationStates");
        migrationBuilder.DropForeignKey(name: "FK_Cases_AttorneyContactPerson", table: "liens_Cases");
        migrationBuilder.DropIndex(name: "IX_Cases_AttorneyContactPersonId", table: "liens_Cases");
        migrationBuilder.DropColumn(name: "AttorneyContactPersonId", table: "liens_Cases");
        migrationBuilder.DropColumn(name: "CaseDropped", table: "liens_Cases");
        migrationBuilder.DropColumn(name: "ClientAddressLine1", table: "liens_Cases");
        migrationBuilder.DropColumn(name: "ClientCity", table: "liens_Cases");
        migrationBuilder.DropColumn(name: "ClientPostalCode", table: "liens_Cases");
        migrationBuilder.DropColumn(name: "ClientState", table: "liens_Cases");
        migrationBuilder.DropColumn(name: "CurrentMedicalStatus", table: "liens_Cases");
        migrationBuilder.DropColumn(name: "ImportedCreatedByName", table: "liens_Cases");
        migrationBuilder.DropColumn(name: "IncidentState", table: "liens_Cases");
        migrationBuilder.DropColumn(name: "MinorComp", table: "liens_Cases");
        migrationBuilder.DropColumn(name: "TrackingFollowUpDate", table: "liens_Cases");
    }
}
