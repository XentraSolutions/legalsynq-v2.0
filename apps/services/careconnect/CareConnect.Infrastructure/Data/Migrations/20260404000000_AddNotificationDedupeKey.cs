using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareConnect.Infrastructure.Data.Migrations;

public partial class AddNotificationDedupeKey : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name:      "DedupeKey",
            table:     "CareConnectNotifications",
            type:      "varchar(500)",
            maxLength: 500,
            nullable:  true);

        migrationBuilder.CreateIndex(
            name:    "IX_CareConnectNotifications_DedupeKey",
            table:   "CareConnectNotifications",
            column:  "DedupeKey",
            unique:  true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name:  "IX_CareConnectNotifications_DedupeKey",
            table: "CareConnectNotifications");

        migrationBuilder.DropColumn(name: "DedupeKey", table: "CareConnectNotifications");
    }
}
