using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CareConnect.Infrastructure.Data.Migrations;

/// <summary>
/// Adds an optional professional title beside provider first/last name.
/// Name remains the computed display name used by existing search and referral surfaces.
/// </summary>
[DbContext(typeof(CareConnectDbContext))]
[Migration("20260803010000_AddProviderTitle")]
public partial class AddProviderTitle : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            SET @providerTitleColumn = IF(
                (SELECT COUNT(*)
                   FROM information_schema.columns
                  WHERE table_schema = DATABASE()
                    AND table_name = 'cc_Providers'
                    AND column_name = 'Title') = 0,
                'ALTER TABLE `cc_Providers` ADD COLUMN `Title` varchar(50) NULL',
                'SELECT 1');
            PREPARE stmt FROM @providerTitleColumn; EXECUTE stmt; DEALLOCATE PREPARE stmt;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            SET @providerTitleColumn = IF(
                (SELECT COUNT(*)
                   FROM information_schema.columns
                  WHERE table_schema = DATABASE()
                    AND table_name = 'cc_Providers'
                    AND column_name = 'Title') = 1,
                'ALTER TABLE `cc_Providers` DROP COLUMN `Title`',
                'SELECT 1');
            PREPARE stmt FROM @providerTitleColumn; EXECUTE stmt; DEALLOCATE PREPARE stmt;
            """);
    }
}
