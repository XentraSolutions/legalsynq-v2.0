namespace Liens.Api.Tests.Infrastructure;

public sealed class LegacyCaseNoteImportContractTests
{
    private static readonly string[] SupportedSqlImporters =
    [
        "import-sl-core-complete.sql",
        "import-sl-core-core-tenant-only.sql",
        "import-sl-core-core-to-019ea7f6-21e9-7421-ab54-7846cdc6bc76.sql",
    ];

    [Fact]
    public void Supported_importers_preserve_case_note_type_discriminator_and_versioned_hash()
    {
        var importDirectory = Path.Combine(FindRepositoryRoot(), "scripts", "LegacyLiensImport");
        var runner = File.ReadAllText(Path.Combine(importDirectory, "Program.cs"));

        runner.Should().Contain("n.CN_USER_ID");
        runner.Should().Contain("case-note-v2:");
        runner.Should().Contain("plan.Source.LegacyUserId is null ? \"general\" : \"feed\"");

        foreach (var filename in SupportedSqlImporters)
        {
            var sql = File.ReadAllText(Path.Combine(importDirectory, filename));
            sql.Should().Contain("CN_USER_ID");
            sql.Should().Contain("case-note-v2:");
            sql.Should().Contain("WHEN LegacyUserId IS NULL THEN 'general' ELSE 'feed'");
        }
    }

    [Fact]
    public void Reconciliation_requires_provenance_conflict_checks_transaction_and_postconditions()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "scripts",
            "LegacyLiensImport",
            "reconcile-sl-core-case-note-categories.sql");
        var sql = File.ReadAllText(path);

        sql.Should().Contain("SL_MIGRATION_SOURCE_PROVENANCE");
        sql.Should().Contain("MappingApprovalReference");
        sql.Should().Contain("r.ApprovalId IS NULL");
        sql.Should().Contain("MappingManifestHash");
        sql.Should().Contain("p_expected_notes");
        sql.Should().Contain("p_expected_checksum");
        sql.Should().Contain("apply snapshot does not match the approved preflight");
        sql.Should().Contain("GET_LOCK");
        sql.Should().Contain("liens:slcore:");
        sql.Should().Contain("START TRANSACTION");
        sql.Should().Contain("ROLLBACK");
        sql.Should().Contain("IsEdited <> 0");
        sql.Should().Contain("NoteTenantId <> v_tenant_id");
        sql.Should().Contain("CaseTenantId <> v_tenant_id");
        sql.Should().Contain("ExpectedChecksum");
        sql.Should().Contain("apply postcondition failed");
        sql.Should().Contain("LEFT JOIN liens_CaseNotes");
        sql.IndexOf("START TRANSACTION", StringComparison.Ordinal)
            .Should().BeLessThan(sql.IndexOf("CREATE TEMPORARY TABLE tmp_case_note_reconciliation", StringComparison.Ordinal));
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
