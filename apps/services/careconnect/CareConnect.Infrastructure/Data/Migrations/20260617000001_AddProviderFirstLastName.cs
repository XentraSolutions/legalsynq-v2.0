using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareConnect.Infrastructure.Data.Migrations;

/// <summary>
/// Adds FirstName and LastName to the Providers table so the tenant-portal
/// "add provider" form can capture the provider contact's name as separate
/// fields instead of a single free-text Name.
///
/// Name remains the single computed display string read by every existing
/// consumer (search, email templates, admin dashboard) — these new columns
/// are additive only and do not change its value or nullability.
/// </summary>
public partial class AddProviderFirstLastName : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name:  "FirstName",
            table: "cc_Providers",
            type:  "longtext",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name:  "LastName",
            table: "cc_Providers",
            type:  "longtext",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "FirstName", table: "cc_Providers");
        migrationBuilder.DropColumn(name: "LastName",  table: "cc_Providers");
    }
}
