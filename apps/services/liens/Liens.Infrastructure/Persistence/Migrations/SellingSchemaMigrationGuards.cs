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
        bool unique = false)
        => ExecuteConditionally(
            migrationBuilder,
            $"""
            SELECT COUNT(*)
            FROM information_schema.STATISTICS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = '{table}'
              AND INDEX_NAME = '{index}'
            """,
            $"CREATE {(unique ? "UNIQUE " : string.Empty)}INDEX `{index}` ON `{table}` {columns}");

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
    {
        var escapedDdl = ddl.Replace("'", "''", StringComparison.Ordinal);

        migrationBuilder.Sql(
            $"""
            SET @legalsynq_selling_ddl = IF(
                ({countQuery}) = 0,
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
