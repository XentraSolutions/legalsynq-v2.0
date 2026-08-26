using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Liens.Infrastructure.Persistence.Migrations;

[DbContext(typeof(LiensDbContext))]
[Migration("20260825170000_BackfillMedicalStatusLookupValues")]
public partial class BackfillMedicalStatusLookupValues : Migration
{
    private const string SystemUserId = "00000000-0000-0000-0000-000000000001";

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        SeedGlobalMedicalStatus(migrationBuilder, "TREATING", "Plaintiff Treating", 1);
        SeedGlobalMedicalStatus(migrationBuilder, "DONE_TREATING", "Plaintiff Done Treating", 2);
        SeedGlobalMedicalStatus(migrationBuilder, "UNKNOWN", "Unknown", 3);

        var backfillSql = $"""
            INSERT INTO `liens_LookupValues`
                (`Id`, `TenantId`, `Category`, `Code`, `Name`, `Description`,
                 `SortOrder`, `IsActive`, `IsSystem`,
                 `CreatedByUserId`, `UpdatedByUserId`, `CreatedAtUtc`, `UpdatedAtUtc`)
            SELECT UUID(), c.`TenantId`, 'MedicalStatus', c.`CurrentMedicalStatus`, c.`CurrentMedicalStatus`, NULL,
                   100, 1, 0, '{SystemUserId}', NULL, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
            FROM (
                SELECT DISTINCT `TenantId`, TRIM(`CurrentMedicalStatus`) AS `CurrentMedicalStatus`
                FROM `liens_Cases`
                WHERE `CurrentMedicalStatus` IS NOT NULL
                  AND TRIM(`CurrentMedicalStatus`) <> ''
            ) AS c
            WHERE NOT EXISTS (
                SELECT 1
                FROM `liens_LookupValues` lv
                WHERE lv.`TenantId` = c.`TenantId`
                  AND lv.`Category` = 'MedicalStatus'
                  AND lv.`Code` = c.`CurrentMedicalStatus`
            );
            """.Replace("'", "''");

        migrationBuilder.Sql($"""
            SET @legalsynq_medical_status_backfill = IF(
                (SELECT COUNT(*)
                 FROM information_schema.COLUMNS
                 WHERE TABLE_SCHEMA = DATABASE()
                   AND TABLE_NAME = 'liens_Cases'
                   AND COLUMN_NAME = 'CurrentMedicalStatus') = 1,
                '{backfillSql}',
                'SELECT 1');
            PREPARE legalsynq_medical_status_backfill_stmt FROM @legalsynq_medical_status_backfill;
            EXECUTE legalsynq_medical_status_backfill_stmt;
            DEALLOCATE PREPARE legalsynq_medical_status_backfill_stmt;
            """, suppressTransaction: true);
    }

    private static void SeedGlobalMedicalStatus(
        MigrationBuilder migrationBuilder,
        string code,
        string name,
        int sortOrder)
    {
        migrationBuilder.Sql($"""
            INSERT INTO `liens_LookupValues`
                (`Id`, `TenantId`, `Category`, `Code`, `Name`, `Description`,
                 `SortOrder`, `IsActive`, `IsSystem`,
                 `CreatedByUserId`, `UpdatedByUserId`, `CreatedAtUtc`, `UpdatedAtUtc`)
            SELECT UUID(), NULL, 'MedicalStatus', '{code}', '{name}', NULL,
                   {sortOrder}, 1, 1, '{SystemUserId}', NULL, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6)
            WHERE NOT EXISTS (
                SELECT 1
                FROM `liens_LookupValues`
                WHERE `TenantId` IS NULL
                  AND `Category` = 'MedicalStatus'
                  AND `Code` = '{code}'
            );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Existing case values may rely on the backfilled lookup entries, so the
        // data migration intentionally has no destructive rollback.
    }
}
