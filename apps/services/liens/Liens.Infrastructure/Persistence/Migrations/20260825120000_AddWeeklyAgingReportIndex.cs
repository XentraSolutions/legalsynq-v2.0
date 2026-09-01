using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Liens.Infrastructure.Persistence.Migrations;

[DbContext(typeof(LiensDbContext))]
[Migration("20260825120000_AddWeeklyAgingReportIndex")]
public partial class AddWeeklyAgingReportIndex : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        SellingSchemaMigrationGuards.CreateIndexIfMissing(
            migrationBuilder,
            "liens_SellingBuyerAccessLinks",
            "IX_SellingBuyerAccessLinks_WeeklyAging",
            "(`TenantId`, `SellerOrgId`, `Purpose`, `ResponseStatus`, `RespondedAtUtc`, `LienId`)");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        SellingSchemaMigrationGuards.DropIndexIfExists(
            migrationBuilder,
            "liens_SellingBuyerAccessLinks",
            "IX_SellingBuyerAccessLinks_WeeklyAging");
    }
}
