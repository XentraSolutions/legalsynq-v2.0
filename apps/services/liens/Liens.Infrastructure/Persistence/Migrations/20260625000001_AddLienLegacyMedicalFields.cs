using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Liens.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(LiensDbContext))]
    [Migration("20260625000001_AddLienLegacyMedicalFields")]
    public partial class AddLienLegacyMedicalFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "InitialServiceDate",
                table: "liens_Liens",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "EndServiceDate",
                table: "liens_Liens",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IsBulk",
                table: "liens_Liens",
                type: "varchar(10)",
                maxLength: 10,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "IsServicing",
                table: "liens_Liens",
                type: "varchar(10)",
                maxLength: 10,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InitialServiceDate",
                table: "liens_Liens");

            migrationBuilder.DropColumn(
                name: "EndServiceDate",
                table: "liens_Liens");

            migrationBuilder.DropColumn(
                name: "IsBulk",
                table: "liens_Liens");

            migrationBuilder.DropColumn(
                name: "IsServicing",
                table: "liens_Liens");
        }
    }
}
