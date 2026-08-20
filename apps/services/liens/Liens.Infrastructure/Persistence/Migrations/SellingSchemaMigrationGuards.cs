using Microsoft.EntityFrameworkCore.Migrations;

namespace Liens.Infrastructure.Persistence.Migrations;

/// <summary>
/// MySQL DDL helpers for selling migrations that may be resumed after a partially
/// applied production deployment. MySQL auto-commits many DDL statements, so a
/// failed EF migration can leave earlier tables, columns, indexes, or constraints
/// in place even though the migration was not recorded in __EFMigrationsHistory.
/// </summary>
internal static class SellingSchemaMigrationGuards
{
    public static void CreateTableIfMissing(MigrationBuilder migrationBuilder, string sql)
        => migrationBuilder.Sql(Terminate(sql), suppressTransaction: true);

    public static void ExecuteSql(MigrationBuilder migrationBuilder, string sql)
        => migrationBuilder.Sql(Terminate(sql), suppressTransaction: true);

    public static void AddColumnIfMissing(
        MigrationBuilder migrationBuilder,
        string table,
        string column,
        string definition)
        => ExecuteConditionally(
            migrationBuilder,
            $"""
            SELECT COUNT(*)
            FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = '{table}'
              AND COLUMN_NAME = '{column}'
            """,
            $"ALTER TABLE `{table}` ADD COLUMN `{column}` {definition}");

    public static void CreateIndexIfMissing(
        MigrationBuilder migrationBuilder,
        string table,
        string index,
        string columns,
        bool unique = false,
        string? equivalentIndex = null,
        string? replacementColumn = null)
    {
        var indexPredicate = equivalentIndex is null
            ? $"INDEX_NAME = '{index}'"
            : $"INDEX_NAME IN ('{index}', '{equivalentIndex}')";
        var existenceQuery = replacementColumn is null
            ? $"""
              SELECT COUNT(*)
              FROM information_schema.STATISTICS
              WHERE TABLE_SCHEMA = DATABASE()
                AND TABLE_NAME = '{table}'
                AND {indexPredicate}
              """
            : $"""
              SELECT
                  (SELECT COUNT(*)
                   FROM information_schema.STATISTICS
                   WHERE TABLE_SCHEMA = DATABASE()
                     AND TABLE_NAME = '{table}'
                     AND {indexPredicate})
                + (SELECT COUNT(*)
                   FROM information_schema.COLUMNS
                   WHERE TABLE_SCHEMA = DATABASE()
                     AND TABLE_NAME = '{table}'
                     AND COLUMN_NAME = '{replacementColumn}')
              """;

        ExecuteConditionally(
            migrationBuilder,
            existenceQuery,
            $"CREATE {(unique ? "UNIQUE " : string.Empty)}INDEX `{index}` ON `{table}` {columns}");
    }

    public static void DropIndexIfExists(
        MigrationBuilder migrationBuilder,
        string table,
        string index)
        => ExecuteWhenPresent(
            migrationBuilder,
            $"""
            SELECT COUNT(*)
            FROM information_schema.STATISTICS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = '{table}'
              AND INDEX_NAME = '{index}'
            """,
            $"DROP INDEX `{index}` ON `{table}`");

    public static void AddCheckConstraintIfMissing(
        MigrationBuilder migrationBuilder,
        string table,
        string constraint,
        string expression)
        => ExecuteConditionally(
            migrationBuilder,
            $"""
            SELECT COUNT(*)
            FROM information_schema.TABLE_CONSTRAINTS
            WHERE CONSTRAINT_SCHEMA = DATABASE()
              AND TABLE_NAME = '{table}'
              AND CONSTRAINT_NAME = '{constraint}'
              AND CONSTRAINT_TYPE = 'CHECK'
            """,
            $"ALTER TABLE `{table}` ADD CONSTRAINT `{constraint}` CHECK ({expression})");

    public static void AddForeignKeyIfMissing(
        MigrationBuilder migrationBuilder,
        string table,
        string constraint,
        string definition)
        => ExecuteConditionally(
            migrationBuilder,
            $"""
            SELECT COUNT(*)
            FROM information_schema.TABLE_CONSTRAINTS
            WHERE CONSTRAINT_SCHEMA = DATABASE()
              AND TABLE_NAME = '{table}'
              AND CONSTRAINT_NAME = '{constraint}'
              AND CONSTRAINT_TYPE = 'FOREIGN KEY'
            """,
            $"ALTER TABLE `{table}` ADD CONSTRAINT `{constraint}` {definition}");

    private static void ExecuteConditionally(
        MigrationBuilder migrationBuilder,
        string countQuery,
        string ddl)
        => ExecuteConditionalDdl(migrationBuilder, countQuery, ddl, executeWhenPresent: false);

    private static void ExecuteWhenPresent(
        MigrationBuilder migrationBuilder,
        string countQuery,
        string ddl)
        => ExecuteConditionalDdl(migrationBuilder, countQuery, ddl, executeWhenPresent: true);

    private static void ExecuteConditionalDdl(
        MigrationBuilder migrationBuilder,
        string countQuery,
        string ddl,
        bool executeWhenPresent)
    {
        var escapedDdl = ddl.Replace("'", "''", StringComparison.Ordinal);
        var comparison = executeWhenPresent ? "> 0" : "= 0";

        migrationBuilder.Sql(
            $"""
            SET @legalsynq_selling_ddl = IF(
                ({countQuery}) {comparison},
                '{escapedDdl}',
                'SELECT 1');
            PREPARE legalsynq_selling_stmt FROM @legalsynq_selling_ddl;
            EXECUTE legalsynq_selling_stmt;
            DEALLOCATE PREPARE legalsynq_selling_stmt;
            """,
            suppressTransaction: true);
    }

    private static string Terminate(string sql)
    {
        var normalized = sql.TrimEnd();
        return normalized.EndsWith(';') ? normalized : $"{normalized};";
    }
}
