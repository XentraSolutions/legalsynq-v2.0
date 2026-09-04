using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Liens.Infrastructure.Persistence.Migrations;

[DbContext(typeof(LiensDbContext))]
[Migration("20260904010000_AddContactPhoneExtension")]
public partial class AddContactPhoneExtension : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
                name: "PhoneExtension",
                table: "liens_Contacts",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true)
            .Annotation("MySql:CharSet", "utf8mb4");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "PhoneExtension",
            table: "liens_Contacts");
    }
}
