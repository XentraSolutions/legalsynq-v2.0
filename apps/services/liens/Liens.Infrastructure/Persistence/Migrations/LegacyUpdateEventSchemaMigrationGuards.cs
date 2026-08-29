using Microsoft.EntityFrameworkCore.Migrations;

namespace Liens.Infrastructure.Persistence.Migrations;

internal static class LegacyUpdateEventSchemaMigrationGuards
{
    private const string TableName = "liens_LegacyUpdateEvents";

    public static void CreateTableIfMissing(MigrationBuilder migrationBuilder, string sql)
        => migrationBuilder.Sql(Terminate(sql), suppressTransaction: true);

    public static void CreateIndexIfMissing(
        MigrationBuilder migrationBuilder,
        string index,
        string columns,
        bool unique = false)
    {
        var ddl = $"CREATE {(unique ? "UNIQUE " : string.Empty)}INDEX `{index}` ON `{TableName}` {columns}";
        var escapedDdl = ddl.Replace("'", "''", StringComparison.Ordinal);

        migrationBuilder.Sql(
            $"""
            SET @legalsynq_legacy_update_event_ddl = IF(
                (SELECT COUNT(*)
                 FROM information_schema.STATISTICS
                 WHERE TABLE_SCHEMA = DATABASE()
                   AND TABLE_NAME = '{TableName}'
                   AND INDEX_NAME = '{index}') = 0,
                '{escapedDdl}',
                'SELECT 1');
            PREPARE legalsynq_legacy_update_event_stmt FROM @legalsynq_legacy_update_event_ddl;
            EXECUTE legalsynq_legacy_update_event_stmt;
            DEALLOCATE PREPARE legalsynq_legacy_update_event_stmt;
            """,
            suppressTransaction: true);
    }

    private static string Terminate(string sql)
    {
        var normalized = sql.TrimEnd();
        return normalized.EndsWith(';') ? normalized : $"{normalized};";
    }
}
