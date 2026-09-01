namespace Liens.Api.Tests.Infrastructure;

public sealed class JulioNoRecoveryAmountRepairContractTests
{
    [Fact]
    public void Repair_is_bound_to_the_reviewed_production_entities_and_provenance()
    {
        var sql = ReadRepairSql();

        sql.Should().Contain("BINARY @target_schema = BINARY 'LS_LIENS'");
        sql.Should().Contain("019f1a05-7459-7855-b46b-110a702e37a4");
        sql.Should().Contain("26-32723");
        sql.Should().Contain("6daaf5dd-9d64-11f1-b823-12a7a8afef43");
        sql.Should().Contain("6e69e862-9d64-11f1-b823-12a7a8afef43");
        sql.Should().Contain("72abe00f-9d64-11f1-b823-12a7a8afef43");
        sql.Should().Contain("'SL_CASE', '32723'");
        sql.Should().Contain("'SL_LEINS_MEDICAL', '72342'");
        sql.Should().Contain("'SL_LIENS_SETTLEMENT_PAYMENT_DETAILS', '41604'");
        sql.Should().Contain("legacyPaymentDetailId=41604; legacyCaseId=32723");
        sql.Should().Contain("status=4");
        sql.Should().Contain("checkAmount=");
        sql.Should().Contain("@matched_crosswalk_count = 3");
    }

    [Fact]
    public void Repair_zeroes_only_the_false_receipt_and_preserves_no_recovery()
    {
        var sql = ReadRepairSql();

        System.Text.RegularExpressions.Regex.Matches(sql, @"(?m)^UPDATE\s+")
            .Should().HaveCount(1);

        var updateStart = sql.IndexOf(
            "UPDATE liens_SettlementPaymentDetails payment",
            StringComparison.Ordinal);
        var updateEnd = sql.IndexOf(
            "SET @rows_updated = ROW_COUNT();",
            updateStart,
            StringComparison.Ordinal);
        updateStart.Should().BeGreaterThanOrEqualTo(0);
        updateEnd.Should().BeGreaterThan(updateStart);
        var updateSql = sql[updateStart..updateEnd];

        updateSql.Should().Contain("SET payment.Amount = 0.00");
        updateSql.Should().Contain("BINARY payment.Id = BINARY @payment_id");
        updateSql.Should().Contain("BINARY payment.TenantId = BINARY @tenant_id");
        updateSql.Should().Contain("BINARY payment.CaseId = BINARY @case_id");
        updateSql.Should().Contain("BINARY payment.LienId = BINARY @lien_id");
        updateSql.Should().Contain("payment.Amount = 3700.00");
        updateSql.Should().Contain("BINARY payment.Note = BINARY @payment_note");
        updateSql.Should().Contain("payment.IsDeleted = 0");
        sql.Should().Contain("@post_active_payment_total = 0.00");
        sql.Should().Contain("@post_active_no_recovery_count = 1");
        sql.Should().Contain("@post_case_unchanged = 1");
        sql.Should().Contain("@post_lien_unchanged = 1");
        sql.Should().Contain("target_case.Status = 'Closed'");
        sql.Should().Contain("lien.Status = 'Settled'");
        sql.Should().NotContain("SET payment.IsDeleted = 1");
        sql.Should().NotContain("UPDATE liens_Cases");
        sql.Should().NotContain("UPDATE liens_Liens");
        sql.Should().NotContain("INSERT INTO liens_");
        sql.Should().NotContain("REPLACE INTO");
        sql.Should().NotContain("DELETE FROM");
    }

    [Fact]
    public void Repair_is_dry_run_first_serializable_checksum_gated_and_idempotent()
    {
        var sql = ReadRepairSql();

        sql.Should().Contain("SET @apply = 0");
        sql.Should().Contain("SET @expected_updates = -1");
        sql.Should().Contain("SET @expected_checksum = NULL");
        sql.Should().Contain("SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;\nSTART TRANSACTION;");
        sql.Should().Contain("FOR UPDATE");
        sql.Should().Contain("@expected_updates = @changes_to_apply");
        sql.Should().Contain("LOWER(COALESCE(@expected_checksum, '')) = LOWER(@plan_checksum)");
        sql.Should().Contain("@already_repaired = IF(@final_payment_ok = 1, 1, 0)");
        sql.Should().Contain("IF(@apply_permitted = 1, 'COMMIT', 'SELECT 1')");
        sql.Should().Contain("DEALLOCATE PREPARE julio_repair_conditional_commit;\nROLLBACK;");
        sql.Should().NotContain("'COMMIT', 'ROLLBACK'");
    }

    private static string ReadRepairSql()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "scripts",
            "LegacyLiensImport",
            "repair-julio-de-anda-fajardo-26-32723-no-recovery-amount.sql");
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
