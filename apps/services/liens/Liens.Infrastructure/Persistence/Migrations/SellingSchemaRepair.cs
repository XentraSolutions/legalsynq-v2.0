using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Logging;

namespace Liens.Infrastructure.Persistence.Migrations;

public static class SellingSchemaRepair
{
    private const string RepairLockName = "legalsynq-liens-selling-schema-repair";
    private const int ExpectedTableCount = 7;
    private const int ExpectedColumnCount = 10;
    private const int ExpectedIndexCount = 27;
    private const int ExpectedForeignKeyCount = 16;
    private const int ExpectedCompanyTypeSeedCount = 4;
    private const int ExpectedContactTypeSeedCount = 28;

    /// <summary>
    /// Repairs selling schema drift even when an environment incorrectly recorded
    /// the migrations before all MySQL DDL completed. Normal deployments return
    /// after two read-only information_schema checks.
    /// </summary>
    public static async Task<bool> EnsureAsync(
        LiensDbContext db,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(logger);

        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
        {
            await connection.OpenAsync(cancellationToken);
        }

        var repairLockAcquired = false;
        try
        {
            if (!await NeedsRepairAsync(connection, cancellationToken))
            {
                logger.LogDebug("Selling schema verification passed; no recovery DDL was required.");
                return false;
            }

            repairLockAcquired = await AcquireRepairLockAsync(connection, cancellationToken);
            if (!repairLockAcquired)
            {
                throw new TimeoutException(
                    "Timed out waiting for another Liens instance to finish selling schema recovery.");
            }

            // A different service instance may have completed the repair while this
            // instance waited for the advisory lock.
            if (!await NeedsRepairAsync(connection, cancellationToken))
            {
                logger.LogInformation("Selling schema recovery was completed by another service instance.");
                return false;
            }

            logger.LogWarning(
                "Selling schema is incomplete despite migration history; applying guarded recovery DDL.");

            Migration[] recoveryMigrations =
            [
                new AddSellingCompanyDirectory(),
                new AddSellingPartyCompatibility(),
            ];

            foreach (var migration in recoveryMigrations)
            {
                foreach (var operation in migration.UpOperations)
                {
                    if (operation is not SqlOperation sqlOperation)
                    {
                        throw new InvalidOperationException(
                            $"Selling recovery migration '{migration.GetType().Name}' contains an unguarded " +
                            $"operation '{operation.GetType().Name}'.");
                    }

                    await using var command = connection.CreateCommand();
                    command.CommandText = sqlOperation.Sql;
                    await command.ExecuteNonQueryAsync(cancellationToken);
                }
            }

            if (await NeedsRepairAsync(connection, cancellationToken))
            {
                throw new InvalidOperationException(
                    "Selling schema recovery completed without producing the expected schema.");
            }

            logger.LogInformation("Selling schema recovery completed successfully.");
            return true;
        }
        finally
        {
            if (repairLockAcquired)
            {
                try
                {
                    await ReleaseRepairLockAsync(connection, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Could not release the selling schema recovery advisory lock.");
                }
            }

            if (openedHere)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task<bool> AcquireRepairLockAsync(
        System.Data.Common.DbConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT GET_LOCK('{RepairLockName}', 30);";
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is not null && result is not DBNull && Convert.ToInt32(result) == 1;
    }

    private static async Task ReleaseRepairLockAsync(
        System.Data.Common.DbConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT RELEASE_LOCK('{RepairLockName}');";
        await command.ExecuteScalarAsync(cancellationToken);
    }

    private static async Task<bool> NeedsRepairAsync(
        System.Data.Common.DbConnection connection,
        CancellationToken cancellationToken)
    {
        await using (var tableCommand = connection.CreateCommand())
        {
            tableCommand.CommandText = """
                SELECT COUNT(*)
                FROM information_schema.TABLES
                WHERE TABLE_SCHEMA = DATABASE()
                  AND TABLE_NAME IN (
                      'liens_CompanyTypes',
                      'liens_Companies',
                      'liens_ContactPersonTypes',
                      'liens_CompanyContactPersons',
                      'liens_SellingPartyAliases',
                      'liens_SellingPartyBackfillCheckpoints',
                      'liens_SellingPartyBackfillQuarantines')
                """;

            var tableCount = Convert.ToInt32(await tableCommand.ExecuteScalarAsync(cancellationToken));
            if (tableCount != ExpectedTableCount)
            {
                return true;
            }
        }

        await using var schemaCommand = connection.CreateCommand();
        schemaCommand.CommandText = """
            SELECT
                (SELECT COUNT(*)
                 FROM information_schema.COLUMNS
                 WHERE TABLE_SCHEMA = DATABASE()
                   AND ((TABLE_NAME = 'liens_SellingPortfolioBuyers' AND COLUMN_NAME = 'BuyerCompanyId')
                     OR (TABLE_NAME = 'liens_SellingBuyerAccessLinks' AND COLUMN_NAME IN ('BuyerCompanyContactPersonId', 'BuyerCompanyId'))
                     OR (TABLE_NAME = 'liens_Liens' AND COLUMN_NAME IN ('FundingCompanyCompanyId', 'FundingCompanyContactPersonId', 'MedicalFacilityCompanyId', 'MedicalProviderCompanyId'))
                     OR (TABLE_NAME = 'liens_LienOffers' AND COLUMN_NAME = 'BuyerCompanyId')
                     OR (TABLE_NAME = 'liens_Cases' AND COLUMN_NAME IN ('CaseManagerContactPersonId', 'HandlingLawFirmCompanyId')))) AS ColumnCount,
                (SELECT COUNT(DISTINCT INDEX_NAME)
                 FROM information_schema.STATISTICS
                 WHERE TABLE_SCHEMA = DATABASE()
                   AND INDEX_NAME IN (
                       'IX_Companies_LinkedTenantId',
                       'IX_Companies_TenantId_OrgId_CompanyTypeId_IsActive',
                       'IX_liens_Companies_CompanyTypeId',
                       'UX_Companies_TenantId_OrgId_CompanyTypeId_NormalizedName',
                       'IX_CompanyContactPersons_CompanyId_ContactPersonTypeId',
                       'IX_CompanyContactPersons_TenantId_CompanyId_IsActive_Name',
                       'IX_liens_CompanyContactPersons_ContactPersonTypeId',
                       'UX_CompanyTypes_Code',
                       'IX_ContactPersonTypes_CompanyTypeId_IsActive_SortOrder',
                       'UX_ContactPersonTypes_CompanyTypeId_Code',
                       'IX_liens_SellingPortfolioBuyers_BuyerCompanyId',
                       'IX_liens_SellingBuyerAccessLinks_BuyerCompanyContactPersonId',
                       'IX_liens_SellingBuyerAccessLinks_BuyerCompanyId',
                       'IX_liens_Liens_FundingCompanyCompanyId',
                       'IX_liens_Liens_FundingCompanyContactPersonId',
                       'IX_liens_Liens_MedicalFacilityCompanyId',
                       'IX_liens_Liens_MedicalProviderCompanyId',
                       'IX_liens_LienOffers_BuyerCompanyId',
                       'IX_liens_Cases_CaseManagerContactPersonId',
                       'IX_liens_Cases_HandlingLawFirmCompanyId',
                       'IX_liens_SellingPartyAliases_CompanyContactPersonId',
                       'IX_liens_SellingPartyAliases_CompanyId',
                       'UX_SellingPartyAliases_ExternalScope',
                       'UX_SellingPartyAliases_PreferredCompany',
                       'UX_SellingPartyAliases_PreferredContact',
                       'UX_SellingPartyBackfillCheckpoints_Tenant_Workflow',
                       'UX_SellingPartyBackfillQuarantines_SourceReason')) AS IndexCount,
                (SELECT COUNT(*)
                 FROM information_schema.TABLE_CONSTRAINTS
                 WHERE CONSTRAINT_SCHEMA = DATABASE()
                   AND CONSTRAINT_TYPE = 'FOREIGN KEY'
                   AND CONSTRAINT_NAME IN (
                       'FK_liens_Companies_liens_CompanyTypes_CompanyTypeId',
                       'FK_liens_ContactPersonTypes_liens_CompanyTypes_CompanyTypeId',
                       'FK_liens_CompanyContactPersons_liens_Companies_CompanyId',
                       'FK_liens_CompanyContactPersons_liens_ContactPersonTypes_Contact~',
                       'FK_liens_Cases_liens_Companies_HandlingLawFirmCompanyId',
                       'FK_liens_Cases_liens_CompanyContactPersons_CaseManagerContactPe~',
                       'FK_liens_LienOffers_liens_Companies_BuyerCompanyId',
                       'FK_liens_Liens_liens_Companies_FundingCompanyCompanyId',
                       'FK_liens_Liens_liens_Companies_MedicalFacilityCompanyId',
                       'FK_liens_Liens_liens_Companies_MedicalProviderCompanyId',
                       'FK_liens_Liens_liens_CompanyContactPersons_FundingCompanyContac~',
                       'FK_liens_SellingBuyerAccessLinks_liens_Companies_BuyerCompanyId',
                       'FK_liens_SellingBuyerAccessLinks_liens_CompanyContactPersons_Bu~',
                       'FK_liens_SellingPortfolioBuyers_liens_Companies_BuyerCompanyId',
                       'FK_liens_SellingPartyAliases_liens_Companies_CompanyId',
                       'FK_liens_SellingPartyAliases_liens_CompanyContactPersons_Compan~')) AS ForeignKeyCount,
                (SELECT COUNT(*) FROM `liens_CompanyTypes`
                 WHERE `Id` IN (
                     '10000000-0000-0000-0000-000000000001',
                     '10000000-0000-0000-0000-000000000002',
                     '10000000-0000-0000-0000-000000000003',
                     '10000000-0000-0000-0000-000000000004')) AS CompanyTypeSeedCount,
                (SELECT COUNT(*) FROM `liens_ContactPersonTypes`
                 WHERE `Id` >= '20000000-0000-0000-0000-000000000001'
                   AND `Id` <= '20000000-0000-0000-0000-000000000028') AS ContactTypeSeedCount
            """;

        await using var reader = await schemaCommand.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return true;
        }

        return Convert.ToInt32(reader.GetValue(0)) != ExpectedColumnCount
            || Convert.ToInt32(reader.GetValue(1)) != ExpectedIndexCount
            || Convert.ToInt32(reader.GetValue(2)) != ExpectedForeignKeyCount
            || Convert.ToInt32(reader.GetValue(3)) != ExpectedCompanyTypeSeedCount
            || Convert.ToInt32(reader.GetValue(4)) != ExpectedContactTypeSeedCount;
    }
}
