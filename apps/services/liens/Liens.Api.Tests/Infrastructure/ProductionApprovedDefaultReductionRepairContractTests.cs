namespace Liens.Api.Tests.Infrastructure;

public sealed class ProductionApprovedDefaultReductionRepairContractTests
{
    [Fact]
    public void Repair_is_bound_to_the_completed_production_metadata_cohort()
    {
        var sql = ReadRepairSql();

        sql.Should().Contain(
            "CREATE PROCEDURE liens_materialize_sl_core_approved_default_reductions_prod_v1");
        sql.Should().Contain("019f1a05-7459-7855-b46b-110a702e37a4");
        sql.Should().Contain("35cece1a-9e54-11f1-b823-12a7a8afef43");
        sql.Should().Contain("IF DATABASE() <> 'LS_LIENS' THEN");
        sql.Should().Contain("2026-04-27");
        sql.Should().Contain("v_source_rows <> 192");
        sql.Should().Contain("v_distinct_liens <> 192");
        sql.Should().Contain("v_blank_source_dates <> 192");
        sql.Should().Contain("v_reduction_total <> 467303.5100");
        sql.Should().NotContain("019fb470-f161-7fbd-93a0-c808d43c43c3");
        sql.Should().NotContain("0ab1aa20-9e22-11f1-9a38-0a971fa4811b");
    }

    [Fact]
    public void Apply_preserves_transactional_and_collation_independent_guards()
    {
        var sql = ReadRepairSql();

        sql.Should().NotContain("DROP PROCEDURE");
        sql.Should().Contain("SET TRANSACTION ISOLATION LEVEL SERIALIZABLE");
        sql.Should().Contain("BINARY r.TenantId = BINARY v_tenant_id");
        sql.Should().Contain("BINARY LOWER(p.SOURCE_FINGERPRINT) =");
        sql.Should().Contain("BINARY COALESCE(t.MetadataNote, '') <>");
        sql.Should().Contain("BINARY LOWER(p_expected_checksum) <> BINARY v_checksum");
        sql.Should().Contain("ROLLBACK");
        sql.Should().Contain("COMMIT");
    }

    private static string ReadRepairSql()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "scripts",
            "LegacyLiensImport",
            "materialize-sl-core-approved-default-reductions-prod.sql");
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
