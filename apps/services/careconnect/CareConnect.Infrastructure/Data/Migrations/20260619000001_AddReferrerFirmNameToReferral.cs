using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareConnect.Infrastructure.Data.Migrations
{
    [Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(CareConnectDbContext))]
    [Migration("20260619000001_AddReferrerFirmNameAndPhoneToReferral")]
    public partial class AddReferrerFirmNameAndPhoneToReferral : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReferrerFirmName",
                table: "cc_Referrals",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferrerPhone",
                table: "cc_Referrals",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReferrerPhone",
                table: "cc_Referrals");

            migrationBuilder.DropColumn(
                name: "ReferrerFirmName",
                table: "cc_Referrals");
        }
    }
}
