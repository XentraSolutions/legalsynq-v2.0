using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareConnect.Infrastructure.Data.Migrations;

/// <summary>
/// Adds ReferrerFirstName and ReferrerLastName to the Referrals table so the public
/// referral form can capture the law firm contact's name as separate fields, mirroring
/// ClientFirstName/ClientLastName for the patient.
///
/// ReferrerName remains the single computed display string read by every existing
/// consumer (emails, admin dashboard, search, thread default sender) — these new
/// columns are additive only and do not change its value or nullability.
/// </summary>
public partial class AddReferrerFirstLastNameToReferral : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name:  "ReferrerFirstName",
            table: "cc_Referrals",
            type:  "longtext",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name:  "ReferrerLastName",
            table: "cc_Referrals",
            type:  "longtext",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "ReferrerFirstName", table: "cc_Referrals");
        migrationBuilder.DropColumn(name: "ReferrerLastName",  table: "cc_Referrals");
    }
}
