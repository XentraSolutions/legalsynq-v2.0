namespace Liens.Api.Tests.Infrastructure;

public sealed class MemetImportedPaymentRepairContractTests
{
    [Fact]
    public void Repair_resolves_environment_targets_from_the_reviewed_legacy_ids()
    {
        var sql = ReadRepairSql();

        sql.Should().Contain("019fb470-f161-7fbd-93a0-c808d43c43c3");
        sql.Should().Contain("019f1a05-7459-7855-b46b-110a702e37a4");
        sql.Should().Contain("25-02030");
        sql.Should().Contain("SET @legacy_case_id = '27208'");
        sql.Should().Contain("'59915'");
        sql.Should().Contain("'59916'");
        sql.Should().Contain("'41411'");
        sql.Should().Contain("'41412'");
        sql.Should().Contain("x.SourceTable = 'SL_CASE'");
        sql.Should().Contain("x.SourceTable = 'SL_LEINS_MEDICAL'");
        sql.Should().Contain("x.SourceTable = 'SL_LIENS_SETTLEMENT_PAYMENT_DETAILS'");
        sql.Should().Contain("legacyCaseId=2720899999999999");
        sql.Should().Contain("status=4");
        sql.Should().Contain("@input_count = 2");
        sql.Should().Contain("CaseCrosswalkCount <> 1");
        sql.Should().Contain("LienCrosswalkCount <> 1");
        sql.Should().Contain("PaymentCrosswalkCount <> 1");
        sql.Should().NotContain("6d99e5d3-9d64-11f1-b823-12a7a8afef43");
        sql.Should().NotContain("72a94a03-9d64-11f1-b823-12a7a8afef43");
        sql.Should().NotContain("72a94a79-9d64-11f1-b823-12a7a8afef43");
    }

    [Fact]
    public void Apply_is_dry_run_first_transactional_and_soft_delete_only()
    {
        var sql = ReadRepairSql();

        sql.Should().Contain("SET @apply = 0");
        sql.Should().Contain("SET @expected_updates = -1");
        sql.Should().Contain("SET @expected_checksum = NULL");
        sql.Should().Contain("@expected_updates = @changes_to_apply");
        sql.Should().Contain("LOWER(COALESCE(@expected_checksum, '')) = LOWER(@plan_checksum)");
        sql.Should().Contain("START TRANSACTION");
        sql.Should().Contain("FOR UPDATE");
        sql.Should().Contain("SET payment.IsDeleted = 1");
        sql.Should().Contain("payment.UpdatedByUserId = @actor_user_id");
        sql.Should().Contain("'COMMIT', 'ROLLBACK'");
        sql.Should().NotContain("DELETE FROM liens_SettlementPaymentDetails");
        sql.Should().NotContain("SET payment.Amount =");
    }

    private static string ReadRepairSql()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "scripts",
            "LegacyLiensImport",
            "repair-memet-hussein-25-02030-imported-payments.sql");
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
