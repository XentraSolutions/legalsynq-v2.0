using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareConnect.Infrastructure.Data.Migrations
{
    [DbContext(typeof(CareConnectDbContext))]
    [Migration("20260805153000_AddReferralAttributions")]
    public partial class AddReferralAttributions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ReferralAttributionId",
                table: "cc_Referrals",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateTable(
                name: "cc_ReferralAttributions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    FirstName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LastName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Code = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    UpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cc_ReferralAttributions", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "cc_ReferralAttributionAccessCodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ReferralAttributionId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CodeHash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    AccessStartAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    AccessEndAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    UpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cc_ReferralAttributionAccessCodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cc_ReferralAttributionAccessCodes_cc_ReferralAttributions_Ref~",
                        column: x => x.ReferralAttributionId,
                        principalTable: "cc_ReferralAttributions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Referrals_ReferralAttributionId",
                table: "cc_Referrals",
                column: "ReferralAttributionId");

            migrationBuilder.CreateIndex(
                name: "IX_Referrals_TenantId_ReferralAttributionId",
                table: "cc_Referrals",
                columns: new[] { "TenantId", "ReferralAttributionId" });

            migrationBuilder.CreateIndex(
                name: "IX_Referrals_TenantId_ReferralAttributionId_CreatedAtUtc",
                table: "cc_Referrals",
                columns: new[] { "TenantId", "ReferralAttributionId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Referrals_TenantId_ReferralAttributionId_Status",
                table: "cc_Referrals",
                columns: new[] { "TenantId", "ReferralAttributionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ReferralAttributions_TenantId",
                table: "cc_ReferralAttributions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ReferralAttributions_TenantId_IsActive",
                table: "cc_ReferralAttributions",
                columns: new[] { "TenantId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "UX_ReferralAttributions_TenantId_Code",
                table: "cc_ReferralAttributions",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ReferralAttributionAccessCodes_CodeHash",
                table: "cc_ReferralAttributionAccessCodes",
                column: "CodeHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReferralAttributionAccessCodes_TenantId",
                table: "cc_ReferralAttributionAccessCodes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ReferralAttributionAccessCodes_ReferralAttributionId",
                table: "cc_ReferralAttributionAccessCodes",
                column: "ReferralAttributionId");

            migrationBuilder.AddForeignKey(
                name: "FK_cc_Referrals_cc_ReferralAttributions_ReferralAttributionId",
                table: "cc_Referrals",
                column: "ReferralAttributionId",
                principalTable: "cc_ReferralAttributions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_cc_Referrals_cc_ReferralAttributions_ReferralAttributionId",
                table: "cc_Referrals");

            migrationBuilder.DropTable(
                name: "cc_ReferralAttributionAccessCodes");

            migrationBuilder.DropTable(
                name: "cc_ReferralAttributions");

            migrationBuilder.DropIndex(
                name: "IX_Referrals_ReferralAttributionId",
                table: "cc_Referrals");

            migrationBuilder.DropIndex(
                name: "IX_Referrals_TenantId_ReferralAttributionId",
                table: "cc_Referrals");

            migrationBuilder.DropIndex(
                name: "IX_Referrals_TenantId_ReferralAttributionId_CreatedAtUtc",
                table: "cc_Referrals");

            migrationBuilder.DropIndex(
                name: "IX_Referrals_TenantId_ReferralAttributionId_Status",
                table: "cc_Referrals");

            migrationBuilder.DropColumn(
                name: "ReferralAttributionId",
                table: "cc_Referrals");
        }
    }
}
