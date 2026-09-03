using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Liens.Infrastructure.Persistence.Migrations;

[DbContext(typeof(LiensDbContext))]
[Migration("20260902020000_ReserveCaseNumbers")]
public partial class ReserveCaseNumbers : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE TABLE IF NOT EXISTS `liens_CaseNumberReservations` (
                `TenantId` char(36) COLLATE ascii_general_ci NOT NULL,
                `CaseNumber` varchar(50) CHARACTER SET utf8mb4 NOT NULL,
                `ReservedAtUtc` datetime(6) NOT NULL,
                CONSTRAINT `PK_liens_CaseNumberReservations`
                    PRIMARY KEY (`TenantId`, `CaseNumber`)
            ) CHARACTER SET=utf8mb4
            """);

        migrationBuilder.Sql(
            """
            INSERT IGNORE INTO `liens_CaseNumberReservations`
                (`TenantId`, `CaseNumber`, `ReservedAtUtc`)
            SELECT `TenantId`, `CaseNumber`, `CreatedAtUtc`
            FROM `liens_Cases`
            WHERE `CaseNumber` <> ''
            """);

        // Deleted legacy cases may only be represented by their retained lien numbers.
        migrationBuilder.Sql(
            """
            INSERT IGNORE INTO `liens_CaseNumberReservations`
                (`TenantId`, `CaseNumber`, `ReservedAtUtc`)
            SELECT
                `TenantId`,
                SUBSTRING_INDEX(`LienNumber`, '-', 2),
                MIN(`CreatedAtUtc`)
            FROM `liens_Liens`
            WHERE `LienNumber` REGEXP '^[0-9]{2}-[0-9]{5,6}-[0-9]+$'
            GROUP BY `TenantId`, SUBSTRING_INDEX(`LienNumber`, '-', 2)
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "liens_CaseNumberReservations");
    }
}
