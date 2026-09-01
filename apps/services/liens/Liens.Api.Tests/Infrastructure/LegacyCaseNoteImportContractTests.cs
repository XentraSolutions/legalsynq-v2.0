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

    [Fact]
    public void Case_note_backfill_inserts_crosswalk_targets_with_mapped_authors()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "scripts",
            "LegacyLiensImport",
            "backfill-sl-core-case-notes.sql");
        var sql = File.ReadAllText(path);

        sql.Should().Contain("INSERT INTO liens_CaseNotes");
        sql.Should().Contain("SL_CASE_NOTES");
        sql.Should().Contain("TargetNoteId, TargetCaseId");
        sql.Should().Contain("CreatedByUserId, CreatedByName");
        sql.Should().Contain("SL_MIGRATION_SOURCE_PROVENANCE");
        sql.Should().Contain("tmp_sl_core_note_crosswalk");
        sql.Should().Contain("tmp_sl_core_case_crosswalk");
        sql.Should().Contain("CREATE TEMPORARY TABLE tmp_sl_core_case_note_backfill (");
        sql.Should().Contain("INSERT INTO tmp_sl_core_case_note_backfill");
        sql.Should().Contain("LegacyNoteId BIGINT UNSIGNED NOT NULL PRIMARY KEY");
        sql.Should().NotContain("HEX(note_x.LegacyId)");
        sql.Should().Contain("NeedsAuthorUpdate");
        sql.Should().Contain("c.CreatedByName = s.DesiredUserName");
        sql.Should().Contain("'system-migration',");
        sql.Should().Contain("CAST(TRIM(source_note.CN_CREATED_BY) AS BINARY) = CAST('migration' AS BINARY)");
        sql.Should().NotContain("BINARY CASE");
        sql.Should().Contain("BINARY c.CreatedByName = BINARY 'Legacy SL-CORE'");
        sql.Should().Contain("BINARY c.CreatedByName = BINARY 'system-migration'");
        sql.Should().Contain("BINARY c.CreatedByName = BINARY s.DesiredUserName");
        sql.Should().Contain("CrosswalkCoverageErrors");
        sql.Should().Contain("AuthorUpdatesToApply");
        sql.Should().Contain("RowsAuthorUpdated");
        sql.Should().Contain("expected change count does not match dry run");
        sql.Should().Contain("case-note backfill has conflicts");
        sql.Should().Contain("case-note backfill postcondition failed");
        sql.Should().Contain("ChangesToApply");
        sql.Should().Contain("ExistingTargetConflicts");
        sql.Should().Contain("Meagan Pugong");
        sql.Should().Contain("01a02571-5e6b-7b80-9c08-48b919999ebd");
        sql.Should().Contain("Sharrel Tibay");
        sql.Should().Contain("019f1a05-792f-74f2-b071-4fdc0d6bd30a");
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
