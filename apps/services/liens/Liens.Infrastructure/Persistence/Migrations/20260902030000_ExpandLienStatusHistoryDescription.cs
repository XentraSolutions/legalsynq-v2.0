using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Liens.Infrastructure.Persistence.Migrations;

[DbContext(typeof(LiensDbContext))]
[Migration("20260902030000_ExpandLienStatusHistoryDescription")]
public partial class ExpandLienStatusHistoryDescription : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "Description",
            table: "liens_LienStatusHistory",
            type: "text",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "varchar(500)",
            oldMaxLength: 500);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        throw new NotSupportedException(
            "Lien history descriptions cannot be safely narrowed to varchar(500) after comprehensive history has been written.");
    }
}
