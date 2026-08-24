namespace Liens.Api.Tests.Infrastructure;

public sealed class HectorImportedPaymentRepairContractTests
{
    [Fact]
    public void Repair_is_bound_to_the_reviewed_production_entities_and_provenance()
    {
        var sql = ReadSql("repair-hector-zaldana-26-31912-imported-payment.sql");

        sql.Should().Contain("BINARY @target_schema = BINARY 'LS_LIENS'");
        sql.Should().Contain("019f1a05-7459-7855-b46b-110a702e37a4");
        sql.Should().Contain("26-31912");
        sql.Should().Contain("6da8ac27-9d64-11f1-b823-12a7a8afef43");
        sql.Should().Contain("6e64c54d-9d64-11f1-b823-12a7a8afef43");
        sql.Should().Contain("6e64c5d7-9d64-11f1-b823-12a7a8afef43");
        sql.Should().Contain("72a94988-9d64-11f1-b823-12a7a8afef43");
        sql.Should().Contain("'41410'");
        sql.Should().Contain("legacyCaseId=31912");
        sql.Should().Contain("status=4");
        sql.Should().Contain("checkAmount=");
        sql.Should().Contain("target_case.DemandAmount = 32078.00");
        sql.Should().Contain("@matched_crosswalk_count = 4");
    }

    [Fact]
    public void Repair_soft_deletes_only_the_false_payment_and_preserves_two_open_liens()
    {
        var sql = ReadSql("repair-hector-zaldana-26-31912-imported-payment.sql");

        sql.Should().Contain("SET payment.IsDeleted = 1");
        sql.Should().Contain("@post_open_lien_count = 2");
        sql.Should().Contain("@post_active_payment_total = 0.00");
        sql.Should().Contain("@post_active_no_recovery_count = 0");
        sql.Should().NotContain("UPDATE liens_Liens");
        sql.Should().NotContain("UPDATE liens_Cases");
        sql.Should().NotContain("DELETE FROM liens_SettlementPaymentDetails");
        sql.Should().NotContain("SET payment.Amount =");
    }

    [Fact]
    public void Repair_is_dry_run_first_serializable_and_checksum_gated()
    {
        var sql = ReadSql("repair-hector-zaldana-26-31912-imported-payment.sql");

        sql.Should().Contain("SET @apply = 0");
        sql.Should().Contain("SET @expected_updates = -1");
        sql.Should().Contain("SET @expected_checksum = NULL");
        sql.Should().Contain("SET TRANSACTION ISOLATION LEVEL SERIALIZABLE");
        sql.Should().Contain("START TRANSACTION");
        sql.Should().Contain("FOR UPDATE");
        sql.Should().Contain("@expected_updates = @changes_to_apply");
        sql.Should().Contain("LOWER(COALESCE(@expected_checksum, '')) = LOWER(@plan_checksum)");
        sql.Should().Contain("IF(@apply_permitted = 1, 'COMMIT', 'SELECT 1')");
        sql.Should().Contain("ROLLBACK;");
        sql.Should().NotContain("'COMMIT', 'ROLLBACK'");
    }

    [Fact]
    public void Emergency_rollback_restores_only_the_reviewed_false_artifact()
    {
        var sql = ReadSql("rollback-hector-zaldana-26-31912-payment-repair.sql");

        sql.Should().Contain("WARNING: this intentionally restores the false $17,228");
        sql.Should().Contain("SET payment.IsDeleted = 0");
        sql.Should().Contain("payment.UpdatedAtUtc = @repair_timestamp");
        sql.Should().Contain("BINARY payment.UpdatedByUserId = BINARY @repair_actor_user_id");
        sql.Should().Contain("SET TRANSACTION ISOLATION LEVEL SERIALIZABLE");
        sql.Should().Contain("IF(@apply_permitted = 1, 'COMMIT', 'SELECT 1')");
        sql.Should().NotContain("UPDATE liens_Liens");
        sql.Should().NotContain("UPDATE liens_Cases");
        sql.Should().NotContain("DELETE FROM liens_SettlementPaymentDetails");
    }

    private static string ReadSql(string fileName)
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "scripts",
            "LegacyLiensImport",
            fileName);
        return File.ReadAllText(path);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "scripts", "LegacyLiensImport")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
