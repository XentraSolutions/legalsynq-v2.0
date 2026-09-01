namespace Liens.Api.Tests.Infrastructure;

public sealed class SellingCaseDraftConcurrencyTokenScriptContractTests
{
    [Fact]
    public void Script_repairs_schema_before_recording_migration_history()
    {
        var sql = ReadScript();

        sql.Should().Contain("GET_LOCK('legalsynq-liens-selling-schema-repair', 30)");
        sql.Should().Contain("20260826141219_AddSellingCaseDraft");
        sql.Should().Contain("ADD COLUMN `ConcurrencyToken` char(36) COLLATE ascii_general_ci NULL");
        sql.Should().Contain("SET `ConcurrencyToken` = UUID()");
        sql.Should().Contain("MODIFY COLUMN `ConcurrencyToken` char(36) COLLATE ascii_general_ci NOT NULL");
        sql.Should().Contain("__concurrency_token_postcondition_failed__");

        sql.IndexOf("MODIFY COLUMN `ConcurrencyToken`", StringComparison.Ordinal)
            .Should().BeLessThan(
                sql.IndexOf(
                    "SELECT @selling_case_draft_migration_id, '8.0.2'",
                    StringComparison.Ordinal));
    }

    [Fact]
    public void Script_is_restart_safe_and_does_not_expose_connection_secrets()
    {
        var sql = ReadScript();

        sql.Should().Contain("information_schema.COLUMNS");
        sql.Should().Contain("WHERE NOT EXISTS");
        sql.Should().Contain("RELEASE_LOCK('legalsynq-liens-selling-schema-repair')");
        sql.Should().Contain("--defaults-extra-file=/secure/liens.cnf");
        sql.Contains("password=", StringComparison.OrdinalIgnoreCase).Should().BeFalse();
    }

    private static string ReadScript()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "scripts",
            "apply-selling-case-draft-concurrency-token-migration.sql");
        return File.ReadAllText(path);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "scripts")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
