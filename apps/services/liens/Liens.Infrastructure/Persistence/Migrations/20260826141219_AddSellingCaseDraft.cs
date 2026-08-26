using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Liens.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSellingCaseDraft : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "liens_SellingCaseDrafts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    OrgId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CaseStatus = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AccidentTypeId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AccidentState = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DateOfLoss = table.Column<DateOnly>(type: "date", nullable: true),
                    HandlingLawFirmCompanyId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    CaseManagerContactPersonId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    CaseTrackingNotes = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CaseId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    FinalizedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UpdatedByUserId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_liens_SellingCaseDrafts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_liens_SellingCaseDrafts_liens_Cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "liens_Cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_liens_SellingCaseDrafts_liens_Companies_HandlingLawFirmCompa~",
                        column: x => x.HandlingLawFirmCompanyId,
                        principalTable: "liens_Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_liens_SellingCaseDrafts_liens_CompanyContactPersons_CaseMana~",
                        column: x => x.CaseManagerContactPersonId,
                        principalTable: "liens_CompanyContactPersons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_liens_SellingCaseDrafts_CaseManagerContactPersonId",
                table: "liens_SellingCaseDrafts",
                column: "CaseManagerContactPersonId");

            migrationBuilder.CreateIndex(
                name: "IX_liens_SellingCaseDrafts_HandlingLawFirmCompanyId",
                table: "liens_SellingCaseDrafts",
                column: "HandlingLawFirmCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_SellingCaseDrafts_Tenant_Org_CreatedAtUtc",
                table: "liens_SellingCaseDrafts",
                columns: new[] { "TenantId", "OrgId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SellingCaseDrafts_Tenant_Org_FinalizedAtUtc",
                table: "liens_SellingCaseDrafts",
                columns: new[] { "TenantId", "OrgId", "FinalizedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_SellingCaseDrafts_CaseId",
                table: "liens_SellingCaseDrafts",
                column: "CaseId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new InvalidOperationException(
                "This migration is forward-only because selling case drafts may contain plaintiff PII. " +
                "Use an audited data export and a forward corrective migration rather than dropping the draft table.");
        }
    }
}
