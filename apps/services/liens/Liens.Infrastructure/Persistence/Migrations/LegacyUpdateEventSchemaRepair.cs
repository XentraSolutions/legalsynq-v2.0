using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Logging;

namespace Liens.Infrastructure.Persistence.Migrations;

public static class LegacyUpdateEventSchemaRepair
{
    public const string MigrationId = "20260829120000_AddLegacyUpdateEvents";
    private const string ProductVersion = "8.0.2";
    private const string TableName = "liens_LegacyUpdateEvents";
    private const string RepairLockName = "legalsynq-liens-legacy-update-event-schema-repair";

    private static readonly IReadOnlyDictionary<string, ColumnContract> RequiredColumns =
        new Dictionary<string, ColumnContract>(StringComparer.Ordinal)
        {
            ["Id"] = new("char(36)", false, "ascii_general_ci"),
            ["TenantId"] = new("char(36)", false, "ascii_general_ci"),
            ["OrgId"] = new("char(36)", false, "ascii_general_ci"),
            ["CaseId"] = new("char(36)", false, "ascii_general_ci"),
            ["LienId"] = new("char(36)", true, "ascii_general_ci"),
            ["Scope"] = new("varchar(20)", false, "utf8mb4_"),
            ["Action"] = new("varchar(255)", false, "utf8mb4_"),
            ["Description"] = new("text", true, "utf8mb4_"),
            ["ActorDisplayName"] = new("varchar(255)", true, "utf8mb4_"),
            ["OccurredAtUtc"] = new("datetime(6)", false, null),
            ["ImportedAtUtc"] = new("datetime(6)", false, null),
            ["ImportRunId"] = new("char(36)", false, "ascii_general_ci"),
            ["SourceSystem"] = new("varchar(100)", false, "utf8mb4_"),
            ["SourceTable"] = new("varchar(100)", false, "utf8mb4_"),
            ["LegacyId"] = new("varchar(100)", false, "utf8mb4_"),
            ["LegacySequence"] = new("bigint", false, null),
        };

    private static readonly IReadOnlyDictionary<string, IndexContract> RequiredIndexes =
        new Dictionary<string, IndexContract>(StringComparer.Ordinal)
        {
            ["PRIMARY"] = new(true, ["Id:A:FULL:YES"]),
            ["IX_LegacyUpdateEvents_CaseTimeline"] = new(
                false,
                [
                    "TenantId:A:FULL:YES",
                    "CaseId:A:FULL:YES",
                    "Scope:A:FULL:YES",
                    "OccurredAtUtc:D:FULL:YES",
                    "LegacySequence:D:FULL:YES",
                ]),
            ["IX_LegacyUpdateEvents_ImportRunId"] = new(false, ["ImportRunId:A:FULL:YES"]),
            ["IX_LegacyUpdateEvents_LienTimeline"] = new(
                false,
                [
                    "TenantId:A:FULL:YES",
                    "LienId:A:FULL:YES",
                    "OccurredAtUtc:D:FULL:YES",
                    "LegacySequence:D:FULL:YES",
                ]),
            ["UX_LegacyUpdateEvents_Tenant_Source_Table_Key"] = new(
                true,
                [
                    "TenantId:A:FULL:YES",
                    "SourceSystem:A:FULL:YES",
                    "SourceTable:A:FULL:YES",
                    "LegacyId:A:FULL:YES",
                ]),
        };

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

            await ValidateSchemaContractAsync(connection, cancellationToken);
            var historyReconciled = await ReconcileMigrationHistoryAsync(db, connection, cancellationToken);
            logger.LogInformation(
                "Legacy update-event schema verification completed successfully; migration pending before repair: {MigrationWasPending}; history reconciled: {HistoryReconciled}.",
                migrationWasPending,
                historyReconciled);
            return migrationWasPending || historyReconciled;
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

    private static async Task ValidateSchemaContractAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        var actualColumns = new Dictionary<string, ColumnContract>(StringComparer.Ordinal);
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
SELECT COLUMN_NAME, COLUMN_TYPE, IS_NULLABLE, COLLATION_NAME
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'liens_LegacyUpdateEvents';
""";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                actualColumns[reader.GetString(0)] = new ColumnContract(
                    reader.GetString(1).ToLowerInvariant(),
                    string.Equals(reader.GetString(2), "YES", StringComparison.Ordinal),
                    reader.IsDBNull(3) ? null : reader.GetString(3).ToLowerInvariant());
            }
        }

        if (RequiredColumns.Any(required =>
            !actualColumns.TryGetValue(required.Key, out var actual)
            || !ColumnMatches(required.Value, actual)))
        {
            throw new InvalidOperationException(
                "Legacy update-event schema recovery did not produce the required column contract.");
        }

        var actualIndexes = new Dictionary<string, (bool Unique, List<string> Columns)>(StringComparer.Ordinal);
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
SELECT INDEX_NAME, NON_UNIQUE, COLUMN_NAME, COLLATION, SUB_PART, IS_VISIBLE
FROM information_schema.STATISTICS
WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'liens_LegacyUpdateEvents'
ORDER BY INDEX_NAME, SEQ_IN_INDEX;
""";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var name = reader.GetString(0);
                if (!actualIndexes.TryGetValue(name, out var index))
                {
                    index = (reader.GetInt32(1) == 0, []);
                    actualIndexes.Add(name, index);
                }

                index.Columns.Add(string.Join(':',
                    reader.GetString(2),
                    reader.IsDBNull(3) ? "A" : reader.GetString(3),
                    reader.IsDBNull(4) ? "FULL" : reader.GetValue(4).ToString(),
                    reader.GetString(5)));
            }
        }

        if (RequiredIndexes.Any(required =>
            !actualIndexes.TryGetValue(required.Key, out var actual)
            || actual.Unique != required.Value.Unique
            || !actual.Columns.SequenceEqual(required.Value.Columns, StringComparer.Ordinal)))
        {
            throw new InvalidOperationException(
                "Legacy update-event schema recovery did not produce the required index contract.");
        }

        var constraints = new Dictionary<string, ConstraintContract>(StringComparer.Ordinal);
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
SELECT tc.CONSTRAINT_NAME, tc.CONSTRAINT_TYPE, cc.CHECK_CLAUSE, tc.ENFORCED
FROM information_schema.TABLE_CONSTRAINTS tc
LEFT JOIN information_schema.CHECK_CONSTRAINTS cc
  ON cc.CONSTRAINT_SCHEMA = tc.CONSTRAINT_SCHEMA
 AND cc.CONSTRAINT_NAME = tc.CONSTRAINT_NAME
WHERE tc.TABLE_SCHEMA = DATABASE() AND tc.TABLE_NAME = 'liens_LegacyUpdateEvents';
""";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                constraints[reader.GetString(0)] = new ConstraintContract(
                    reader.GetString(1),
                    reader.IsDBNull(2) ? null : reader.GetString(2),
                    reader.GetString(3));
            }
        }

        if (!constraints.TryGetValue("PRIMARY", out var primary)
            || !string.Equals(primary.Type, "PRIMARY KEY", StringComparison.Ordinal)
            || !constraints.TryGetValue("CK_LegacyUpdateEvents_Scope", out var scope)
            || !CheckConstraintMatches(scope, "scopein('case','lien')")
            || !constraints.TryGetValue("CK_LegacyUpdateEvents_ScopeLien", out var scopeLien)
            || !CheckConstraintMatches(
                scopeLien,
                "(scope='case'andlienidisnull)or(scope='lien'andlienidisnotnull)")
            || !constraints.TryGetValue(
                "FK_liens_LegacyUpdateEvents_liens_LegacyImportRuns_ImportRunId",
                out var foreignKey)
            || !string.Equals(foreignKey.Type, "FOREIGN KEY", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Legacy update-event schema recovery did not produce the required constraint contract.");
        }

        var foreignKeyColumns = new List<ForeignKeyColumnContract>();
        await using (var foreignKeyCommand = connection.CreateCommand())
        {
            foreignKeyCommand.CommandText = """
SELECT k.COLUMN_NAME, k.REFERENCED_TABLE_NAME, k.REFERENCED_COLUMN_NAME, r.DELETE_RULE
FROM information_schema.KEY_COLUMN_USAGE k
INNER JOIN information_schema.REFERENTIAL_CONSTRAINTS r
  ON r.CONSTRAINT_SCHEMA = k.CONSTRAINT_SCHEMA
 AND r.CONSTRAINT_NAME = k.CONSTRAINT_NAME
WHERE k.CONSTRAINT_SCHEMA = DATABASE()
  AND k.TABLE_NAME = 'liens_LegacyUpdateEvents'
  AND k.CONSTRAINT_NAME = 'FK_liens_LegacyUpdateEvents_liens_LegacyImportRuns_ImportRunId'
ORDER BY k.ORDINAL_POSITION;
""";
            await using var reader = await foreignKeyCommand.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                foreignKeyColumns.Add(new ForeignKeyColumnContract(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3)));
            }
        }

        if (foreignKeyColumns.Count != 1
            || foreignKeyColumns[0] != new ForeignKeyColumnContract(
                "ImportRunId",
                "liens_LegacyImportRuns",
                "Id",
                "RESTRICT"))
        {
            throw new InvalidOperationException(
                "Legacy update-event schema recovery did not produce the required import-run foreign key.");
        }
    }

    private static async Task<bool> ReconcileMigrationHistoryAsync(
        LiensDbContext db,
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        var historyRepository = db.GetService<IHistoryRepository>();
        await using (var createCommand = connection.CreateCommand())
        {
            createCommand.CommandText = historyRepository.GetCreateIfNotExistsScript();
            await createCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        var appliedMigrations = await historyRepository.GetAppliedMigrationsAsync(cancellationToken);
        if (appliedMigrations.Any(row => string.Equals(row.MigrationId, MigrationId, StringComparison.Ordinal)))
            return false;

        await using var insertCommand = connection.CreateCommand();
        insertCommand.CommandText = historyRepository.GetInsertScript(new HistoryRow(MigrationId, ProductVersion));
        try
        {
            if (await insertCommand.ExecuteNonQueryAsync(cancellationToken) != 1)
                throw new InvalidOperationException("Legacy update-event EF migration history could not be reconciled.");
        }
        catch (DbException)
        {
            var concurrentlyApplied = await historyRepository.GetAppliedMigrationsAsync(cancellationToken);
            if (concurrentlyApplied.Any(row =>
                string.Equals(row.MigrationId, MigrationId, StringComparison.Ordinal)))
                return false;
            throw;
        }

        var reconciledMigrations = await historyRepository.GetAppliedMigrationsAsync(cancellationToken);
        if (!reconciledMigrations.Any(row =>
            string.Equals(row.MigrationId, MigrationId, StringComparison.Ordinal)))
            throw new InvalidOperationException("Legacy update-event EF migration history verification failed.");
        return true;
    }

    private static bool ColumnMatches(ColumnContract required, ColumnContract actual) =>
        string.Equals(required.ColumnType, actual.ColumnType, StringComparison.Ordinal)
        && required.Nullable == actual.Nullable
        && (required.Collation is null
            ? actual.Collation is null
            : required.Collation.EndsWith('_')
                ? actual.Collation?.StartsWith(required.Collation, StringComparison.Ordinal) == true
                : string.Equals(required.Collation, actual.Collation, StringComparison.Ordinal));

    private static bool CheckConstraintMatches(ConstraintContract actual, string requiredClause) =>
        string.Equals(actual.Type, "CHECK", StringComparison.Ordinal)
        && string.Equals(actual.Enforced, "YES", StringComparison.Ordinal)
        && string.Equals(NormalizeCheckClause(actual.CheckClause), requiredClause, StringComparison.Ordinal);

    private static string NormalizeCheckClause(string? clause)
    {
        if (clause is null)
            return string.Empty;

        var normalized = new string(clause
            .Where(character => !char.IsWhiteSpace(character) && character != '`')
            .ToArray())
            .ToLowerInvariant()
            .Replace("_utf8mb4", string.Empty, StringComparison.Ordinal)
            .Replace("\\'", "'", StringComparison.Ordinal);

        while (HasRedundantOuterParentheses(normalized))
            normalized = normalized[1..^1];
        return normalized;
    }

    private static bool HasRedundantOuterParentheses(string expression)
    {
        if (expression.Length < 2 || expression[0] != '(' || expression[^1] != ')')
            return false;

        var depth = 0;
        for (var index = 0; index < expression.Length; index++)
        {
            depth += expression[index] switch
            {
                '(' => 1,
                ')' => -1,
                _ => 0,
            };
            if (depth == 0 && index < expression.Length - 1)
                return false;
            if (depth < 0)
                return false;
        }
        return depth == 0;
    }

    private sealed record ColumnContract(string ColumnType, bool Nullable, string? Collation);
    private sealed record IndexContract(bool Unique, IReadOnlyList<string> Columns);
    private sealed record ConstraintContract(string Type, string? CheckClause, string Enforced);
    private sealed record ForeignKeyColumnContract(
        string Column,
        string ReferencedTable,
        string ReferencedColumn,
        string DeleteRule);

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
