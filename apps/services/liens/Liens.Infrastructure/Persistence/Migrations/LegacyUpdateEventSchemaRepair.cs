using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Logging;

namespace Liens.Infrastructure.Persistence.Migrations;

public static class LegacyUpdateEventSchemaRepair
{
    public const string MigrationId = "20260829120000_AddLegacyUpdateEvents";
    private const string RepairLockName = "legalsynq-liens-legacy-update-event-schema-repair";

    public static async Task<bool> EnsureAsync(
        LiensDbContext db,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(logger);

        var pendingMigrations = await db.Database.GetPendingMigrationsAsync(cancellationToken);
        var migrationWasPending = pendingMigrations.Contains(MigrationId, StringComparer.Ordinal);
        var connection = db.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
            await connection.OpenAsync(cancellationToken);

        var repairLockAcquired = false;
        try
        {
            repairLockAcquired = await AcquireRepairLockAsync(connection, cancellationToken);
            if (!repairLockAcquired)
            {
                throw new TimeoutException(
                    "Timed out waiting for another Liens instance to finish legacy update-event schema recovery.");
            }

            var migration = CreateRecoveryMigration();
            foreach (var operation in migration.UpOperations)
            {
                if (operation is not SqlOperation sqlOperation)
                {
                    throw new InvalidOperationException(
                        $"Legacy update-event recovery migration contains an unguarded operation '{operation.GetType().Name}'.");
                }

                await using var command = connection.CreateCommand();
                command.CommandText = sqlOperation.Sql;
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            logger.LogInformation(
                "Legacy update-event schema verification completed successfully; migration pending before repair: {MigrationWasPending}.",
                migrationWasPending);
            return migrationWasPending;
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
                    logger.LogWarning(ex, "Could not release the legacy update-event schema recovery advisory lock.");
                }
            }

            if (openedHere)
                await connection.CloseAsync();
        }
    }

    internal static Migration CreateRecoveryMigration() => new AddLegacyUpdateEvents();

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
}
