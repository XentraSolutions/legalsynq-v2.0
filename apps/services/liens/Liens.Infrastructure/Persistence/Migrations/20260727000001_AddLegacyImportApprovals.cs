using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Liens.Infrastructure.Persistence.Migrations;

[DbContext(typeof(LiensDbContext))]
[Migration("20260727000001_AddLegacyImportApprovals")]
public partial class AddLegacyImportApprovals : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "liens_LegacyImportApprovals",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                OrgId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                SourceSystem = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
                SourceFingerprint = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
                LegacyProgram = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
                MappingVersion = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
                MappingManifestHash = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
                MappingApprovalReference = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
                LienAmountSource = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
                LegacyStatusOneTarget = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
                LegacyStatusTwoTarget = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
                MigrationUserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                ApprovedByUserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                Status = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
                ApprovedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                ExpiresAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                ConsumedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                ConsumedByRunId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci")
            },
            constraints: table => table.PrimaryKey("PK_liens_LegacyImportApprovals", x => x.Id))
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.AddColumn<Guid>(
            name: "ApprovalId",
            table: "liens_LegacyImportRuns",
            type: "char(36)",
            nullable: true,
            collation: "ascii_general_ci");

        migrationBuilder.CreateIndex(
            name: "IX_LegacyImportApprovals_ConsumedByRunId",
            table: "liens_LegacyImportApprovals",
            column: "ConsumedByRunId");

        migrationBuilder.CreateIndex(
            name: "IX_LegacyImportApprovals_Tenant_Source_Program_Fingerprint_Status",
            table: "liens_LegacyImportApprovals",
            columns: new[] { "TenantId", "SourceSystem", "LegacyProgram", "SourceFingerprint", "Status" });

        migrationBuilder.CreateIndex(
            name: "IX_LegacyImportRuns_ApprovalId",
            table: "liens_LegacyImportRuns",
            column: "ApprovalId");

        migrationBuilder.AddForeignKey(
            name: "FK_liens_LegacyImportRuns_liens_LegacyImportApprovals_ApprovalId",
            table: "liens_LegacyImportRuns",
            column: "ApprovalId",
            principalTable: "liens_LegacyImportApprovals",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        throw new InvalidOperationException(
            "Legacy import approvals are release evidence. Use an audited compensation or database restore procedure instead of deleting approval provenance.");
    }
}
