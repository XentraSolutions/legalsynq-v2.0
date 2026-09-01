namespace Liens.Api.Tests.Infrastructure;

public sealed class DarinClosedAtZeroRollbackContractTests
{
    [Fact]
    public void Rollback_is_production_only_and_targets_the_reviewed_repair_rows()
    {
        var sql = ReadRollbackSql();

        sql.Should().Contain("BINARY @target_schema = BINARY 'LS_LIENS'");
        sql.Should().Contain("019f1a05-7459-7855-b46b-110a702e37a4");
        sql.Should().Contain("6da5cccd-9d64-11f1-b823-12a7a8afef43");
        sql.Should().Contain("6e58f0ee-9d64-11f1-b823-12a7a8afef43");
        sql.Should().Contain("72acb13c-9d64-11f1-b823-12a7a8afef43");
        sql.Should().Contain("01a02b21-5580-772f-af1a-a18f5d996a5c");
        sql.Should().Contain("legacyPaymentDetailId=41970");
        sql.Should().Contain("legacyCaseId=309849999999999");
        sql.Should().Contain("@matched_crosswalk_count = 3");
        sql.Should().Contain("FROM LS_IDENTITY.idt_Users");
        sql.Should().Contain("INNER JOIN LS_IDENTITY.idt_UserTenants");
    }

    [Fact]
    public void Rollback_requires_the_untouched_shared_repair_audit_stamp()
    {
        var sql = ReadRollbackSql();

        sql.Should().Contain("lien.UpdatedAtUtc = bad_payment.UpdatedAtUtc");
        sql.Should().Contain("lien.UpdatedAtUtc = zero_payment.UpdatedAtUtc");
        sql.Should().Contain("BINARY lien.UpdatedByUserId = BINARY bad_payment.UpdatedByUserId");
        sql.Should().Contain("BINARY lien.UpdatedByUserId = BINARY zero_payment.UpdatedByUserId");
        sql.Should().Contain("payment.UpdatedAtUtc = @repair_timestamp");
        sql.Should().Contain("payment.UpdatedByUserId = @repair_actor_user_id");
        sql.Should().Contain("lien.UpdatedAtUtc = @repair_timestamp");
        sql.Should().Contain("lien.UpdatedByUserId = @repair_actor_user_id");
        sql.Should().Contain("@repaired_state_count = 1");
        sql.Should().Contain("@already_rolled_back_state_count = 1");
    }

    [Fact]
    public void Rollback_restores_only_the_three_reviewed_business_values()
    {
        var sql = ReadRollbackSql();

        sql.Should().Contain("SET payment.IsDeleted = 0");
        sql.Should().Contain("payment.Note = @original_zero_payment_note");
        sql.Should().Contain("lien.CurrentBalance = 16000.00");
        sql.Should().Contain("lien.PayoffAmount = NULL");
        sql.Should().Contain("@case_active_payment_total = 49500.00");
        sql.Should().Contain("@target_active_payment_total = 16000.00");
        sql.Should().Contain("@target_active_no_recovery_count = 2");
        sql.Should().NotContain("SET lien.Status =");
        sql.Should().NotContain("SET lien.ClosedAtUtc =");
        sql.Should().NotContain("UPDATE liens_Cases");
        sql.Should().NotContain("DELETE FROM");
    }

    [Fact]
    public void Rollback_is_dry_run_first_serializable_and_checksum_guarded()
    {
        var sql = ReadRollbackSql();

        sql.Should().Contain("SET @apply = 0");
        sql.Should().Contain("SET @expected_updates = -1");
        sql.Should().Contain("SET @expected_checksum = NULL");
        sql.Should().Contain("@expected_updates = @changes_to_apply");
        sql.Should().Contain("LOWER(COALESCE(@expected_checksum, '')) = LOWER(@plan_checksum)");
        sql.Should().Contain("SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;\nSTART TRANSACTION;");
        sql.Should().Contain("FOR UPDATE");
        sql.Should().Contain("FOR SHARE");
        sql.Should().Contain("IF(@apply_permitted = 1, 'COMMIT', 'SELECT 1')");
        sql.Should().Contain("DEALLOCATE PREPARE darin_rollback_conditional_commit;\nROLLBACK;");
        sql.Should().NotContain("'COMMIT', 'ROLLBACK'");
    }

    private static string ReadRollbackSql()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "scripts",
            "LegacyLiensImport",
            "rollback-darin-tellis-25-01967-lien-05-repair.sql");
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
