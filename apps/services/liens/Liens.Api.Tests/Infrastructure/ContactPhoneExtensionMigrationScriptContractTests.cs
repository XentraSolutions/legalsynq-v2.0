namespace Liens.Api.Tests.Infrastructure;

public sealed class ContactPhoneExtensionMigrationScriptContractTests
{
    [Fact]
    public void Script_verifies_schema_before_recording_migration_history()
    {
        var sql = ReadScript();

        sql.Should().Contain("20260904010000_AddContactPhoneExtension");
        sql.Should().Contain("20260903010000_AddCaseUpdateHistory");
        sql.Should().Contain(
            "ALTER TABLE `liens_Contacts` ADD COLUMN `PhoneExtension` varchar(20) CHARACTER SET utf8mb4 NULL");
        sql.Should().Contain("information_schema.COLUMNS");
        sql.Should().Contain("CAST(`MigrationId` AS BINARY)");
        sql.Should().Contain("COLUMN_TYPE = 'varchar(20)'");
        sql.Should().Contain("IS_NULLABLE = 'YES'");
        sql.Should().Contain("INSERT IGNORE INTO `__EFMigrationsHistory`");

        sql.IndexOf("ALTER TABLE `liens_Contacts`", StringComparison.Ordinal)
            .Should().BeLessThan(
                sql.IndexOf("INSERT IGNORE INTO `__EFMigrationsHistory`", StringComparison.Ordinal));
    }

    [Fact]
    public void Script_is_restart_safe_and_does_not_expose_connection_secrets()
    {
        var sql = ReadScript();

        sql.Should().Contain("contact_phone_extension_column_present = 0");
        sql.Should().Contain("contact_phone_extension_contract_valid = 1");
        sql.Should().Contain("--defaults-extra-file=/secure/liens.cnf");
        sql.Contains("password=", StringComparison.OrdinalIgnoreCase).Should().BeFalse();
    }

    [Fact]
    public void Catchup_script_covers_the_observed_seven_migration_gap_in_order()
    {
        var sql = ReadScript("apply-liens-post-selling-draft-catchup.sql");
        var migrationIds = new[]
        {
            "20260829120000_AddLegacyUpdateEvents",
            "20260831010000_OptimizeCaseNoteReportQueries",
            "20260831130318_AddSellingPortalMessageAttachments",
            "20260902020000_ReserveCaseNumbers",
            "20260902030000_ExpandLienStatusHistoryDescription",
            "20260903010000_AddCaseUpdateHistory",
            "20260904010000_AddContactPhoneExtension",
        };

        var previousIndex = -1;
        foreach (var migrationId in migrationIds)
        {
            var index = sql.IndexOf($"-- {migrationId}", StringComparison.Ordinal);
            index.Should().BeGreaterThan(previousIndex);
            previousIndex = index;
        }

        sql.Should().Contain("20260827100000_AddSellingCaseDraftConcurrencyToken");
        sql.Should().Contain("ALTER TABLE `liens_Contacts` ADD COLUMN `PhoneExtension` varchar(20)");
        sql.Should().Contain("CAST(`MigrationId` AS BINARY)");
        sql.Should().Contain("Every row must report READY");
    }

    [Fact]
    public void Catchup_script_repairs_schema_before_reconciling_history()
    {
        var sql = ReadScript("apply-liens-post-selling-draft-catchup.sql");

        sql.IndexOf("CREATE TABLE IF NOT EXISTS `liens_LegacyUpdateEvents`", StringComparison.Ordinal)
            .Should().BeLessThan(sql.IndexOf(
                "SELECT '20260829120000_AddLegacyUpdateEvents', '8.0.2'",
                StringComparison.Ordinal));
        sql.IndexOf("CREATE TABLE IF NOT EXISTS `liens_SellingPortalMessageAttachments`", StringComparison.Ordinal)
            .Should().BeLessThan(sql.IndexOf(
                "SELECT '20260831130318_AddSellingPortalMessageAttachments', '8.0.2'",
                StringComparison.Ordinal));
        sql.IndexOf("ADD COLUMN `PhoneExtension`", StringComparison.Ordinal)
            .Should().BeLessThan(sql.IndexOf(
                "SELECT '20260904010000_AddContactPhoneExtension', '8.0.2'",
                StringComparison.Ordinal));
    }

    private static string ReadScript(string fileName = "apply-contact-phone-extension-migration.sql")
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "scripts",
            fileName);
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
