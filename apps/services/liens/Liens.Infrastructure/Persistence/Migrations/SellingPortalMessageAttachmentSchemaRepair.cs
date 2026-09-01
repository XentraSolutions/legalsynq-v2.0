using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Logging;

namespace Liens.Infrastructure.Persistence.Migrations;

public static class SellingPortalMessageAttachmentSchemaRepair
{
    private const string RepairLockName = "legalsynq-liens-selling-portal-message-attachment-schema-repair";
    private const string TableName = "liens_SellingPortalMessageAttachments";
    private const int ExpectedTableCount = 1;
    private const int ExpectedColumnCount = 16;
    private const int ExpectedIndexCount = 7;
    private const int ExpectedForeignKeyCount = 3;

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
            await connection.OpenAsync(cancellationToken);

        var repairLockAcquired = false;
        try
        {
            if (!await NeedsRepairAsync(connection, cancellationToken))
            {
                logger.LogDebug("Selling portal message-attachment schema verification passed; no recovery DDL was required.");
                return false;
            }

            repairLockAcquired = await AcquireRepairLockAsync(connection, cancellationToken);
            if (!repairLockAcquired)
            {
                throw new TimeoutException(
                    "Timed out waiting for another Liens instance to finish selling portal message-attachment schema recovery.");
            }

            if (!await NeedsRepairAsync(connection, cancellationToken))
            {
                logger.LogInformation("Selling portal message-attachment schema recovery was completed by another service instance.");
                return false;
            }

            logger.LogWarning(
                "Selling portal message-attachment schema is incomplete; applying guarded recovery DDL.");

            var migration = CreateRecoveryMigration();
            foreach (var operation in migration.UpOperations)
            {
                if (operation is not SqlOperation sqlOperation)
                {
                    throw new InvalidOperationException(
                        $"Selling portal message-attachment recovery migration contains an unguarded " +
                        $"operation '{operation.GetType().Name}'.");
                }

                await using var command = connection.CreateCommand();
                command.CommandText = sqlOperation.Sql;
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            if (await NeedsRepairAsync(connection, cancellationToken))
            {
                throw new InvalidOperationException(
                    "Selling portal message-attachment schema recovery completed without producing the expected schema.");
            }

            logger.LogInformation("Selling portal message-attachment schema recovery completed successfully.");
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
                    logger.LogWarning(ex, "Could not release the selling portal message-attachment schema recovery advisory lock.");
                }
            }

            if (openedHere)
                await connection.CloseAsync();
        }
    }

    internal static Migration CreateRecoveryMigration() => new AddSellingPortalMessageAttachments();

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
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT
                (SELECT COUNT(*)
                 FROM information_schema.TABLES
                 WHERE TABLE_SCHEMA = DATABASE()
                   AND TABLE_NAME = '{TableName}') AS TableCount,
                (SELECT COUNT(*)
                 FROM information_schema.COLUMNS
                 WHERE TABLE_SCHEMA = DATABASE()
                   AND TABLE_NAME = '{TableName}'
                   AND COLUMN_NAME IN (
                       'Id',
                       'TenantId',
                       'LienId',
                       'SellerOrgId',
                       'BuyerOrgId',
                       'BuyerContactId',
                       'AccessLinkId',
                       'MessageId',
                       'DocumentId',
                       'FileName',
                       'ContentType',
                       'FileSizeBytes',
                       'CreatedAtUtc',
                       'UpdatedAtUtc',
                       'CreatedByUserId',
                       'UpdatedByUserId')) AS ColumnCount,
                (SELECT COUNT(DISTINCT INDEX_NAME)
                 FROM information_schema.STATISTICS
                 WHERE TABLE_SCHEMA = DATABASE()
                   AND TABLE_NAME = '{TableName}'
                   AND INDEX_NAME IN (
                       'PRIMARY',
                       'IX_liens_SellingPortalMessageAttachments_AccessLinkId',
                       'IX_liens_SellingPortalMessageAttachments_LienId',
                       'IX_liens_SellingPortalMessageAttachments_MessageId',
                       'IX_SellingPortalMessageAttachments_Tenant_Document',
                       'IX_SellingPortalMessageAttachments_Tenant_Lien_Participants',
                       'IX_SellingPortalMessageAttachments_Tenant_Message_Created')) AS IndexCount,
                (SELECT COUNT(*)
                 FROM information_schema.TABLE_CONSTRAINTS
                 WHERE CONSTRAINT_SCHEMA = DATABASE()
                   AND TABLE_NAME = '{TableName}'
                   AND CONSTRAINT_TYPE = 'FOREIGN KEY'
                   AND CONSTRAINT_NAME IN (
                       'FK_liens_SellingPortalMessageAttachments_liens_Liens_LienId',
                       'FK_liens_SellingPortalMessageAttachments_liens_SellingBuyerAcce~',
                       'FK_liens_SellingPortalMessageAttachments_liens_SellingPortalMes~')) AS ForeignKeyCount
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return true;

        return Convert.ToInt32(reader.GetValue(0)) != ExpectedTableCount
            || Convert.ToInt32(reader.GetValue(1)) != ExpectedColumnCount
            || Convert.ToInt32(reader.GetValue(2)) != ExpectedIndexCount
            || Convert.ToInt32(reader.GetValue(3)) != ExpectedForeignKeyCount;
    }
}
