using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Liens.Infrastructure.Persistence.Migrations
{
    [DbContext(typeof(LiensDbContext))]
    [Migration("20260626000002_AddMedicalFacilityContactType")]
    public partial class AddMedicalFacilityContactType : Migration
    {
        private const string SystemUserId = "00000000-0000-0000-0000-000000000001";

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"""
                INSERT INTO `liens_LookupValues`
                    (`Id`, `TenantId`, `Category`, `Code`, `Name`, `Description`,
                     `SortOrder`, `IsActive`, `IsSystem`,
                     `CreatedByUserId`, `UpdatedByUserId`, `CreatedAtUtc`, `UpdatedAtUtc`)
                SELECT UUID(), NULL, 'ContactType', s.Code, s.Name, s.Description, s.Sort, 1, 1,
                       '{SystemUserId}', NULL, NOW(), NOW()
                FROM (
                    SELECT 'MedicalFacility' AS Code, 'Medical Facility' AS Name, 'Medical facility contact' AS Description, 8 AS Sort
                ) AS s
                WHERE NOT EXISTS (
                    SELECT 1 FROM `liens_LookupValues`
                    WHERE `Category` = 'ContactType' AND `Code` = s.Code AND `TenantId` IS NULL
                );
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM `liens_LookupValues`
                WHERE `TenantId` IS NULL
                  AND `Category` = 'ContactType'
                  AND `Code` = 'MedicalFacility';
                """);
        }
    }
}
