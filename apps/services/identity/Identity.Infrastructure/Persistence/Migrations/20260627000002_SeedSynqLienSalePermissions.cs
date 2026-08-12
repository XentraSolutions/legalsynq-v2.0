using Identity.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations;

[DbContext(typeof(IdentityDbContext))]
[Migration("20260627000002_SeedSynqLienSalePermissions")]
public partial class SeedSynqLienSalePermissions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            INSERT IGNORE INTO `idt_Capabilities`
                (`Id`, `ProductId`, `Code`, `Name`, `Description`, `Category`, `IsActive`, `CreatedAtUtc`, `UpdatedAtUtc`, `CreatedBy`, `UpdatedBy`)
            VALUES
                ('60000000-0000-0000-0000-000000000066', '10000000-0000-0000-0000-000000000002',
                 'SYNQ_LIENS.lien_sale:read', 'Read Lien Sale Portfolios', 'View lien sale portfolios and portfolio detail',
                 'Lien Sale', 1, '2024-01-01 00:00:00.000000', NULL, NULL, NULL),
                ('60000000-0000-0000-0000-000000000067', '10000000-0000-0000-0000-000000000002',
                 'SYNQ_LIENS.lien_sale:create', 'Create Lien Sale Portfolios', 'Create lien sale portfolios',
                 'Lien Sale', 1, '2024-01-01 00:00:00.000000', NULL, NULL, NULL),
                ('60000000-0000-0000-0000-000000000068', '10000000-0000-0000-0000-000000000002',
                 'SYNQ_LIENS.lien_sale:update', 'Update Lien Sale Portfolios', 'Update lien sale portfolios and included liens',
                 'Lien Sale', 1, '2024-01-01 00:00:00.000000', NULL, NULL, NULL),
                ('60000000-0000-0000-0000-000000000069', '10000000-0000-0000-0000-000000000002',
                 'SYNQ_LIENS.lien_sale:publish', 'Publish Lien Sale Portfolios', 'Publish lien sale portfolios for review',
                 'Lien Sale', 1, '2024-01-01 00:00:00.000000', NULL, NULL, NULL),
                ('60000000-0000-0000-0000-000000000070', '10000000-0000-0000-0000-000000000002',
                 'SYNQ_LIENS.lien_sale:withdraw', 'Withdraw Lien Sale Portfolios', 'Withdraw lien sale portfolios',
                 'Lien Sale', 1, '2024-01-01 00:00:00.000000', NULL, NULL, NULL),
                ('60000000-0000-0000-0000-000000000071', '10000000-0000-0000-0000-000000000002',
                 'SYNQ_LIENS.lien_sale:view_analytics', 'View Lien Sale Analytics', 'View lien sale portfolio analytics',
                 'Lien Sale', 1, '2024-01-01 00:00:00.000000', NULL, NULL, NULL);
            """);

        migrationBuilder.Sql("""
            INSERT IGNORE INTO `idt_RoleCapabilities` (`ProductRoleId`, `CapabilityId`)
            VALUES
                -- SYNQLIEN_SELLER: owns Phase 1 sale portfolio operations.
                ('50000000-0000-0000-0000-000000000003', '60000000-0000-0000-0000-000000000066'),
                ('50000000-0000-0000-0000-000000000003', '60000000-0000-0000-0000-000000000067'),
                ('50000000-0000-0000-0000-000000000003', '60000000-0000-0000-0000-000000000068'),
                ('50000000-0000-0000-0000-000000000003', '60000000-0000-0000-0000-000000000069'),
                ('50000000-0000-0000-0000-000000000003', '60000000-0000-0000-0000-000000000070'),
                ('50000000-0000-0000-0000-000000000003', '60000000-0000-0000-0000-000000000071');
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE FROM `idt_RoleCapabilities`
            WHERE `CapabilityId` IN (
                '60000000-0000-0000-0000-000000000066',
                '60000000-0000-0000-0000-000000000067',
                '60000000-0000-0000-0000-000000000068',
                '60000000-0000-0000-0000-000000000069',
                '60000000-0000-0000-0000-000000000070',
                '60000000-0000-0000-0000-000000000071'
            );
            """);

        migrationBuilder.Sql("""
            DELETE FROM `idt_Capabilities`
            WHERE `Id` IN (
                '60000000-0000-0000-0000-000000000066',
                '60000000-0000-0000-0000-000000000067',
                '60000000-0000-0000-0000-000000000068',
                '60000000-0000-0000-0000-000000000069',
                '60000000-0000-0000-0000-000000000070',
                '60000000-0000-0000-0000-000000000071'
            );
            """);
    }
}
