using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Liens.Infrastructure.Persistence.Migrations;

[DbContext(typeof(LiensDbContext))]
[Migration("20260824000001_BackfillAcceptedLienPurchaseDates")]
public partial class BackfillAcceptedLienPurchaseDates : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE `liens_Liens` AS lien
            INNER JOIN (
                SELECT
                    `TenantId`,
                    `LienId`,
                    DATE(MIN(`RespondedAtUtc`)) AS `PurchaseDate`
                FROM `liens_SellingBuyerAccessLinks`
                WHERE `ResponseStatus` = 'Accepted'
                  AND `RespondedAtUtc` IS NOT NULL
                GROUP BY `TenantId`, `LienId`
            ) AS accepted
                ON accepted.`TenantId` = lien.`TenantId`
               AND accepted.`LienId` = lien.`Id`
            SET lien.`PurchaseDate` = accepted.`PurchaseDate`
            WHERE lien.`PurchaseDate` IS NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Existing purchase dates cannot be distinguished from backfilled values.
    }
}
