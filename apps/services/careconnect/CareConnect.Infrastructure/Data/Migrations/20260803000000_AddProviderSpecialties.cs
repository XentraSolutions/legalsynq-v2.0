using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareConnect.Infrastructure.Data.Migrations;

[DbContext(typeof(CareConnectDbContext))]
[Migration("20260803000000_AddProviderSpecialties")]
public partial class AddProviderSpecialties : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS `cc_Specialties` (
                `Id`           char(36)      NOT NULL,
                `Name`         varchar(200)  NOT NULL,
                `Code`         varchar(50)   NOT NULL,
                `Description`  varchar(1000) NULL,
                `IsActive`     tinyint(1)    NOT NULL,
                `CreatedAtUtc` datetime(6)   NOT NULL,
                `UpdatedAtUtc` datetime(6)   NOT NULL,
                CONSTRAINT `PK_cc_Specialties` PRIMARY KEY (`Id`),
                UNIQUE KEY `IX_cc_Specialties_Code` (`Code`)
            ) CHARACTER SET=utf8mb4;
            """);

        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS `cc_ProviderSpecialties` (
                `ProviderId`  char(36)   NOT NULL,
                `SpecialtyId` char(36)   NOT NULL,
                `IsPrimary`   tinyint(1) NOT NULL,
                CONSTRAINT `PK_cc_ProviderSpecialties` PRIMARY KEY (`ProviderId`, `SpecialtyId`),
                KEY `IX_cc_ProviderSpecialties_SpecialtyId` (`SpecialtyId`),
                KEY `IX_cc_ProviderSpecialties_ProviderId_IsPrimary` (`ProviderId`, `IsPrimary`)
            ) CHARACTER SET=utf8mb4;
            """);

        migrationBuilder.Sql("""
            INSERT INTO `cc_Specialties`
                (`Id`, `Name`, `Code`, `Description`, `IsActive`, `CreatedAtUtc`, `UpdatedAtUtc`)
            VALUES
                ('41000000-0000-0000-0000-000000000001', 'Pain',             'PAIN',             NULL, 1, '2024-01-01 00:00:00', '2024-01-01 00:00:00'),
                ('41000000-0000-0000-0000-000000000007', 'Spine',            'SPINE',            NULL, 1, '2024-01-01 00:00:00', '2024-01-01 00:00:00'),
                ('41000000-0000-0000-0000-000000000004', 'Physical Therapy', 'PHYSICAL_THERAPY', NULL, 1, '2024-01-01 00:00:00', '2024-01-01 00:00:00'),
                ('41000000-0000-0000-0000-000000000006', 'Neuro',            'NEURO',            NULL, 1, '2024-01-01 00:00:00', '2024-01-01 00:00:00'),
                ('41000000-0000-0000-0000-000000000005', 'Imaging',          'IMAGING',          NULL, 1, '2024-01-01 00:00:00', '2024-01-01 00:00:00'),
                ('41000000-0000-0000-0000-000000000002', 'Chiropractor',     'CHIROPRACTOR',     NULL, 1, '2024-01-01 00:00:00', '2024-01-01 00:00:00'),
                ('41000000-0000-0000-0000-000000000009', 'Extremities',      'EXTREMITIES',      NULL, 1, '2024-01-01 00:00:00', '2024-01-01 00:00:00')
            ON DUPLICATE KEY UPDATE
                `Name` = VALUES(`Name`),
                `Code` = VALUES(`Code`),
                `Description` = VALUES(`Description`),
                `IsActive` = VALUES(`IsActive`),
                `UpdatedAtUtc` = VALUES(`UpdatedAtUtc`);
            """);

        migrationBuilder.Sql("""
            UPDATE `cc_Specialties`
            SET `IsActive` = 0,
                `UpdatedAtUtc` = '2024-01-01 00:00:00'
            WHERE `Id` IN (
                '41000000-0000-0000-0000-000000000003',
                '41000000-0000-0000-0000-000000000008'
            )
            OR `Code` IN ('ORTHOPEDICS', 'SURGERY_CENTER');
            """);

        migrationBuilder.Sql("""
            INSERT IGNORE INTO `cc_ProviderSpecialties`
                (`ProviderId`, `SpecialtyId`, `IsPrimary`)
            SELECT
                pc.`ProviderId`,
                CASE c.`Code`
                    WHEN 'PAIN' THEN '41000000-0000-0000-0000-000000000001'
                    WHEN 'PT' THEN '41000000-0000-0000-0000-000000000004'
                    WHEN 'IMG' THEN '41000000-0000-0000-0000-000000000005'
                    WHEN 'NEURO' THEN '41000000-0000-0000-0000-000000000006'
                    WHEN 'SPINE' THEN '41000000-0000-0000-0000-000000000007'
                    WHEN 'CHIRO' THEN '41000000-0000-0000-0000-000000000002'
                END AS `SpecialtyId`,
                0
            FROM `cc_ProviderCategories` pc
            INNER JOIN `cc_Categories` c ON c.`Id` = pc.`CategoryId`
            WHERE c.`Code` IN ('PAIN', 'CHIRO', 'PT', 'IMG', 'NEURO', 'SPINE');
            """);

        migrationBuilder.Sql("""
            UPDATE `cc_ProviderSpecialties` ps
            INNER JOIN (
                SELECT `ProviderId`, MIN(`SpecialtyId`) AS `PrimarySpecialtyId`
                FROM `cc_ProviderSpecialties`
                GROUP BY `ProviderId`
            ) selected ON selected.`ProviderId` = ps.`ProviderId`
            SET ps.`IsPrimary` = CASE WHEN ps.`SpecialtyId` = selected.`PrimarySpecialtyId` THEN 1 ELSE 0 END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TABLE IF EXISTS `cc_ProviderSpecialties`;");
        migrationBuilder.Sql("DROP TABLE IF EXISTS `cc_Specialties`;");
    }
}
