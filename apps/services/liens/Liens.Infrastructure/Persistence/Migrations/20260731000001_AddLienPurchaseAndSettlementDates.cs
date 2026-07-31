using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Liens.Infrastructure.Persistence.Migrations;

[DbContext(typeof(LiensDbContext))]
[Migration("20260731000001_AddLienPurchaseAndSettlementDates")]
public partial class AddLienPurchaseAndSettlementDates : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateOnly>(
            name: "PurchaseDate",
            table: "liens_Liens",
            type: "date",
            nullable: true);

        migrationBuilder.AddColumn<DateOnly>(
            name: "SettlementDate",
            table: "liens_LienSettlements",
            type: "date",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Liens_TenantId_PurchaseDate",
            table: "liens_Liens",
            columns: new[] { "TenantId", "PurchaseDate" });

        migrationBuilder.CreateIndex(
            name: "IX_LienSettlements_TenantId_SettlementDate",
            table: "liens_LienSettlements",
            columns: new[] { "TenantId", "SettlementDate" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Liens_TenantId_PurchaseDate",
            table: "liens_Liens");

        migrationBuilder.DropIndex(
            name: "IX_LienSettlements_TenantId_SettlementDate",
            table: "liens_LienSettlements");

        migrationBuilder.DropColumn(
            name: "PurchaseDate",
            table: "liens_Liens");

        migrationBuilder.DropColumn(
            name: "SettlementDate",
            table: "liens_LienSettlements");
    }
}
