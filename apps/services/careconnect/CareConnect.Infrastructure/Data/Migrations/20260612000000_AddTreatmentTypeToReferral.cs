using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareConnect.Infrastructure.Data.Migrations
{
    [Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(CareConnectDbContext))]
    [Migration("20260612000000_AddTreatmentTypeToReferral")]
    public partial class AddTreatmentTypeToReferral : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TreatmentTypeId",
                table: "cc_Referrals",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateIndex(
                name: "IX_Referrals_TreatmentTypeId",
                table: "cc_Referrals",
                column: "TreatmentTypeId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Referrals_TreatmentTypeId",
                table: "cc_Referrals");

            migrationBuilder.DropColumn(
                name: "TreatmentTypeId",
                table: "cc_Referrals");
        }
    }
}
