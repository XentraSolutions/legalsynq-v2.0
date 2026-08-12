using Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations;

[DbContext(typeof(IdentityDbContext))]
[Migration("20260626000001_SeedSynqLienCasePermissions")]
public partial class SeedSynqLienCasePermissions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            INSERT IGNORE INTO `idt_Capabilities`
                (`Id`, `ProductId`, `Code`, `Name`, `Description`, `Category`, `IsActive`, `CreatedAtUtc`, `UpdatedAtUtc`, `CreatedBy`, `UpdatedBy`)
            VALUES
                ('60000000-0000-0000-0000-000000000063', '10000000-0000-0000-0000-000000000002',
                 'SYNQ_LIENS.case:read',   'Read Cases',   'View case records',
                 'Case', 1, '2024-01-01 00:00:00.000000', NULL, NULL, NULL),
                ('60000000-0000-0000-0000-000000000064', '10000000-0000-0000-0000-000000000002',
                 'SYNQ_LIENS.case:create', 'Create Case',  'Create a new case record',
                 'Case', 1, '2024-01-01 00:00:00.000000', NULL, NULL, NULL),
                ('60000000-0000-0000-0000-000000000065', '10000000-0000-0000-0000-000000000002',
                 'SYNQ_LIENS.case:update', 'Update Case',  'Update an existing case record',
                 'Case', 1, '2024-01-01 00:00:00.000000', NULL, NULL, NULL);
            """);

        migrationBuilder.Sql("""
            INSERT IGNORE INTO `idt_RoleCapabilities` (`ProductRoleId`, `CapabilityId`)
            VALUES
                -- SYNQLIEN_SELLER: full case access for law-firm case workflows.
                ('50000000-0000-0000-0000-000000000003', '60000000-0000-0000-0000-000000000063'),
                ('50000000-0000-0000-0000-000000000003', '60000000-0000-0000-0000-000000000064'),
                ('50000000-0000-0000-0000-000000000003', '60000000-0000-0000-0000-000000000065'),
                -- SYNQLIEN_BUYER: read cases tied to purchasable or held lien workflows.
                ('50000000-0000-0000-0000-000000000004', '60000000-0000-0000-0000-000000000063'),
                -- SYNQLIEN_HOLDER: full case access for servicing workflows.
                ('50000000-0000-0000-0000-000000000005', '60000000-0000-0000-0000-000000000063'),
                ('50000000-0000-0000-0000-000000000005', '60000000-0000-0000-0000-000000000064'),
                ('50000000-0000-0000-0000-000000000005', '60000000-0000-0000-0000-000000000065');
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE FROM `idt_RoleCapabilities`
            WHERE `CapabilityId` IN (
                '60000000-0000-0000-0000-000000000063',
                '60000000-0000-0000-0000-000000000064',
                '60000000-0000-0000-0000-000000000065'
            );
            """);

        migrationBuilder.Sql("""
            DELETE FROM `idt_Capabilities`
            WHERE `Id` IN (
                '60000000-0000-0000-0000-000000000063',
                '60000000-0000-0000-0000-000000000064',
                '60000000-0000-0000-0000-000000000065'
            );
            """);
    }
}
