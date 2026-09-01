using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Liens.Infrastructure.Persistence.Migrations;

[DbContext(typeof(LiensDbContext))]
[Migration("20260831010000_OptimizeCaseNoteReportQueries")]
public partial class OptimizeCaseNoteReportQueries : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        SellingSchemaMigrationGuards.ExecuteSql(
            migrationBuilder,
            """
            UPDATE `liens_CaseNotes`
            SET `Category` = CASE LOWER(TRIM(`Category`))
                WHEN 'general' THEN 'general'
                WHEN 'feed' THEN 'feed'
                WHEN 'internal' THEN 'internal'
                WHEN 'follow-up' THEN 'follow-up'
                WHEN 'case created' THEN 'Case Created'
                WHEN 'settlement history' THEN 'Settlement History'
                ELSE `Category`
            END
            WHERE LOWER(TRIM(`Category`)) IN (
                'general',
                'feed',
                'internal',
                'follow-up',
                'case created',
                'settlement history')
              AND BINARY `Category` <> BINARY CASE LOWER(TRIM(`Category`))
                WHEN 'general' THEN 'general'
                WHEN 'feed' THEN 'feed'
                WHEN 'internal' THEN 'internal'
                WHEN 'follow-up' THEN 'follow-up'
                WHEN 'case created' THEN 'Case Created'
                WHEN 'settlement history' THEN 'Settlement History'
                ELSE `Category`
              END
            """);

        SellingSchemaMigrationGuards.CreateIndexIfMissing(
            migrationBuilder,
            "liens_CaseNotes",
            "IX_CaseNotes_ReportLookup",
            "(`TenantId`, `CaseId`, `IsDeleted`, `Category`, `CreatedAtUtc` DESC, `Id` DESC)");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        SellingSchemaMigrationGuards.DropIndexIfExists(
            migrationBuilder,
            "liens_CaseNotes",
            "IX_CaseNotes_ReportLookup");
    }
}
