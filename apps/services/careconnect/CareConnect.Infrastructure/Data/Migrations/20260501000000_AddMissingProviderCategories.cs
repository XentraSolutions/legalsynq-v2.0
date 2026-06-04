using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareConnect.Infrastructure.Data.Migrations
{
    [DbContext(typeof(CareConnectDbContext))]
    [Migration("20260501000000_AddMissingProviderCategories")]
    public partial class AddMissingProviderCategories : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                INSERT IGNORE INTO `cc_Categories`
                    (`Id`, `Code`, `CreatedAtUtc`, `Description`, `IsActive`, `Name`)
                VALUES
                    ('40000000-0000-0000-0000-000000000006', 'EXTREM',  '2024-01-01 00:00:00', NULL, 1, 'Extremities'),
                    ('40000000-0000-0000-0000-000000000007', 'SPINE',   '2024-01-01 00:00:00', NULL, 1, 'Spine Surgeon'),
                    ('40000000-0000-0000-0000-000000000008', 'NEURO',   '2024-01-01 00:00:00', NULL, 1, 'Neurology'),
                    ('40000000-0000-0000-0000-000000000009', 'SURGERY', '2024-01-01 00:00:00', NULL, 1, 'Surgery Center');
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM `cc_Categories`
                WHERE `Id` IN (
                    '40000000-0000-0000-0000-000000000006',
                    '40000000-0000-0000-0000-000000000007',
                    '40000000-0000-0000-0000-000000000008',
                    '40000000-0000-0000-0000-000000000009'
                );
                """);
        }
    }
}
