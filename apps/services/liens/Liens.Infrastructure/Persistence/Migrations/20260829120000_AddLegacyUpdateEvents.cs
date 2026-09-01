using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Liens.Infrastructure.Persistence.Migrations;

[DbContext(typeof(LiensDbContext))]
[Migration("20260829120000_AddLegacyUpdateEvents")]
public partial class AddLegacyUpdateEvents : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        LegacyUpdateEventSchemaMigrationGuards.CreateTableIfMissing(
            migrationBuilder,
            """
            CREATE TABLE IF NOT EXISTS `liens_LegacyUpdateEvents` (
                `Id` char(36) COLLATE ascii_general_ci NOT NULL,
                `TenantId` char(36) COLLATE ascii_general_ci NOT NULL,
                `OrgId` char(36) COLLATE ascii_general_ci NOT NULL,
                `CaseId` char(36) COLLATE ascii_general_ci NOT NULL,
                `LienId` char(36) COLLATE ascii_general_ci NULL,
                `Scope` varchar(20) CHARACTER SET utf8mb4 NOT NULL,
                `Action` varchar(255) CHARACTER SET utf8mb4 NOT NULL,
                `Description` text CHARACTER SET utf8mb4 NULL,
                `ActorDisplayName` varchar(255) CHARACTER SET utf8mb4 NULL,
                `OccurredAtUtc` datetime(6) NOT NULL,
                `ImportedAtUtc` datetime(6) NOT NULL,
                `ImportRunId` char(36) COLLATE ascii_general_ci NOT NULL,
                `SourceSystem` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
                `SourceTable` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
                `LegacyId` varchar(100) CHARACTER SET utf8mb4 NOT NULL,
                `LegacySequence` bigint NOT NULL,
                CONSTRAINT `PK_liens_LegacyUpdateEvents` PRIMARY KEY (`Id`),
                CONSTRAINT `CK_LegacyUpdateEvents_Scope` CHECK (`Scope` IN ('Case', 'Lien')),
                CONSTRAINT `CK_LegacyUpdateEvents_ScopeLien` CHECK (
                    (`Scope` = 'Case' AND `LienId` IS NULL)
                    OR (`Scope` = 'Lien' AND `LienId` IS NOT NULL)),
                CONSTRAINT `FK_liens_LegacyUpdateEvents_liens_LegacyImportRuns_ImportRunId`
                    FOREIGN KEY (`ImportRunId`) REFERENCES `liens_LegacyImportRuns` (`Id`) ON DELETE RESTRICT
            ) CHARACTER SET=utf8mb4
            """);

        LegacyUpdateEventSchemaMigrationGuards.CreateIndexIfMissing(
            migrationBuilder,
            "IX_LegacyUpdateEvents_CaseTimeline",
            "(`TenantId`, `CaseId`, `Scope`, `OccurredAtUtc` DESC, `LegacySequence` DESC)");
        LegacyUpdateEventSchemaMigrationGuards.CreateIndexIfMissing(
            migrationBuilder,
            "IX_LegacyUpdateEvents_ImportRunId",
            "(`ImportRunId`)");
        LegacyUpdateEventSchemaMigrationGuards.CreateIndexIfMissing(
            migrationBuilder,
            "IX_LegacyUpdateEvents_LienTimeline",
            "(`TenantId`, `LienId`, `OccurredAtUtc` DESC, `LegacySequence` DESC)");
        LegacyUpdateEventSchemaMigrationGuards.CreateIndexIfMissing(
            migrationBuilder,
            "UX_LegacyUpdateEvents_Tenant_Source_Table_Key",
            "(`TenantId`, `SourceSystem`, `SourceTable`, `LegacyId`)",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        throw new InvalidOperationException(
            "Legacy update history is intentionally irreversible. Disable imported-history reads and use the guarded run-bound compensation procedure before exposure; repair forward after exposure.");
    }
}
