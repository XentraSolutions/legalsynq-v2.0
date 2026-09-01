using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Liens.Infrastructure.Persistence.Migrations;

[DbContext(typeof(LiensDbContext))]
[Migration("20260827100000_AddSellingCaseDraftConcurrencyToken")]
public partial class AddSellingCaseDraftConcurrencyToken : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        SellingSchemaMigrationGuards.AddColumnIfMissing(
            migrationBuilder,
            "liens_SellingCaseDrafts",
            "ConcurrencyToken",
            "char(36) COLLATE ascii_general_ci NULL");

        SellingSchemaMigrationGuards.ExecuteSql(
            migrationBuilder,
            """
            UPDATE `liens_SellingCaseDrafts`
            SET `ConcurrencyToken` = UUID()
            WHERE `ConcurrencyToken` IS NULL
               OR `ConcurrencyToken` = '00000000-0000-0000-0000-000000000000'
            """);

        SellingSchemaMigrationGuards.ExecuteSql(
            migrationBuilder,
            """
            ALTER TABLE `liens_SellingCaseDrafts`
            MODIFY COLUMN `ConcurrencyToken` char(36) COLLATE ascii_general_ci NOT NULL
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropColumn(
            name: "ConcurrencyToken",
            table: "liens_SellingCaseDrafts");
}
