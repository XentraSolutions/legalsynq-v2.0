using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Liens.Infrastructure.Persistence.Migrations;

[DbContext(typeof(LiensDbContext))]
[Migration("20260826090000_AddLienSellingCaseReference")]
public partial class AddLienSellingCaseReference : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        SellingSchemaMigrationGuards.AddColumnIfMissing(
            migrationBuilder,
            "liens_Liens",
            "SellingCaseId",
            "char(36) COLLATE ascii_general_ci NULL");

        SellingSchemaMigrationGuards.AddColumnIfMissing(
            migrationBuilder,
            "liens_Liens",
            "MovedToManagementAtUtc",
            "datetime(6) NULL");

        SellingSchemaMigrationGuards.CreateIndexIfMissing(
            migrationBuilder,
            "liens_Liens",
            "IX_Liens_SellingCaseId",
            "(`SellingCaseId`)");

        SellingSchemaMigrationGuards.CreateIndexIfMissing(
            migrationBuilder,
            "liens_Liens",
            "IX_Liens_TenantId_SellingCaseId",
            "(`TenantId`, `SellingCaseId`)");

        SellingSchemaMigrationGuards.AddForeignKeyIfMissing(
            migrationBuilder,
            "liens_Liens",
            "FK_liens_Liens_liens_Cases_SellingCaseId",
            "FOREIGN KEY (`SellingCaseId`) REFERENCES `liens_Cases` (`Id`) ON DELETE RESTRICT");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_liens_Liens_liens_Cases_SellingCaseId",
            table: "liens_Liens");

        migrationBuilder.DropIndex(
            name: "IX_Liens_SellingCaseId",
            table: "liens_Liens");

        migrationBuilder.DropIndex(
            name: "IX_Liens_TenantId_SellingCaseId",
            table: "liens_Liens");

        migrationBuilder.DropColumn(
            name: "SellingCaseId",
            table: "liens_Liens");

        migrationBuilder.DropColumn(
            name: "MovedToManagementAtUtc",
            table: "liens_Liens");
    }
}
