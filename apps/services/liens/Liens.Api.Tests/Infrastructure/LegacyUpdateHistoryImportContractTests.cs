using System.Reflection;

namespace Liens.Api.Tests.Infrastructure;

public sealed class LegacyUpdateHistoryImportContractTests
{
    private const string ApprovedFingerprint = "3adccecf8a38114a14cd500240aab2a4db3d9bf45f00945c659dc3b5252663fe";

    [Fact]
    public void Update_history_mode_has_separate_preflight_only_cli_contract()
    {
        var program = ReadImportFile("Program.cs");
        var importer = ReadImportFile("UpdateHistoryImport.cs");

        program.Should().Contain("if (args.Any(arg => arg == \"--import-update-logs\"))");
        program.Should().Contain("return await UpdateHistoryImport.RunAsync(args);");
        program.IndexOf("--import-update-logs", StringComparison.Ordinal)
            .Should().BeLessThan(program.IndexOf("Options.Parse(args)", StringComparison.Ordinal));

        importer.Should().Contain("private sealed record Options(");
        importer.Should().NotContain("LienAmountSource");
        importer.Should().NotContain("CaseNumberCollision");
        importer.Should().NotContain("LienNumberCollision");
        importer.Should().Contain("--legacy-program must be 1 for update-history import.");
        importer.Should().Contain("--apply is disabled for update-history CLI mode");
        importer.Should().Contain("apply through import-program-1-update-history.sql with a one-time database approval");
        importer.Should().Contain("Manifest importScope and mappingVersion must both be");
        importer.Should().Contain(ApprovedFingerprint);
        importer.Should().Contain("CN=LegalSynq Identity Migration Signing");
    }

    [Fact]
    public void Update_history_options_parse_without_core_import_amount_or_collision_choices()
    {
        var options = ParseOptions(
            "--import-update-logs",
            "--tenant-id", Guid.NewGuid().ToString(),
            "--org-id", Guid.NewGuid().ToString(),
            "--migration-user-id", Guid.NewGuid().ToString(),
            "--legacy-program", "1",
            "--legacy-connection", "Server=legacy;Database=source;User Id=test;Password=test;",
            "--target-connection", "Server=target;Database=liens;User Id=test;Password=test;",
            "--source-fingerprint", ApprovedFingerprint);

        options.GetType().GetProperty("LegacyProgram")!.GetValue(options).Should().Be(1L);
        options.GetType().GetProperty("Apply")!.GetValue(options).Should().Be(false);
        options.GetType().GetProperty("LienAmountSource").Should().BeNull();
        options.GetType().GetProperty("CaseNumberCollision").Should().BeNull();
        options.GetType().GetProperty("LienNumberCollision").Should().BeNull();
    }

    [Theory]
    [InlineData("2", false, "--legacy-program must be 1*")]
    [InlineData("1", true, "--apply is disabled*")]
    public void Update_history_options_reject_invalid_program_and_unverifiable_apply(
        string program,
        bool apply,
        string expectedMessage)
    {
        var arguments = new List<string>
        {
            "--import-update-logs",
            "--tenant-id", Guid.NewGuid().ToString(),
            "--org-id", Guid.NewGuid().ToString(),
            "--migration-user-id", Guid.NewGuid().ToString(),
            "--legacy-program", program,
            "--legacy-connection", "Server=legacy;Database=source;User Id=test;Password=test;",
            "--target-connection", "Server=target;Database=liens;User Id=test;Password=test;",
            "--source-fingerprint", ApprovedFingerprint,
        };
        if (apply)
            arguments.Add("--apply");

        var action = () => ParseOptions(arguments.ToArray());

        action.Should().Throw<TargetInvocationException>()
            .WithInnerException<ArgumentException>()
            .WithMessage(expectedMessage);
    }

    [Fact]
    public void Importer_requires_dedicated_provenance_and_validates_pacific_timestamp_anchors()
    {
        var importer = ReadImportFile("UpdateHistoryImport.cs");

        importer.Should().Contain("private const string ProvenanceKey = \"sl-core-update-history-v2\"");
        importer.Should().Contain("private const string TimestampSemantics = \"America/Los_Angeles-wall-clock\"");
        importer.Should().Contain("SOURCE_FINGERPRINT, IMPORT_SCOPE, TIMESTAMP_SEMANTICS");
        importer.Should().Contain("do not reuse the sl-core-current receipt");
        importer.Should().Contain("Expected 19 embedded UTC timestamp anchors");
        importer.Should().Contain("zone.IsInvalidTime(unspecified) || zone.IsAmbiguousTime(unspecified)");
        importer.Should().Contain("TimeZoneInfo.ConvertTimeToUtc(unspecified, zone)");
        importer.Should().Contain("SET time_zone = '+00:00'");
    }

    [Fact]
    public void Enabled_imported_history_fails_startup_when_schema_recovery_fails()
    {
        var program = ReadLiensApiFile("Program.cs");

        program.Should().Contain("GetValue<bool>(\"LegacyUpdateHistory:Enabled\")");
        program.Should().Contain("Legacy update-event schema recovery failed while imported history reads are enabled.");
        program.IndexOf("if (legacyUpdateHistoryEnabled && legacyUpdateEventSchemaRecoveryRequired)", StringComparison.Ordinal)
            .Should().BeLessThan(program.IndexOf("Legacy update-event schema recovery could not complete", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(2024, 7, 1, 10, 22, 8, 2024, 7, 1, 17, 22, 8)]
    [InlineData(2024, 1, 15, 10, 22, 8, 2024, 1, 15, 18, 22, 8)]
    public void Pacific_conversion_observes_daylight_and_standard_offsets(
        int year,
        int month,
        int day,
        int hour,
        int minute,
        int second,
        int expectedYear,
        int expectedMonth,
        int expectedDay,
        int expectedHour,
        int expectedMinute,
        int expectedSecond)
    {
        var wallClock = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Unspecified);

        var converted = InvokeImporter<DateTime>("ConvertPacificToUtc", wallClock);

        converted.Should().Be(new DateTime(
            expectedYear,
            expectedMonth,
            expectedDay,
            expectedHour,
            expectedMinute,
            expectedSecond,
            DateTimeKind.Utc));
    }

    [Theory]
    [InlineData(2024, 3, 10, 2, 30, 0)]
    [InlineData(2024, 11, 3, 1, 30, 0)]
    public void Pacific_conversion_rejects_invalid_and_ambiguous_wall_clock_times(
        int year,
        int month,
        int day,
        int hour,
        int minute,
        int second)
    {
        var wallClock = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Unspecified);

        var action = () => InvokeImporter<DateTime>("ConvertPacificToUtc", wallClock);

        action.Should().Throw<TargetInvocationException>()
            .WithInnerException<InvalidOperationException>()
            .WithMessage("*invalid or ambiguous*");
    }

    [Fact]
    public void Mapping_contract_derives_lien_case_accepts_blank_source_case_and_redacts_exceptions()
    {
        var importer = ReadImportFile("UpdateHistoryImport.cs");

        importer.Should().Contain("var suppliedCase = row.SuppliedCaseLegacyId?.Trim();");
        importer.Should().Contain("if (!string.IsNullOrEmpty(suppliedCase)");
        importer.Should().Contain("targetCaseId = targetLien.CaseId.Value;");
        importer.Should().Contain("targetLien.CaseId != targetCase.Id");
        importer.Should().Contain("private const string ApprovedMismatch = \"SL_LIENS_UPDATE_LOG:4891\"");
        importer.Should().Contain("SOURCE_CASE_LIEN_MISMATCH");
        importer.Should().Contain("MISSING_TARGET_CROSSWALK");
        importer.Should().Contain("Blocked:CrossTenantCrosswalk");
        importer.Should().Contain("Blocked:WrongTargetEntity");
        importer.Should().Contain("Blocked:ChangedSourceHash");
        importer.Should().Contain("'Legacy update event excluded by approved migration policy.'");
        importer.Should().NotContain("item.Source.Description ?? (object)DBNull.Value, @sourceHash");
        importer.Should().Contain("plan.OutOfScopeCount++");
    }

    [Fact]
    public void Retired_cli_apply_implementation_remains_atomic_but_is_unreachable()
    {
        var importer = ReadImportFile("UpdateHistoryImport.cs");

        importer.Should().Contain("HasIdenticalCompletedRunAsync");
        importer.Should().Contain("An identical completed update-history import already exists; no rows were written.");
        importer.Should().Contain("await target.BeginTransactionAsync()");
        importer.Should().Contain("plan.Events.Chunk(500)");
        importer.Should().Contain("await RevalidateApplyStateAsync");
        importer.Should().Contain("await RevalidateApplyBatchAsync");
        importer.Should().Contain("await InsertEventBatchAsync");
        importer.Should().Contain("await InsertCrosswalkBatchAsync");
        importer.Should().Contain("VALUES {values}");
        importer.Should().Contain("await ReconcileRunAsync");
        importer.Should().Contain("Status = 'Completed'");
        importer.Should().Contain("await transaction.CommitAsync()");
        importer.Should().Contain("await MarkRunFailedAsync");
        importer.Should().Contain("Status = 'Failed'");
        importer.Should().Contain("event/crosswalk cardinality, ownership, or source-hash reconciliation failed");
        importer.Should().Contain("update-history-v2:");
        importer.Should().Contain("item.Disposition is \"Insert\" or \"AlreadyImported\" ? \"Imported\" : item.Disposition");
        importer.Should().Contain("A matching completed run exists but imported event evidence is missing");
        importer.Should().Contain("HasCompleteRunEvidenceAsync");
        importer.Should().Contain("its event, crosswalk, or exception evidence is incomplete");
        importer.Should().Contain("expectedEventEvidence.Count != candidate.InsertedEventCount");
        importer.Should().Contain("actualEventEvidence.Order(StringComparer.Ordinal).SequenceEqual(");
        importer.Should().Contain("item.ImportRunId == candidate.Id");
        importer.Should().Contain("SELECT TenantId, SourceTable, LegacyId, Severity, ErrorCode, Message, SourceHash");
        importer.Should().Contain("actual.Order(StringComparer.Ordinal).SequenceEqual(expected.Order(StringComparer.Ordinal)");

        importer.IndexOf("CreateRunAsync", StringComparison.Ordinal)
            .Should().BeLessThan(importer.IndexOf("ApplyAsync", StringComparison.Ordinal));
    }

    [Fact]
    public void Existing_event_idempotency_binds_the_crosswalk_to_the_full_immutable_event_identity()
    {
        var importer = ReadImportFile("UpdateHistoryImport.cs");

        importer.Should().Contain("existing.TenantId != options.TenantId");
        importer.Should().Contain("existing.OrgId != options.OrgId");
        importer.Should().Contain("existing.CaseId != targetCaseId");
        importer.Should().Contain("existing.LienId != targetLienId");
        importer.Should().Contain("existing.Scope, row.Scope");
        importer.Should().Contain("existing.Action, row.Action");
        importer.Should().Contain("existing.Description, row.Description");
        importer.Should().Contain("existing.ActorDisplayName, row.Actor");
        importer.Should().Contain("existing.OccurredAtUtc != occurredAtUtc");
        importer.Should().Contain("existing.ImportedAtUtc != existing.RunStartedAtUtc");
        importer.Should().Contain("existing.SourceSystem, SourceSystem");
        importer.Should().Contain("existing.SourceTable, row.SourceTable");
        importer.Should().Contain("existing.LegacyId, row.Id.ToString");
        importer.Should().Contain("existing.LegacySequence != row.Id");
        importer.Should().Contain("existing.ImportRunId != eventCrosswalk.ImportRunId");
        importer.Should().Contain("SELECT StartedAtUtc FROM liens_LegacyImportRuns WHERE Id = @runId AND Status = 'Running'");
    }

    [Fact]
    public void Approved_dataset_totals_are_fingerprint_bound()
    {
        var importer = ReadImportFile("UpdateHistoryImport.cs");

        importer.Should().Contain("[\"Case Details Update\"] = 1502");
        importer.Should().Contain("[\"Case Created\"] = 1186");
        importer.Should().Contain("[\"Personal Info Update\"] = 68");
        importer.Should().Contain("[\"Create\"] = 11157");
        importer.Should().Contain("[\"Create Medical Payee\"] = 2587");
        importer.Should().Contain("[\"Update\"] = 1870");
        importer.Should().Contain("[\"Update Medical Code\"] = 303");
        importer.Should().Contain("[\"Update Medical Information\"] = 57");
        importer.Should().Contain("[\"Update Medical Payee\"] = 2");
        importer.Should().Contain("private const int ApprovedBlankLienCaseCount = 1280");
        importer.Should().Contain("string.IsNullOrWhiteSpace(row.SuppliedCaseLegacyId)");
        importer.Should().Contain("blank LU_CASE_ID count does not match the approved dataset");
    }

    [Fact]
    public void Stored_procedure_is_dry_run_first_approval_bound_atomic_and_idempotent()
    {
        var sql = ReadImportFile("import-program-1-update-history.sql");
        var compensation = ReadImportFile("compensate-program-1-update-history-import.sql");

        sql.Should().Contain("CREATE PROCEDURE liens_import_program_1_update_history");
        sql.Should().Contain("SQL SECURITY DEFINER");
        sql.Should().Contain(ApprovedFingerprint);
        sql.Should().Contain("'sl-core-update-history-v2'");
        sql.Should().Contain("table_schema = 'SL-CORE'");
        sql.Should().Contain("FROM `SL-CORE`.SL_CASE_UPDATE_LOG u");
        sql.Should().Contain("FROM `SL-CORE`.SL_LIENS_UPDATE_LOG u");
        sql.Should().Contain("source_lien.LM_ID = CAST(u.LU_LIEN_ID AS UNSIGNED)");
        sql.Should().NotContain("CAST(source_lien.LM_ID AS CHAR) = CAST(u.LU_LIEN_ID AS CHAR)");
        sql.Should().Contain("CREATE TEMPORARY TABLE tmp_luh_foreign_keys");
        sql.Should().Contain("PRIMARY KEY (SourceTable, LegacyId)");
        sql.Should().Contain("INSERT IGNORE INTO tmp_luh_foreign_keys");
        sql.Should().Contain("LEFT JOIN tmp_luh_foreign_keys foreign_parent");
        sql.Should().Contain("LEFT JOIN tmp_luh_foreign_keys foreign_event");
        sql.Should().Contain("DROP TEMPORARY TABLE IF EXISTS tmp_luh_foreign_keys");
        sql.Should().NotContain("FROM liens_LegacyIdCrosswalks foreign_walk");
        sql.Should().Contain("FROM `SL-CORE`.SL_CASE_NOTES");
        sql.Should().Contain("FROM `SL-CORE`.SL_MIGRATION_SOURCE_PROVENANCE");
        sql.Should().NotContain("FROM SL_CASE_UPDATE_LOG u");
        sql.Should().Contain("TIMESTAMP_SEMANTICS = 'America/Los_Angeles-wall-clock'");
        sql.Should().Contain("v_anchor_count <> 19 OR v_anchor_errors <> 0");
        sql.Should().Contain("PacificCandidateCount <> 1");
        sql.Should().Contain("LOWER(column_type) = 'datetime(6)'");
        sql.Should().Contain("GROUP_CONCAT(CONCAT(column_name");
        sql.Should().Contain("CONCAT(CHAR(92), CHAR(39)), CHAR(39)");
        sql.Should().Contain("Action = 'Personal Info Update'");
        sql.Should().Contain("v_case_events <> 2756 OR v_lien_events <> 15976 OR v_excluded <> 1");
        sql.Should().Contain("Status = 'Approved'");
        sql.Should().Contain("v_approval_binding_hash AS ApprovalBindingHash");
        sql.Should().Contain("v_approval_manifest_hash <> BINARY v_approval_binding_hash");
        sql.Should().Contain("BINARY MappingManifestHash = BINARY v_approval_binding_hash");
        sql.Should().Contain("BINARY MappingApprovalReference = BINARY v_approval_reference");
        sql.Should().Contain("ExpiresAtUtc IS NULL OR ExpiresAtUtc > UTC_TIMESTAMP(6)");
        sql.Should().Contain("ConsumedAtUtc IS NULL");
        sql.Should().Contain("Status = 'Consumed'");
        sql.Should().Contain("START TRANSACTION");
        sql.Should().Contain("ROLLBACK");
        sql.Should().Contain("INSERT INTO liens_LegacyUpdateEvents");
        sql.Should().Contain("INSERT INTO liens_LegacyIdCrosswalks");
        sql.Should().Contain("INSERT INTO liens_LegacyImportExceptions");
        sql.Should().Contain("Legacy update event excluded by approved migration policy.");
        sql.Should().Contain("matching completed run has incomplete event or exception evidence");
        sql.Should().Contain("v_existing_event_count <> v_existing_inserted_events");
        sql.Should().Contain("v_existing_crosswalk_count <> v_existing_inserted_events");
        sql.Should().Contain("v_existing_planned_count <> v_existing_inserted_events");
        sql.Should().Contain("v_existing_matching_exception_count <> v_excluded");
        sql.Should().Contain("v_existing_matching_exception_keys <> v_excluded");
        sql.Should().Contain("update_event.CaseId = plan.TargetCaseId");
        sql.Should().Contain("BINARY update_event.Description <=> BINARY plan.Description");
        sql.Should().Contain("update_event.ImportedAtUtc = v_run_started_at");
        sql.Should().Contain("'NO-OP' AS Mode");
        sql.Should().Contain("unfinished update-history run requires reconciliation");
        sql.Should().NotContain("--apply");

        sql.Should().Contain("CONCAT('legalsynq:luh:', v_tenant_id)");
        sql.Should().Contain("CONCAT('liens:slcore:', v_tenant_id)");
        sql.Should().Contain("parent_run.SourceFingerprint = BINARY v_source_fingerprint");
        sql.Should().Contain("canonical_case_walk.SourceTable = 'SL_CASE'");
        sql.Should().Contain("canonical_case_run.Status = 'Completed'");
        compensation.Should().Contain("CONCAT('legalsynq:luh:', LOWER(p_tenant_id))");

        sql.IndexOf("INSERT INTO liens_LegacyImportRuns", StringComparison.Ordinal)
            .Should().BeLessThan(sql.IndexOf("START TRANSACTION", StringComparison.Ordinal));
        sql.IndexOf("START TRANSACTION", StringComparison.Ordinal)
            .Should().BeLessThan(sql.IndexOf("INSERT INTO liens_LegacyUpdateEvents", StringComparison.Ordinal));
    }

    [Fact]
    public void Versioned_source_hash_is_deterministic_and_sensitive_to_raw_nullable_values()
    {
        var values = new string?[] { "case-1", null, "Update", "raw text", "actor", "2024-07-01 10:22:08.000000" };

        var first = InvokeImporter<string>(
            "VersionedRowHash",
            ApprovedFingerprint,
            "SL_CASE_UPDATE_LOG",
            123L,
            values);
        var repeated = InvokeImporter<string>(
            "VersionedRowHash",
            ApprovedFingerprint,
            "SL_CASE_UPDATE_LOG",
            123L,
            values);
        var blankInsteadOfNull = InvokeImporter<string>(
            "VersionedRowHash",
            ApprovedFingerprint,
            "SL_CASE_UPDATE_LOG",
            123L,
            new string?[] { "case-1", string.Empty, "Update", "raw text", "actor", "2024-07-01 10:22:08.000000" });

        first.Should().StartWith("update-history-v2:").And.HaveLength(82);
        repeated.Should().Be(first);
        blankInsteadOfNull.Should().NotBe(first);
    }

    [Fact]
    public void Compensation_is_run_bound_guarded_and_retains_operational_evidence()
    {
        var sql = ReadImportFile("compensate-program-1-update-history-import.sql");

        sql.Should().Contain("v_schema NOT IN ('LS_QA_LIENS', 'LS_LIENS')");
        sql.Should().Contain("p_confirm_reads_disabled <> 1 OR p_confirm_pre_exposure <> 1");
        sql.Should().Contain("sl-core-update-history-rollback-v1");
        sql.Should().Contain("v_approval_binding_hash AS ApprovalBindingHash");
        sql.Should().Contain("MappingManifestHash = BINARY v_approval_binding_hash");
        sql.Should().Contain("rollback approval was consumed concurrently");
        sql.Should().Contain("ExpiresAtUtc IS NULL OR ExpiresAtUtc > UTC_TIMESTAMP(6)");
        sql.Should().Contain("run.SourceFingerprint = BINARY p_expected_source_fingerprint");
        sql.Should().Contain("run.MappingVersion = BINARY p_expected_mapping_version");
        sql.Should().Contain("run.Status = 'Completed'");
        sql.Should().Contain("GET_LOCK(v_lock_name, 0)");
        sql.Should().Contain("START TRANSACTION");
        sql.Should().Contain("ROLLBACK");
        sql.Should().Contain("COMMIT");
        sql.Should().Contain("TargetEntity = 'LegacyUpdateEvent'");
        sql.Should().Contain("DELETE FROM liens_LegacyUpdateEvents");
        sql.Should().Contain("Status = 'RolledBack'");
        sql.Should().Contain("'exceptionsRetained', TRUE");
        sql.Should().NotContain("DELETE FROM liens_LegacyImportExceptions");
        sql.Should().Contain("compensation postcondition failed");

        sql.IndexOf("DELETE FROM liens_LegacyIdCrosswalks", StringComparison.Ordinal)
            .Should().BeLessThan(sql.IndexOf("DELETE FROM liens_LegacyUpdateEvents", StringComparison.Ordinal));
        sql.IndexOf("START TRANSACTION", StringComparison.Ordinal)
            .Should().BeLessThan(sql.IndexOf("DELETE FROM liens_LegacyIdCrosswalks", StringComparison.Ordinal));
    }

    private static string ReadImportFile(string filename) =>
        File.ReadAllText(Path.Combine(FindRepositoryRoot(), "scripts", "LegacyLiensImport", filename));

    private static string ReadLiensApiFile(string filename) =>
        File.ReadAllText(Path.Combine(FindRepositoryRoot(), "apps", "services", "liens", "Liens.Api", filename));

    private static T InvokeImporter<T>(string methodName, params object?[] arguments)
    {
        var assembly = Assembly.Load("LegacyLiensImport");
        var importer = assembly.GetType("UpdateHistoryImport", throwOnError: true)!;
        var method = importer.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMethodException(importer.FullName, methodName);
        return (T)method.Invoke(null, arguments)!;
    }

    private static object ParseOptions(params string[] arguments)
    {
        var assembly = Assembly.Load("LegacyLiensImport");
        var importer = assembly.GetType("UpdateHistoryImport", throwOnError: true)!;
        var options = importer.GetNestedType("Options", BindingFlags.NonPublic)
            ?? throw new TypeLoadException("UpdateHistoryImport.Options was not found.");
        var parse = options.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static)
            ?? throw new MissingMethodException(options.FullName, "Parse");
        return parse.Invoke(null, [arguments])!;
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
