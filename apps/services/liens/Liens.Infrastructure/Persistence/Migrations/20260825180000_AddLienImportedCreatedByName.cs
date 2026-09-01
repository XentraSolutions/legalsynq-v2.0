using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Liens.Infrastructure.Persistence.Migrations;

[DbContext(typeof(LiensDbContext))]
[Migration("20260825180000_AddLienImportedCreatedByName")]
public partial class AddLienImportedCreatedByName : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => SellingSchemaMigrationGuards.AddColumnIfMissing(
            migrationBuilder,
            "liens_Liens",
            "ImportedCreatedByName",
            "varchar(100) CHARACTER SET utf8mb4 NULL");

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropColumn(
            name: "ImportedCreatedByName",
            table: "liens_Liens");
}
