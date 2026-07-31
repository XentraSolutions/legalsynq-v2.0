using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Liens.Infrastructure.Persistence.Migrations;

[DbContext(typeof(LiensDbContext))]
[Migration("20260726000001_AddLegacyImportControlPlane")]
public partial class AddLegacyImportControlPlane : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "liens_LegacyImportRuns",
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
                Status = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
                StartedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                CompletedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                SummaryJson = table.Column<string>(type: "longtext", nullable: true).Annotation("MySql:CharSet", "utf8mb4"),
                ErrorSummary = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true).Annotation("MySql:CharSet", "utf8mb4")
            },
            constraints: table => table.PrimaryKey("PK_liens_LegacyImportRuns", x => x.Id))
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateTable(
            name: "liens_LegacyIdCrosswalks",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                SourceSystem = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
                SourceTable = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
                LegacyId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
                TargetEntity = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
                TargetId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                SourceHash = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
                ImportRunId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_liens_LegacyIdCrosswalks", x => x.Id);
                table.ForeignKey(
                    name: "FK_liens_LegacyIdCrosswalks_liens_LegacyImportRuns_ImportRunId",
                    column: x => x.ImportRunId,
                    principalTable: "liens_LegacyImportRuns",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateTable(
            name: "liens_LegacyImportExceptions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                TenantId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                ImportRunId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                SourceTable = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
                LegacyId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
                Severity = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
                ErrorCode = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
                Message = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: false).Annotation("MySql:CharSet", "utf8mb4"),
                SourceHash = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true).Annotation("MySql:CharSet", "utf8mb4"),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_liens_LegacyImportExceptions", x => x.Id);
                table.ForeignKey(
                    name: "FK_liens_LegacyImportExceptions_liens_LegacyImportRuns_ImportRunId",
                    column: x => x.ImportRunId,
                    principalTable: "liens_LegacyImportRuns",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateIndex(name: "IX_LegacyImportRuns_Tenant_Source_Program_Started", table: "liens_LegacyImportRuns", columns: new[] { "TenantId", "SourceSystem", "LegacyProgram", "StartedAtUtc" });
        migrationBuilder.CreateIndex(name: "IX_LegacyIdCrosswalks_ImportRunId", table: "liens_LegacyIdCrosswalks", column: "ImportRunId");
        migrationBuilder.CreateIndex(name: "UX_LegacyIdCrosswalk_Tenant_Source_Table_Key", table: "liens_LegacyIdCrosswalks", columns: new[] { "TenantId", "SourceSystem", "SourceTable", "LegacyId" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_LegacyImportExceptions_Tenant_Run_Severity", table: "liens_LegacyImportExceptions", columns: new[] { "TenantId", "ImportRunId", "Severity" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        throw new InvalidOperationException(
            "The legacy-import control plane is intentionally irreversible. Use an audited compensation or database restore procedure; do not remove import provenance while imported business rows may remain.");
    }
}
