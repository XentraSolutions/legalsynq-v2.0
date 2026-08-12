using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareConnect.Infrastructure.Data.Migrations;

/// <summary>
/// Moves provider network membership to provider-location rows backed by Facility.
/// Provider remains the identity record; Facility becomes the searchable location.
/// </summary>
[DbContext(typeof(CareConnectDbContext))]
[Migration("20260804010000_AddProviderNetworkLocations")]
public partial class AddProviderNetworkLocations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        AddColumnIfMissing(migrationBuilder, "cc_Facilities", "Email", "varchar(320) NULL");
        AddColumnIfMissing(migrationBuilder, "cc_Facilities", "Latitude", "decimal(10,7) NULL");
        AddColumnIfMissing(migrationBuilder, "cc_Facilities", "Longitude", "decimal(10,7) NULL");
        AddColumnIfMissing(migrationBuilder, "cc_Facilities", "GeoPointSource", "varchar(20) NULL");
        AddColumnIfMissing(migrationBuilder, "cc_Facilities", "GeoUpdatedAtUtc", "datetime(6) NULL");

        AddColumnIfMissing(migrationBuilder, "cc_NetworkProviders", "FacilityId", "char(36) NULL");
        AddColumnIfMissing(migrationBuilder, "cc_NetworkProviders", "IsActive", "tinyint(1) NOT NULL DEFAULT 1");
        AddColumnIfMissing(migrationBuilder, "cc_NetworkProviders", "AcceptingReferrals", "tinyint(1) NOT NULL DEFAULT 1");

        AddColumnIfMissing(migrationBuilder, "cc_Referrals", "FacilityId", "char(36) NULL");

        migrationBuilder.Sql("""
            INSERT INTO `cc_Facilities`
                (`Id`, `TenantId`, `Name`, `AddressLine1`, `City`, `State`, `PostalCode`, `Email`, `Phone`, `IsActive`,
                 `Latitude`, `Longitude`, `GeoPointSource`, `GeoUpdatedAtUtc`, `CreatedAtUtc`, `UpdatedAtUtc`, `CreatedByUserId`, `UpdatedByUserId`)
            SELECT
                UUID(),
                p.`TenantId`,
                COALESCE(NULLIF(TRIM(p.`OrganizationName`), ''), NULLIF(TRIM(p.`Name`), ''), 'Provider location'),
                COALESCE(NULLIF(TRIM(p.`AddressLine1`), ''), 'Unknown address'),
                COALESCE(NULLIF(TRIM(p.`City`), ''), 'Unknown'),
                COALESCE(NULLIF(TRIM(p.`State`), ''), 'NA'),
                COALESCE(NULLIF(TRIM(p.`PostalCode`), ''), '00000'),
                p.`Email`,
                p.`Phone`,
                p.`IsActive`,
                p.`Latitude`,
                p.`Longitude`,
                p.`GeoPointSource`,
                p.`GeoUpdatedAtUtc`,
                COALESCE(p.`CreatedAtUtc`, UTC_TIMESTAMP(6)),
                COALESCE(p.`UpdatedAtUtc`, UTC_TIMESTAMP(6)),
                p.`CreatedByUserId`,
                p.`UpdatedByUserId`
            FROM `cc_Providers` p
            WHERE NOT EXISTS (
                  SELECT 1
                  FROM `cc_Facilities` f
                  WHERE f.`TenantId` = p.`TenantId`
                    AND UPPER(TRIM(f.`Name`)) = UPPER(TRIM(COALESCE(NULLIF(TRIM(p.`OrganizationName`), ''), NULLIF(TRIM(p.`Name`), ''), 'Provider location')))
                    AND UPPER(TRIM(f.`AddressLine1`)) = UPPER(TRIM(COALESCE(NULLIF(TRIM(p.`AddressLine1`), ''), 'Unknown address')))
                    AND UPPER(TRIM(f.`City`)) = UPPER(TRIM(COALESCE(NULLIF(TRIM(p.`City`), ''), 'Unknown')))
                    AND UPPER(TRIM(f.`State`)) = UPPER(TRIM(COALESCE(NULLIF(TRIM(p.`State`), ''), 'NA')))
                    AND UPPER(TRIM(f.`PostalCode`)) = UPPER(TRIM(COALESCE(NULLIF(TRIM(p.`PostalCode`), ''), '00000')))
              );
            """);

        migrationBuilder.Sql("""
            INSERT IGNORE INTO `cc_ProviderFacilities`
                (`ProviderId`, `FacilityId`, `IsPrimary`)
            SELECT
                p.`Id`,
                MIN(f.`Id`) AS `FacilityId`,
                1 AS `IsPrimary`
            FROM `cc_Providers` p
            INNER JOIN `cc_Facilities` f
                ON f.`TenantId` = p.`TenantId`
               AND UPPER(TRIM(f.`Name`)) = UPPER(TRIM(COALESCE(NULLIF(TRIM(p.`OrganizationName`), ''), NULLIF(TRIM(p.`Name`), ''), 'Provider location')))
               AND UPPER(TRIM(f.`AddressLine1`)) = UPPER(TRIM(COALESCE(NULLIF(TRIM(p.`AddressLine1`), ''), 'Unknown address')))
               AND UPPER(TRIM(f.`City`)) = UPPER(TRIM(COALESCE(NULLIF(TRIM(p.`City`), ''), 'Unknown')))
               AND UPPER(TRIM(f.`State`)) = UPPER(TRIM(COALESCE(NULLIF(TRIM(p.`State`), ''), 'NA')))
               AND UPPER(TRIM(f.`PostalCode`)) = UPPER(TRIM(COALESCE(NULLIF(TRIM(p.`PostalCode`), ''), '00000')))
            GROUP BY p.`Id`;
            """);

        migrationBuilder.Sql("""
            UPDATE `cc_ProviderFacilities` pf
            INNER JOIN (
                SELECT `ProviderId`, MIN(`FacilityId`) AS `PrimaryFacilityId`
                FROM `cc_ProviderFacilities`
                GROUP BY `ProviderId`
            ) selected ON selected.`ProviderId` = pf.`ProviderId`
            SET pf.`IsPrimary` = CASE WHEN pf.`FacilityId` = selected.`PrimaryFacilityId` THEN 1 ELSE 0 END;
            """);

        migrationBuilder.Sql("""
            UPDATE `cc_NetworkProviders` np
            INNER JOIN `cc_Providers` p ON p.`Id` = np.`ProviderId`
            INNER JOIN (
                SELECT `ProviderId`, MIN(`FacilityId`) AS `FacilityId`
                FROM `cc_ProviderFacilities`
                GROUP BY `ProviderId`
            ) pf ON pf.`ProviderId` = np.`ProviderId`
            SET
                np.`FacilityId` = pf.`FacilityId`,
                np.`IsActive` = p.`IsActive`,
                np.`AcceptingReferrals` = p.`AcceptingReferrals`
            WHERE np.`FacilityId` IS NULL;
            """);

        migrationBuilder.Sql("""
            SET @networkProviderFacilityNotNull = IF(
                (SELECT COUNT(*) FROM `cc_NetworkProviders` WHERE `FacilityId` IS NULL) = 0,
                'ALTER TABLE `cc_NetworkProviders` MODIFY COLUMN `FacilityId` char(36) NOT NULL',
                'SELECT 1');
            PREPARE stmt FROM @networkProviderFacilityNotNull; EXECUTE stmt; DEALLOCATE PREPARE stmt;
            """);

        CreateIndexIfMissing(migrationBuilder, "cc_NetworkProviders", "IX_NetworkProviders_ProviderNetworkId_ProviderId_Tmp", "`ProviderNetworkId`, `ProviderId`");
        DropIndexIfExists(migrationBuilder, "cc_NetworkProviders", "IX_cc_NetworkProviders_ProviderNetworkId_ProviderId");
        DropIndexIfExists(migrationBuilder, "cc_NetworkProviders", "IX_NetworkProviders_ProviderNetworkId_ProviderId");
        CreateIndexIfMissing(migrationBuilder, "cc_NetworkProviders", "IX_NetworkProviders_ProviderNetworkId_ProviderId", "`ProviderNetworkId`, `ProviderId`");
        DropIndexIfExists(migrationBuilder, "cc_NetworkProviders", "IX_NetworkProviders_ProviderNetworkId_ProviderId_Tmp");
        CreateIndexIfMissing(migrationBuilder, "cc_NetworkProviders", "IX_NetworkProviders_ProviderNetworkId_ProviderId_FacilityId", "`ProviderNetworkId`, `ProviderId`, `FacilityId`", unique: true);
        CreateIndexIfMissing(migrationBuilder, "cc_NetworkProviders", "IX_NetworkProviders_FacilityId", "`FacilityId`");
        CreateIndexIfMissing(migrationBuilder, "cc_Facilities", "IX_Facilities_TenantId_Latitude_Longitude", "`TenantId`, `Latitude`, `Longitude`");
        CreateIndexIfMissing(migrationBuilder, "cc_Facilities", "IX_Facilities_Tenant_Address", "`TenantId`, `AddressLine1`, `City`, `State`, `PostalCode`");
        CreateIndexIfMissing(migrationBuilder, "cc_Referrals", "IX_Referrals_TenantId_FacilityId", "`TenantId`, `FacilityId`");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        DropIndexIfExists(migrationBuilder, "cc_Referrals", "IX_Referrals_TenantId_FacilityId");
        DropIndexIfExists(migrationBuilder, "cc_Facilities", "IX_Facilities_Tenant_Address");
        DropIndexIfExists(migrationBuilder, "cc_Facilities", "IX_Facilities_TenantId_Latitude_Longitude");
        DropIndexIfExists(migrationBuilder, "cc_NetworkProviders", "IX_NetworkProviders_FacilityId");
        DropIndexIfExists(migrationBuilder, "cc_NetworkProviders", "IX_NetworkProviders_ProviderNetworkId_ProviderId_FacilityId");
        DropIndexIfExists(migrationBuilder, "cc_NetworkProviders", "IX_NetworkProviders_ProviderNetworkId_ProviderId");
        CreateIndexIfMissing(migrationBuilder, "cc_NetworkProviders", "IX_cc_NetworkProviders_ProviderNetworkId_ProviderId", "`ProviderNetworkId`, `ProviderId`", unique: true);

        DropColumnIfExists(migrationBuilder, "cc_Referrals", "FacilityId");
        DropColumnIfExists(migrationBuilder, "cc_NetworkProviders", "AcceptingReferrals");
        DropColumnIfExists(migrationBuilder, "cc_NetworkProviders", "IsActive");
        DropColumnIfExists(migrationBuilder, "cc_NetworkProviders", "FacilityId");
        DropColumnIfExists(migrationBuilder, "cc_Facilities", "GeoUpdatedAtUtc");
        DropColumnIfExists(migrationBuilder, "cc_Facilities", "GeoPointSource");
        DropColumnIfExists(migrationBuilder, "cc_Facilities", "Longitude");
        DropColumnIfExists(migrationBuilder, "cc_Facilities", "Latitude");
        DropColumnIfExists(migrationBuilder, "cc_Facilities", "Email");
    }

    private static void AddColumnIfMissing(MigrationBuilder migrationBuilder, string table, string column, string definition)
    {
        migrationBuilder.Sql($$"""
            SET @addColumn = IF(
                (SELECT COUNT(*)
                   FROM information_schema.columns
                  WHERE table_schema = DATABASE()
                    AND table_name = '{{table}}'
                    AND column_name = '{{column}}') = 0,
                'ALTER TABLE `{{table}}` ADD COLUMN `{{column}}` {{definition}}',
                'SELECT 1');
            PREPARE stmt FROM @addColumn; EXECUTE stmt; DEALLOCATE PREPARE stmt;
            """);
    }

    private static void DropColumnIfExists(MigrationBuilder migrationBuilder, string table, string column)
    {
        migrationBuilder.Sql($$"""
            SET @dropColumn = IF(
                (SELECT COUNT(*)
                   FROM information_schema.columns
                  WHERE table_schema = DATABASE()
                    AND table_name = '{{table}}'
                    AND column_name = '{{column}}') = 1,
                'ALTER TABLE `{{table}}` DROP COLUMN `{{column}}`',
                'SELECT 1');
            PREPARE stmt FROM @dropColumn; EXECUTE stmt; DEALLOCATE PREPARE stmt;
            """);
    }

    private static void CreateIndexIfMissing(MigrationBuilder migrationBuilder, string table, string index, string columns, bool unique = false)
    {
        var uniqueness = unique ? "UNIQUE " : string.Empty;
        migrationBuilder.Sql($$"""
            SET @createIndex = IF(
                (SELECT COUNT(*)
                   FROM information_schema.statistics
                  WHERE table_schema = DATABASE()
                    AND table_name = '{{table}}'
                    AND index_name = '{{index}}') = 0,
                'CREATE {{uniqueness}}INDEX `{{index}}` ON `{{table}}` ({{columns}})',
                'SELECT 1');
            PREPARE stmt FROM @createIndex; EXECUTE stmt; DEALLOCATE PREPARE stmt;
            """);
    }

    private static void DropIndexIfExists(MigrationBuilder migrationBuilder, string table, string index)
    {
        migrationBuilder.Sql($$"""
            SET @dropIndex = IF(
                (SELECT COUNT(*)
                   FROM information_schema.statistics
                  WHERE table_schema = DATABASE()
                    AND table_name = '{{table}}'
                    AND index_name = '{{index}}') > 0,
                'DROP INDEX `{{index}}` ON `{{table}}`',
                'SELECT 1');
            PREPARE stmt FROM @dropIndex; EXECUTE stmt; DEALLOCATE PREPARE stmt;
            """);
    }
}
