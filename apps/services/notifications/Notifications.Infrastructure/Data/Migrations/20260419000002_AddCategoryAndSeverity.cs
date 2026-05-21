using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Notifications.Infrastructure.Data;

#nullable disable

namespace Notifications.Infrastructure.Data.Migrations
{
    [DbContext(typeof(NotificationsDbContext))]
    [Migration("20260419000002_AddCategoryAndSeverity")]
    public partial class AddCategoryAndSeverity : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "ntf_Notifications",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "Severity",
                table: "ntf_Notifications",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Category",
                table: "ntf_Notifications");

            migrationBuilder.DropColumn(
                name: "Severity",
                table: "ntf_Notifications");
        }
    }
}
