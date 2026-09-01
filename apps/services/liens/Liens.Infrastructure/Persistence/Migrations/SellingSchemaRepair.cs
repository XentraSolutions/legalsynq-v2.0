using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Logging;

namespace Liens.Infrastructure.Persistence.Migrations;

public static class SellingSchemaRepair
{
    private const string RepairLockName = "legalsynq-liens-selling-schema-repair";
    private const int ExpectedTableCount = 9;
    private const int ExpectedColumnCount = 37;
    private const int ExpectedIndexCount = 43;
    private const int ExpectedForeignKeyCount = 21;
    private const int ExpectedCheckConstraintCount = 1;
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

            foreach (var migration in CreateRecoveryMigrations())
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

    internal static Migration[] CreateRecoveryMigrations() =>
    [
        new AddSellingCompanyDirectory(),
        new AddSellingPartyCompatibility(),
        new AddScopedContactPersonTypes(),
        new AddReceivableDueDate(),
        new AddCasePaymentLedgerFields(),
        new AddWeeklyAgingReportIndex(),
        new AddLegacyReportParityFields(),
        new AddLienImportedCreatedByName(),
        new AddLienSellingCaseReference(),
        new AddSellingCaseDraft(),
        new AddSellingCaseDraftConcurrencyToken(),
    ];

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
                      'liens_SellingPartyBackfillQuarantines',
                      'liens_LegacyFieldMigrationStates',
                      'liens_SellingCaseDrafts')
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
                     OR (TABLE_NAME = 'liens_Liens' AND COLUMN_NAME IN ('FundingCompanyCompanyId', 'FundingCompanyContactPersonId', 'MedicalFacilityCompanyId', 'MedicalProviderCompanyId', 'ReceivableDueDate', 'SellingCaseId', 'MovedToManagementAtUtc'))
                     OR (TABLE_NAME = 'liens_LienOffers' AND COLUMN_NAME = 'BuyerCompanyId')
                     OR (TABLE_NAME = 'liens_Cases' AND COLUMN_NAME IN ('CaseManagerContactPersonId', 'HandlingLawFirmCompanyId', 'AttorneyContactPersonId', 'CaseDropped', 'ClientAddressLine1', 'ClientCity', 'ClientPostalCode', 'ClientState', 'CurrentMedicalStatus', 'ImportedCreatedByName', 'IncidentState', 'MinorComp', 'TrackingFollowUpDate'))
                     OR (TABLE_NAME = 'liens_ContactPersonTypes' AND COLUMN_NAME IN ('TenantId', 'OrgId'))
                     OR (TABLE_NAME = 'liens_SettlementPaymentDetails' AND COLUMN_NAME IN ('ReceiptId', 'PaymentMethod', 'SettlementType', 'SettlementStatus', 'DetailsContext', 'PostingStatus', 'VoidedAtUtc', 'VoidedByUserId', 'VoidReason'))
                     OR (TABLE_NAME = 'liens_Liens' AND COLUMN_NAME = 'ImportedCreatedByName')
                     OR (TABLE_NAME = 'liens_SellingCaseDrafts' AND COLUMN_NAME = 'ConcurrencyToken'))) AS ColumnCount,
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
                       'UX_ContactPersonTypes_Scope_CompanyTypeId_Code',
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
                       'UX_SellingPartyBackfillQuarantines_SourceReason',
                       'IX_SettlementPayments_Tenant_Date_Deleted',
                       'IX_SettlementPayments_Tenant_Lien_Deleted',
                       'IX_SettlementPayments_Tenant_Case_Status_Date',
                       'IX_SettlementPayments_Tenant_Receipt',
                       'IX_SellingBuyerAccessLinks_WeeklyAging',
                       'IX_Cases_AttorneyContactPersonId',
                       'IX_LegacyFieldMigrationStates_ImportRunId',
                       'UX_LegacyFieldMigrationStates_Source_FieldGroup',
                       'IX_Liens_Tenant_Seller_FundingCompanyCompanyId',
                       'IX_Liens_Tenant_Seller_ReceivableDueDate',
                       'IX_Liens_SellingCaseId',
                       'IX_liens_SellingCaseDrafts_CaseManagerContactPersonId',
                       'IX_liens_SellingCaseDrafts_HandlingLawFirmCompanyId',
                       'IX_SellingCaseDrafts_Tenant_Org_CreatedAtUtc',
                       'IX_SellingCaseDrafts_Tenant_Org_FinalizedAtUtc',
                       'UX_SellingCaseDrafts_CaseId')) AS IndexCount,
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
                       'FK_liens_SellingPartyAliases_liens_CompanyContactPersons_Compan~',
                       'FK_Cases_AttorneyContactPerson',
                       'FK_liens_Liens_liens_Cases_SellingCaseId',
                       'FK_liens_SellingCaseDrafts_liens_Cases_CaseId',
                       'FK_liens_SellingCaseDrafts_liens_Companies_HandlingLawFirmCompa~',
                       'FK_liens_SellingCaseDrafts_liens_CompanyContactPersons_CaseMana~')) AS ForeignKeyCount,
                (SELECT COUNT(*)
                 FROM information_schema.TABLE_CONSTRAINTS
                 WHERE CONSTRAINT_SCHEMA = DATABASE()
                   AND TABLE_NAME = 'liens_ContactPersonTypes'
                   AND CONSTRAINT_NAME = 'CK_ContactPersonTypes_Scope'
                   AND CONSTRAINT_TYPE = 'CHECK') AS CheckConstraintCount,
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
            || Convert.ToInt32(reader.GetValue(3)) != ExpectedCheckConstraintCount
            || Convert.ToInt32(reader.GetValue(4)) != ExpectedCompanyTypeSeedCount
            || Convert.ToInt32(reader.GetValue(5)) != ExpectedContactTypeSeedCount;
    }
}
