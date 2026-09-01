namespace Liens.Api.Tests.Infrastructure;

public sealed class SettlementMetadataBackfillContractTests
{
    [Fact]
    public void String_identity_comparisons_are_collation_independent()
    {
        var sql = ReadRepairSql();

        sql.Should().Contain("BINARY r.TenantId = BINARY v_tenant_id");
        sql.Should().Contain("BINARY LOWER(p.SOURCE_FINGERPRINT) =");
        sql.Should().Contain("BINARY TargetLienTenantId <> BINARY v_tenant_id");
        sql.Should().Contain("BINARY t.ExistingSourceHash <> BINARY t.ExpectedSourceHash");
        sql.Should().Contain("BINARY COALESCE(t.ExistingNote, '') <> BINARY t.ExpectedNote");
        sql.Should().Contain("BINARY LOWER(TRIM(p_expected_checksum)) <> BINARY v_checksum");
        sql.Should().Contain("BINARY x.SourceHash = BINARY t.ExpectedSourceHash");
        sql.Should().NotContain("SOURCE_FINGERPRINT) COLLATE utf8mb4_0900_ai_ci");
    }

    private static string ReadRepairSql()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "scripts",
            "LegacyLiensImport",
            "backfill-sl-core-settlement-metadata.sql");
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
