namespace Liens.Api.Tests.Infrastructure;

public sealed class DarinClosedAtZeroRepairContractTests
{
    [Fact]
    public void Repair_is_pinned_to_the_reviewed_production_entities_and_provenance()
    {
        var sql = ReadRepairSql();

        sql.Should().Contain("BINARY @target_schema = BINARY 'LS_LIENS'");
        sql.Should().Contain("019f1a05-7459-7855-b46b-110a702e37a4");
        sql.Should().Contain("6da5cccd-9d64-11f1-b823-12a7a8afef43");
        sql.Should().Contain("6e58f0ee-9d64-11f1-b823-12a7a8afef43");
        sql.Should().Contain("72acb13c-9d64-11f1-b823-12a7a8afef43");
        sql.Should().Contain("01a02b21-5580-772f-af1a-a18f5d996a5c");
        sql.Should().Contain("legacyPaymentDetailId=41970");
        sql.Should().Contain("legacyCaseId=309849999999999");
        sql.Should().Contain("'SL_CASE', '30984'");
        sql.Should().Contain("'SL_LEINS_MEDICAL', '73236'");
        sql.Should().Contain("'SL_LIENS_SETTLEMENT_PAYMENT_DETAILS', '41970'");
        sql.Should().Contain("@matched_crosswalk_count = 3");
        sql.Should().Contain("FROM LS_IDENTITY.idt_Users");
        sql.Should().Contain("INNER JOIN LS_IDENTITY.idt_UserTenants");
    }

    [Fact]
    public void Apply_keeps_the_lien_closed_at_zero_and_removes_no_recovery()
    {
        var sql = ReadRepairSql();

        sql.Should().Contain("SET payment.IsDeleted = 1");
        sql.Should().Contain("payment.Note = @corrected_zero_payment_note");
        sql.Should().Contain("lien.CurrentBalance = 0.00");
        sql.Should().Contain("lien.PayoffAmount = 0.00");
        sql.Should().Contain("'Settled' AS PreservedLienStatus");
        sql.Should().Contain("'2026-08-22 20:19:59.588613' AS PreservedClosedAtUtc");
        sql.Should().Contain("@target_active_payment_total = 0.00");
        sql.Should().Contain("@target_active_no_recovery_count = 0");
        sql.Should().Contain("@case_active_payment_total = 33500.00");
        sql.Should().Contain("'netProfit=0.00; type=other'");
        sql.Should().NotContain("status=Settled");
        sql.Should().NotContain("SET lien.Status =");
        sql.Should().NotContain("SET lien.ClosedAtUtc =");
        sql.Should().NotContain("UPDATE liens_Cases");
        sql.Should().NotContain("DELETE FROM");
    }

    [Fact]
    public void Apply_is_dry_run_first_checksum_guarded_transactional_and_idempotent()
    {
        var sql = ReadRepairSql();

        sql.Should().Contain("SET @apply = 0");
        sql.Should().Contain("SET @expected_updates = -1");
        sql.Should().Contain("SET @expected_checksum = NULL");
        sql.Should().Contain("@expected_updates = @changes_to_apply");
        sql.Should().Contain("LOWER(COALESCE(@expected_checksum, '')) = LOWER(@plan_checksum)");
        sql.Should().Contain("START TRANSACTION");
        sql.Should().Contain("SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;\nSTART TRANSACTION;");
        sql.Should().Contain("FOR UPDATE");
        sql.Should().Contain("IF(@apply_permitted = 1, 'COMMIT', 'SELECT 1')");
        sql.Should().Contain("DEALLOCATE PREPARE darin_repair_conditional_commit;\nROLLBACK;");
        sql.Should().NotContain("'COMMIT', 'ROLLBACK'");
        sql.Should().Contain("@actor_user_id,");
        sql.Should().Contain("@locked_crosswalk_count = 3");
        sql.Should().Contain("@locked_target_reduction_count = 0");
        sql.Should().Contain("@locked_target_settlement_count = 0");
        sql.Should().Contain("@already_repaired = IF(@final_state_ok = 1, 1, 0)");
        sql.Should().Contain("@changes_to_apply = IF(@initial_state_ok = 1, 3, 0)");
    }

    private static string ReadRepairSql()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "scripts",
            "LegacyLiensImport",
            "repair-darin-tellis-25-01967-lien-05.sql");
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
