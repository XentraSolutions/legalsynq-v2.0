namespace Liens.Api.Tests.Infrastructure;

public sealed class ApprovedDefaultReductionRepairContractTests
{
    [Fact]
    public void Repair_is_bound_to_the_approved_cohort_and_records_default_date_provenance()
    {
        var sql = ReadRepairSql();

        sql.Should().Contain("019fb470-f161-7fbd-93a0-c808d43c43c3");
        sql.Should().Contain("0ab1aa20-9e22-11f1-9a38-0a971fa4811b");
        sql.Should().Contain("2026-04-27");
        sql.Should().Contain("v_source_rows <> 192");
        sql.Should().Contain("v_distinct_liens <> 192");
        sql.Should().Contain("v_blank_source_dates <> 192");
        sql.Should().Contain("v_reduction_total <> 467303.5100");
        sql.Should().Contain("reductionDateSource=business-approved-default");
        sql.Should().Contain("authoritativeSourceReductionDate=<blank>");
        sql.Should().Contain("SL_LIENS_SETTLEMENT_REDUCTION_APPROVED_DEFAULT_DATE");
    }

    [Fact]
    public void Apply_uses_approved_assertions_locks_and_exact_postconditions()
    {
        var sql = ReadRepairSql();

        sql.Should().NotContain("DROP PROCEDURE");
        sql.Should().Contain("p_expected_checksum");
        sql.Should().Contain("SET TRANSACTION ISOLATION LEVEL SERIALIZABLE");
        sql.Should().Contain("FORCE INDEX (IX_liens_LienReductions_TenantId_LienId)");
        sql.Should().Contain("FOR UPDATE");
        sql.Should().Contain("v_unrelated_reduction_rows <> 0");
        sql.Should().Contain("v_post_rows <> 192");
        sql.Should().Contain("v_post_distinct_liens <> 192");
        sql.Should().Contain("v_post_reduction_total <> 467303.5100");
        sql.Should().Contain("v_completed_repair_runs <> 1");
        sql.Should().Contain("BINARY t.ExistingImportRunId <>");
        sql.Should().Contain("BINARY COALESCE(t.MetadataNote, '') <>");
        sql.Should().Contain("BINARY COALESCE(r.Note, '') <>");
        sql.Should().Contain("ROLLBACK");
        sql.Should().Contain("COMMIT");
        sql.Should().NotContain("ALTER TABLE tmp_sl_core_approved_default_reductions");
        sql.IndexOf("START TRANSACTION", StringComparison.Ordinal)
            .Should().BeLessThan(sql.IndexOf(
                "CREATE TEMPORARY TABLE tmp_sl_core_approved_default_reductions",
                StringComparison.Ordinal));
    }

    private static string ReadRepairSql()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "scripts",
            "LegacyLiensImport",
            "materialize-sl-core-approved-default-reductions.sql");
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
