using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MySqlConnector;

internal static class UpdateHistoryImport
{
    private const string SourceSystem = "SL-CORE";
    private const string CaseTable = "SL_CASE_UPDATE_LOG";
    private const string LienTable = "SL_LIENS_UPDATE_LOG";
    private const string MappingVersion = "sl-core-update-history-v2";
    private const string ProvenanceKey = "sl-core-update-history-v2";
    private const string TimestampSemantics = "America/Los_Angeles-wall-clock";
    private const string ApprovedFingerprint = "3adccecf8a38114a14cd500240aab2a4db3d9bf45f00945c659dc3b5252663fe";
    private const string ManifestCertificateSubject = "CN=LegalSynq Identity Migration Signing";
    private const string ApprovedMismatch = "SL_LIENS_UPDATE_LOG:4891";
    private const int ApprovedBlankLienCaseCount = 1280;
    private static readonly Regex UtcAnchorRegex = new(
        @"(?<instant>\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}) UTC",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Any(argument => argument is "--help" or "-h"))
        {
            WriteUsage();
            return 0;
        }

        try
        {
            var options = Options.Parse(args);
            var fingerprint = await ResolveFingerprintAsync(options);
            if (!string.Equals(fingerprint, ApprovedFingerprint, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The update-history runner is approved only for the frozen Program 1 dump fingerprint.");

            SignedManifest? manifest = null;

            await using var source = new MySqlConnection(options.LegacyConnectionString);
            await using var target = new MySqlConnection(options.TargetConnectionString);
            await source.OpenAsync();
            await target.OpenAsync();

            await SetUtcSessionAsync(source);
            await VerifySourceContractAsync(source, fingerprint);
            await VerifyTargetSchemaAsync(target);
            await VerifySingleCoreImportAsync(target, options, fingerprint);

            var rows = await LoadSourceRowsAsync(source, options.LegacyProgram, fingerprint);
            await ValidateUtcAnchorsAsync(source);
            var plan = await BuildPlanAsync(target, options, rows);

            Console.WriteLine($"Tenant: {options.TenantId}");
            Console.WriteLine($"Legacy program: {options.LegacyProgram}");
            Console.WriteLine($"Mode: {(options.Apply ? "APPLY" : "DRY RUN")}");
            Console.WriteLine($"Case events: {plan.CaseInserts} insert, {plan.CaseSkips} already imported");
            Console.WriteLine($"Lien events: {plan.LienInserts} insert, {plan.LienSkips} already imported");
            Console.WriteLine($"Excluded eligible events: {plan.Exceptions.Count}");
            Console.WriteLine($"Out-of-scope events: {plan.OutOfScopeCount}");
            Console.WriteLine($"Aggregate checksum: {plan.AggregateChecksum}");

            if (plan.Blockers.Count > 0)
            {
                Console.Error.WriteLine("Preflight failed; no update-history rows were written.");
                foreach (var blocker in plan.Blockers.Take(20))
                    Console.Error.WriteLine($"  - {blocker}");
                return 3;
            }

            ValidateApprovedDataset(plan);
            if (!options.Apply)
            {
                Console.WriteLine("Dry run passed. Sign a manifest containing these exact counts and checksum before applying.");
                return 0;
            }

            ValidateManifest(manifest!, plan);
            if (await HasIdenticalCompletedRunAsync(target, options, fingerprint, manifest!, plan))
            {
                if (plan.Events.Count != 0)
                    throw new InvalidOperationException(
                        "A matching completed run exists but imported event evidence is missing; disable reads and repair forward.");
                Console.WriteLine("An identical completed update-history import already exists; no rows were written.");
                return 0;
            }

            var runId = await CreateRunAsync(target, options, fingerprint, manifest!);
            try
            {
                await ApplyAsync(target, options, runId, plan);
                Console.WriteLine($"Update-history import completed. Run ID: {runId}");
                return 0;
            }
            catch (Exception exception)
            {
                await MarkRunFailedAsync(target, runId, exception.GetType().Name);
                throw;
            }
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"ERROR: {exception.Message}");
            return 1;
        }
    }

    private static void WriteUsage() => Console.WriteLine("""
LegacyLiensImport Program 1 update-history mode.

Required:
  --import-update-logs
  --tenant-id <guid>
  --org-id <guid>
  --migration-user-id <guid>
  --legacy-program 1
  --legacy-connection <connection>   (or LegacySlCoreConnectionString)
  --target-connection <connection>   (or ConnectionStrings__LiensDb)
  --source-dump <path>               dump file used to verify the source fingerprint,
                                     or use --source-fingerprint <sha256>

This CLI mode is preflight-only. Apply through import-program-1-update-history.sql,
which consumes a one-time database approval under the migration advisory locks.
""");

    private sealed record Options(
        Guid TenantId,
        Guid OrgId,
        Guid MigrationUserId,
        long LegacyProgram,
        string LegacyConnectionString,
        string TargetConnectionString,
        string? SourceDumpPath,
        string? SourceFingerprint,
        string? ManifestPath,
        string? ManifestSignaturePath,
        bool Apply)
    {
        public static Options Parse(string[] args)
        {
            var values = ParseArguments(args);
            var apply = values.ContainsKey("apply");
            if (apply)
                throw new ArgumentException(
                    "--apply is disabled for update-history CLI mode; apply through import-program-1-update-history.sql with a one-time database approval.");
            var program = RequireLong(values, "legacy-program");
            if (program != 1)
                throw new ArgumentException("--legacy-program must be 1 for update-history import.");

            var sourceDump = Optional(values, "source-dump");
            var sourceFingerprint = Optional(values, "source-fingerprint");
            if (string.IsNullOrWhiteSpace(sourceDump) && string.IsNullOrWhiteSpace(sourceFingerprint))
                throw new ArgumentException("Provide --source-dump or --source-fingerprint.");

            var manifest = Optional(values, "mapping-manifest");
            var signature = Optional(values, "mapping-manifest-signature");

            return new Options(
                RequireGuid(values, "tenant-id"),
                RequireGuid(values, "org-id"),
                RequireGuid(values, "migration-user-id"),
                program,
                Optional(values, "legacy-connection")
                    ?? Environment.GetEnvironmentVariable("LegacySlCoreConnectionString")
                    ?? throw new ArgumentException("Provide --legacy-connection or LegacySlCoreConnectionString."),
                Optional(values, "target-connection")
                    ?? Environment.GetEnvironmentVariable("ConnectionStrings__LiensDb")
                    ?? throw new ArgumentException("Provide --target-connection or ConnectionStrings__LiensDb."),
                sourceDump,
                sourceFingerprint,
                manifest,
                signature,
                apply);
        }

        private static Dictionary<string, string?> ParseArguments(string[] args)
        {
            var allowedFlags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "import-update-logs", "apply"
            };
            var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < args.Length; index++)
            {
                var argument = args[index];
                if (!argument.StartsWith("--", StringComparison.Ordinal))
                    throw new ArgumentException($"Unexpected argument '{argument}'.");
                var key = argument[2..];
                if (allowedFlags.Contains(key))
                {
                    result[key] = null;
                    continue;
                }
                if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
                    throw new ArgumentException($"Argument --{key} requires a value.");
                result[key] = args[++index];
            }
            return result;
        }

        private static string? Optional(IReadOnlyDictionary<string, string?> values, string key) =>
            values.TryGetValue(key, out var value) ? value : null;

        private static Guid RequireGuid(IReadOnlyDictionary<string, string?> values, string key) =>
            Guid.TryParse(Optional(values, key), out var value) && value != Guid.Empty
                ? value
                : throw new ArgumentException($"--{key} must be a non-empty GUID.");

        private static long RequireLong(IReadOnlyDictionary<string, string?> values, string key) =>
            long.TryParse(Optional(values, key), NumberStyles.None, CultureInfo.InvariantCulture, out var value)
                ? value
                : throw new ArgumentException($"--{key} must be an integer.");
    }

    private sealed record SignedManifest(
        Guid TenantId,
        Guid OrgId,
        Guid MigrationUserId,
        long LegacyProgram,
        int ExpectedImportedCaseCount,
        int ExpectedImportedLienCount,
        int ExpectedExcludedCount,
        string AggregateChecksum,
        string ApprovalReference,
        string Hash,
        IReadOnlySet<string> ApprovedAnomalies)
    {
        private sealed record Payload(
            string? TenantId,
            string? OrgId,
            string? MigrationUserId,
            long? LegacyProgram,
            string? ImportScope,
            string? MappingVersion,
            string? SourceFingerprint,
            int? ExpectedImportedCaseCount,
            int? ExpectedImportedLienCount,
            int? ExpectedExcludedCount,
            string? AggregateChecksum,
            string? ApprovalReference,
            string[]? ApprovedAnomalies);

        public static async Task<SignedManifest> LoadAsync(Options options, string fingerprint)
        {
            if (!File.Exists(options.ManifestPath) || !File.Exists(options.ManifestSignaturePath))
                throw new ArgumentException("The mapping manifest or signature file does not exist.");

            var bytes = await File.ReadAllBytesAsync(options.ManifestPath!);
            byte[] signature;
            try
            {
                signature = Convert.FromBase64String((await File.ReadAllTextAsync(options.ManifestSignaturePath!)).Trim());
            }
            catch (FormatException)
            {
                throw new ArgumentException("The mapping manifest signature must be Base64 encoded.");
            }

            using var certificate = GetSigningCertificate();
            using var rsa = certificate.GetRSAPublicKey()
                ?? throw new ArgumentException("The migration signing certificate has no RSA public key.");
            if (!rsa.VerifyData(bytes, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
                throw new ArgumentException("The mapping manifest signature is invalid.");

            Payload payload;
            try
            {
                payload = JsonSerializer.Deserialize<Payload>(bytes, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    ?? throw new ArgumentException("The mapping manifest is empty.");
            }
            catch (JsonException exception)
            {
                throw new ArgumentException("The mapping manifest is invalid JSON.", exception);
            }

            if (!Guid.TryParse(payload.TenantId, out var tenantId)
                || !Guid.TryParse(payload.OrgId, out var orgId)
                || !Guid.TryParse(payload.MigrationUserId, out var actorId)
                || payload.LegacyProgram is null
                || payload.ExpectedImportedCaseCount is null
                || payload.ExpectedImportedLienCount is null
                || payload.ExpectedExcludedCount is null
                || string.IsNullOrWhiteSpace(payload.AggregateChecksum)
                || string.IsNullOrWhiteSpace(payload.ApprovalReference))
                throw new ArgumentException("The signed manifest is missing required update-history approval fields.");

            if (tenantId != options.TenantId || orgId != options.OrgId || actorId != options.MigrationUserId
                || payload.LegacyProgram != options.LegacyProgram)
                throw new ArgumentException("The signed manifest does not match the requested tenant, organization, actor, and program.");
            if (!string.Equals(payload.ImportScope, MappingVersion, StringComparison.Ordinal)
                || !string.Equals(payload.MappingVersion, MappingVersion, StringComparison.Ordinal))
                throw new ArgumentException($"Manifest importScope and mappingVersion must both be '{MappingVersion}'.");
            if (!string.Equals(payload.SourceFingerprint, fingerprint, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("The signed manifest does not authorize this source dump fingerprint.");
            if (!IsSha256(payload.AggregateChecksum))
                throw new ArgumentException("Manifest aggregateChecksum must be a SHA-256 value.");

            return new SignedManifest(
                tenantId,
                orgId,
                actorId,
                payload.LegacyProgram.Value,
                payload.ExpectedImportedCaseCount.Value,
                payload.ExpectedImportedLienCount.Value,
                payload.ExpectedExcludedCount.Value,
                payload.AggregateChecksum.ToLowerInvariant(),
                Truncate(payload.ApprovalReference.Trim(), 200),
                Sha256(bytes),
                new HashSet<string>(payload.ApprovedAnomalies ?? [], StringComparer.Ordinal));
        }

        private static X509Certificate2 GetSigningCertificate()
        {
            using var store = new X509Store(StoreName.TrustedPeople, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadOnly);
            var matches = store.Certificates
                .Find(X509FindType.FindBySubjectDistinguishedName, ManifestCertificateSubject, false)
                .Where(certificate => certificate.NotBefore <= DateTime.Now && certificate.NotAfter >= DateTime.Now)
                .ToList();
            return matches.Count switch
            {
                1 => new X509Certificate2(matches[0]),
                0 => throw new ArgumentException($"Install the signing certificate '{ManifestCertificateSubject}' in LocalMachine\\TrustedPeople."),
                _ => throw new ArgumentException("More than one usable migration signing certificate was found.")
            };
        }
    }

    private sealed record SourceRow(
        string SourceTable,
        long Id,
        string ParentLegacyId,
        string? SuppliedCaseLegacyId,
        string CanonicalCaseLegacyId,
        string Action,
        string? Description,
        string? Actor,
        DateTime WallClock,
        bool Eligible,
        string OutOfScopeReason,
        string Scope,
        string SourceHash);

    private sealed record Crosswalk(Guid TargetId, string TargetEntity, string SourceHash, Guid ImportRunId);
    private sealed record TargetCase(Guid Id, Guid OrgId);
    private sealed record TargetLien(Guid Id, Guid OrgId, Guid? CaseId);
    private sealed record ExistingEvent(
        Guid Id,
        Guid TenantId,
        Guid OrgId,
        Guid CaseId,
        Guid? LienId,
        string Scope,
        string Action,
        string? Description,
        string? ActorDisplayName,
        DateTime OccurredAtUtc,
        DateTime ImportedAtUtc,
        string SourceSystem,
        string SourceTable,
        string LegacyId,
        long LegacySequence,
        DateTime RunStartedAtUtc,
        DateTime RunCompletedAtUtc,
        Guid ImportRunId);
    private sealed record CompletedRunCandidate(Guid Id, int InsertedEventCount);
    private sealed record ImportedEventEvidence(
        Guid EventId,
        Guid ImportRunId,
        string SourceTable,
        string LegacyId,
        string SourceHash);
    private sealed record EventPlan(SourceRow Source, Guid Id, Guid CaseId, Guid? LienId, DateTime OccurredAtUtc);
    private sealed record ImportExceptionPlan(SourceRow Source, string ErrorCode);

    private sealed class ImportPlan
    {
        public List<EventPlan> Events { get; } = [];
        public List<ImportedEventEvidence> ImportedEvidence { get; } = [];
        public List<ImportExceptionPlan> Exceptions { get; } = [];
        public List<string> Blockers { get; } = [];
        public List<(SourceRow Row, string Disposition)> Dispositions { get; } = [];
        public int OutOfScopeCount { get; set; }
        public int CaseInserts => Events.Count(item => item.Source.Scope == "Case");
        public int LienInserts => Events.Count(item => item.Source.Scope == "Lien");
        public int CaseSkips => Dispositions.Count(item => item.Row.Scope == "Case" && item.Disposition == "AlreadyImported");
        public int LienSkips => Dispositions.Count(item => item.Row.Scope == "Lien" && item.Disposition == "AlreadyImported");
        public string AggregateChecksum => ComputeAggregateChecksum(Dispositions);
    }

    private static async Task<List<SourceRow>> LoadSourceRowsAsync(MySqlConnection source, long program, string fingerprint)
    {
        var result = new List<SourceRow>();
        await LoadCaseRowsAsync(source, program, fingerprint, result);
        await LoadLienRowsAsync(source, program, fingerprint, result);
        return result;
    }

    private static async Task LoadCaseRowsAsync(MySqlConnection source, long program, string fingerprint, List<SourceRow> rows)
    {
        const string sql = """
SELECT u.CUL_ID, u.CUL_CASE_ID, u.CUL_LIEN_ID, u.CUL_ACTION, u.CUL_DESCRIPTION, u.CUL_UPDATED_BY,
       DATE_FORMAT(u.CUL_TIMESTAMP, '%Y-%m-%d %H:%i:%s.%f'),
       c.CASE_PROGRAM, c.CASE_IS_DELETED
FROM SL_CASE_UPDATE_LOG u
LEFT JOIN SL_CASE c ON CAST(c.CASE_ID AS CHAR) = u.CUL_CASE_ID
ORDER BY u.CUL_ID;
""";
        await using var command = new MySqlCommand(sql, source);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var id = reader.GetInt64(0);
            var caseId = Text(reader, 1) ?? string.Empty;
            var rawLienId = Text(reader, 2);
            var action = Text(reader, 3) ?? string.Empty;
            var description = Text(reader, 4);
            var actor = Text(reader, 5);
            var wallClockText = Text(reader, 6);
            var sourceProgram = reader.IsDBNull(7) ? (long?)null : Convert.ToInt64(reader.GetValue(7), CultureInfo.InvariantCulture);
            var deleted = Text(reader, 8);
            var eligible = sourceProgram == program && !IsDeleted(deleted);
            var reason = sourceProgram is null ? "MissingParent" : sourceProgram != program ? "OtherProgram" : IsDeleted(deleted) ? "DeletedParent" : string.Empty;
            var wallClock = ParseWallClock(wallClockText, CaseTable, id, eligible);
            var hash = VersionedRowHash(fingerprint, CaseTable, id, caseId, rawLienId, action, description, actor, wallClockText);
            rows.Add(new SourceRow(CaseTable, id, caseId, null, caseId, action, description, actor, wallClock, eligible, reason, "Case", hash));
        }
    }

    private static async Task LoadLienRowsAsync(MySqlConnection source, long program, string fingerprint, List<SourceRow> rows)
    {
        const string sql = """
SELECT u.LU_ID, u.LU_CASE_ID, u.LU_LIEN_ID, u.LU_ACTION, u.LU_DESCRIPTION, u.LU_UPDATED_BY,
       DATE_FORMAT(u.LU_TIMESTAMP, '%Y-%m-%d %H:%i:%s.%f'),
       lm.LM_CASE_ID, lm.LM_IS_DELETED, c.CASE_PROGRAM, c.CASE_IS_DELETED
FROM SL_LIENS_UPDATE_LOG u
LEFT JOIN SL_LEINS_MEDICAL lm ON CAST(lm.LM_ID AS CHAR) = u.LU_LIEN_ID
LEFT JOIN SL_CASE c ON c.CASE_ID = lm.LM_CASE_ID
ORDER BY u.LU_ID;
""";
        await using var command = new MySqlCommand(sql, source);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var id = reader.GetInt64(0);
            var suppliedCaseId = Text(reader, 1);
            var lienId = Text(reader, 2) ?? string.Empty;
            var action = Text(reader, 3) ?? string.Empty;
            var description = Text(reader, 4);
            var actor = Text(reader, 5);
            var wallClockText = Text(reader, 6);
            var canonicalCaseId = reader.IsDBNull(7) ? string.Empty : Convert.ToString(reader.GetValue(7), CultureInfo.InvariantCulture) ?? string.Empty;
            var lienDeleted = Text(reader, 8);
            var sourceProgram = reader.IsDBNull(9) ? (long?)null : Convert.ToInt64(reader.GetValue(9), CultureInfo.InvariantCulture);
            var caseDeleted = Text(reader, 10);
            var eligible = sourceProgram == program && !IsDeleted(lienDeleted) && !IsDeleted(caseDeleted);
            var reason = sourceProgram is null ? "MissingParent" : sourceProgram != program ? "OtherProgram"
                : IsDeleted(lienDeleted) || IsDeleted(caseDeleted) ? "DeletedParent" : string.Empty;
            var wallClock = ParseWallClock(wallClockText, LienTable, id, eligible);
            var hash = VersionedRowHash(fingerprint, LienTable, id, suppliedCaseId, lienId, action, description, actor, wallClockText);
            rows.Add(new SourceRow(LienTable, id, lienId, suppliedCaseId, canonicalCaseId, action, description, actor, wallClock, eligible, reason, "Lien", hash));
        }
    }

    private static async Task<ImportPlan> BuildPlanAsync(MySqlConnection target, Options options, IReadOnlyList<SourceRow> rows)
    {
        var crosswalks = await LoadCrosswalksAsync(target, options.TenantId);
        var foreignCrosswalkKeys = await LoadForeignCrosswalkKeysAsync(target, options.TenantId);
        var cases = await LoadTargetCasesAsync(target, options.TenantId);
        var liens = await LoadTargetLiensAsync(target, options.TenantId);
        var existingEvents = await LoadExistingEventsAsync(target, options);
        var plan = new ImportPlan();

        foreach (var row in rows)
        {
            if (!row.Eligible)
            {
                plan.OutOfScopeCount++;
                plan.Dispositions.Add((row, $"OutOfScope:{row.OutOfScopeReason}"));
                continue;
            }

            if (string.IsNullOrWhiteSpace(row.Action))
            {
                plan.Blockers.Add($"{row.SourceTable}:{row.Id} has no action.");
                plan.Dispositions.Add((row, "Blocked:MissingAction"));
                continue;
            }

            var sourceParentTable = row.Scope == "Case" ? "SL_CASE" : "SL_LEINS_MEDICAL";
            var parentKey = Key(sourceParentTable, row.ParentLegacyId);
            var eventKey = Key(row.SourceTable, row.Id.ToString(CultureInfo.InvariantCulture));
            if (foreignCrosswalkKeys.Contains(parentKey) || foreignCrosswalkKeys.Contains(eventKey))
            {
                plan.Blockers.Add($"{row.SourceTable}:{row.Id} source key is already bound to another tenant.");
                plan.Dispositions.Add((row, "Blocked:CrossTenantCrosswalk"));
                continue;
            }
            if (!crosswalks.TryGetValue(parentKey, out var parentCrosswalk))
            {
                plan.Exceptions.Add(new ImportExceptionPlan(row, "MISSING_TARGET_CROSSWALK"));
                plan.Dispositions.Add((row, "Excluded:MissingTargetCrosswalk"));
                continue;
            }

            if (!string.Equals(parentCrosswalk.TargetEntity, row.Scope, StringComparison.Ordinal))
            {
                plan.Blockers.Add($"{sourceParentTable}:{row.ParentLegacyId} crosswalk targets the wrong entity.");
                plan.Dispositions.Add((row, "Blocked:WrongTargetEntity"));
                continue;
            }
            if (string.IsNullOrWhiteSpace(parentCrosswalk.SourceHash))
            {
                plan.Blockers.Add($"{sourceParentTable}:{row.ParentLegacyId} crosswalk has no source hash.");
                plan.Dispositions.Add((row, "Blocked:MalformedParentCrosswalk"));
                continue;
            }

            Guid targetCaseId;
            Guid? targetLienId = null;
            if (row.Scope == "Case")
            {
                if (!cases.TryGetValue(parentCrosswalk.TargetId, out var targetCase) || targetCase.OrgId != options.OrgId)
                {
                    plan.Blockers.Add($"SL_CASE:{row.ParentLegacyId} target is missing or owned by another organization.");
                    plan.Dispositions.Add((row, "Blocked:InvalidTargetOwnership"));
                    continue;
                }
                targetCaseId = targetCase.Id;
            }
            else
            {
                if (!liens.TryGetValue(parentCrosswalk.TargetId, out var targetLien)
                    || targetLien.OrgId != options.OrgId || targetLien.CaseId is null)
                {
                    plan.Blockers.Add($"SL_LEINS_MEDICAL:{row.ParentLegacyId} target is missing, unlinked, or owned by another organization.");
                    plan.Dispositions.Add((row, "Blocked:InvalidTargetOwnership"));
                    continue;
                }

                var suppliedCase = row.SuppliedCaseLegacyId?.Trim();
                if (!string.IsNullOrEmpty(suppliedCase)
                    && !string.Equals(suppliedCase, row.CanonicalCaseLegacyId, StringComparison.Ordinal))
                {
                    var anomalyKey = $"{row.SourceTable}:{row.Id}";
                    if (anomalyKey == ApprovedMismatch)
                    {
                        plan.Exceptions.Add(new ImportExceptionPlan(row, "SOURCE_CASE_LIEN_MISMATCH"));
                        plan.Dispositions.Add((row, "Excluded:ApprovedCaseLienMismatch"));
                    }
                    else
                    {
                        plan.Blockers.Add($"{anomalyKey} has an unapproved case/lien mismatch.");
                        plan.Dispositions.Add((row, "Blocked:CaseLienMismatch"));
                    }
                    continue;
                }

                var canonicalCaseKey = Key("SL_CASE", row.CanonicalCaseLegacyId);
                if (foreignCrosswalkKeys.Contains(canonicalCaseKey)
                    || !crosswalks.TryGetValue(canonicalCaseKey, out var caseCrosswalk)
                    || !string.Equals(caseCrosswalk.TargetEntity, "Case", StringComparison.Ordinal)
                    || string.IsNullOrWhiteSpace(caseCrosswalk.SourceHash)
                    || !cases.TryGetValue(caseCrosswalk.TargetId, out var targetCase)
                    || targetCase.OrgId != options.OrgId
                    || targetLien.CaseId != targetCase.Id)
                {
                    plan.Blockers.Add($"{row.SourceTable}:{row.Id} canonical case mapping does not match the target lien.");
                    plan.Dispositions.Add((row, "Blocked:CanonicalCaseMismatch"));
                    continue;
                }

                targetCaseId = targetLien.CaseId.Value;
                targetLienId = targetLien.Id;
            }

            var occurredAtUtc = ConvertPacificToUtc(row.WallClock);
            if (crosswalks.TryGetValue(eventKey, out var eventCrosswalk))
            {
                if (!string.Equals(eventCrosswalk.TargetEntity, "LegacyUpdateEvent", StringComparison.Ordinal)
                    || !existingEvents.TryGetValue(eventCrosswalk.TargetId, out var existing)
                    || existing.TenantId != options.TenantId
                    || existing.OrgId != options.OrgId
                    || existing.CaseId != targetCaseId
                    || existing.LienId != targetLienId
                    || !string.Equals(existing.Scope, row.Scope, StringComparison.Ordinal)
                    || !string.Equals(existing.Action, row.Action, StringComparison.Ordinal)
                    || !string.Equals(existing.Description, row.Description, StringComparison.Ordinal)
                    || !string.Equals(existing.ActorDisplayName, row.Actor, StringComparison.Ordinal)
                    || existing.OccurredAtUtc != occurredAtUtc
                    || existing.ImportedAtUtc != existing.RunStartedAtUtc
                    || !string.Equals(existing.SourceSystem, SourceSystem, StringComparison.Ordinal)
                    || !string.Equals(existing.SourceTable, row.SourceTable, StringComparison.Ordinal)
                    || !string.Equals(existing.LegacyId, row.Id.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
                    || existing.LegacySequence != row.Id
                    || existing.ImportRunId != eventCrosswalk.ImportRunId)
                {
                    plan.Blockers.Add($"{row.SourceTable}:{row.Id} has an invalid existing update-event crosswalk.");
                    plan.Dispositions.Add((row, "Blocked:InvalidEventCrosswalk"));
                    continue;
                }
                if (!string.Equals(eventCrosswalk.SourceHash, row.SourceHash, StringComparison.Ordinal))
                {
                    plan.Blockers.Add($"{row.SourceTable}:{row.Id} source hash changed after import.");
                    plan.Dispositions.Add((row, "Blocked:ChangedSourceHash"));
                    continue;
                }
                plan.ImportedEvidence.Add(new ImportedEventEvidence(
                    existing.Id,
                    existing.ImportRunId,
                    row.SourceTable,
                    row.Id.ToString(CultureInfo.InvariantCulture),
                    eventCrosswalk.SourceHash));
                plan.Dispositions.Add((row, "AlreadyImported"));
                continue;
            }

            plan.Events.Add(new EventPlan(row, Guid.CreateVersion7(), targetCaseId, targetLienId, occurredAtUtc));
            plan.Dispositions.Add((row, "Insert"));
        }

        return plan;
    }

    private static void ValidateApprovedDataset(ImportPlan plan)
    {
        var eligibleSourceRows = plan.Dispositions
            .Select(item => item.Row)
            .Where(row => row.Eligible && $"{row.SourceTable}:{row.Id}" != ApprovedMismatch)
            .DistinctBy(row => (row.SourceTable, row.Id))
            .ToList();
        var caseActions = eligibleSourceRows
            .Where(row => row.Scope == "Case")
            .GroupBy(row => row.Action).ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var lienActions = eligibleSourceRows
            .Where(row => row.Scope == "Lien")
            .GroupBy(row => row.Action).ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var blankLienCaseCount = eligibleSourceRows.Count(row =>
            row.Scope == "Lien" && string.IsNullOrWhiteSpace(row.SuppliedCaseLegacyId));
        if (blankLienCaseCount != ApprovedBlankLienCaseCount)
            throw new InvalidOperationException("The fingerprint-bound blank LU_CASE_ID count does not match the approved dataset.");

        AssertActions(caseActions, new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["Case Details Update"] = 1502,
            ["Case Created"] = 1186,
            ["Personal Info Update"] = 68,
        }, "case");
        AssertActions(lienActions, new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["Create"] = 11157,
            ["Create Medical Payee"] = 2587,
            ["Update"] = 1870,
            ["Update Medical Code"] = 303,
            ["Update Medical Information"] = 57,
            ["Update Medical Payee"] = 2,
        }, "lien");
    }

    private static void AssertActions(IReadOnlyDictionary<string, int> actual, IReadOnlyDictionary<string, int> expected, string scope)
    {
        if (actual.Count != expected.Count || expected.Any(item => actual.GetValueOrDefault(item.Key) != item.Value))
            throw new InvalidOperationException($"The fingerprint-bound {scope} action totals do not match the approved dataset.");
    }

    private static void ValidateManifest(SignedManifest manifest, ImportPlan plan)
    {
        var totalCases = plan.CaseInserts + plan.CaseSkips;
        var totalLiens = plan.LienInserts + plan.LienSkips;
        if (manifest.ExpectedImportedCaseCount != totalCases
            || manifest.ExpectedImportedLienCount != totalLiens
            || manifest.ExpectedExcludedCount != plan.Exceptions.Count
            || !string.Equals(manifest.AggregateChecksum, plan.AggregateChecksum, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The signed manifest counts or checksum do not match this preflight.");

        var anomalies = plan.Exceptions
            .Where(item => item.ErrorCode == "SOURCE_CASE_LIEN_MISMATCH")
            .Select(item => $"{item.Source.SourceTable}:{item.Source.Id}")
            .ToHashSet(StringComparer.Ordinal);
        if (!anomalies.SetEquals(manifest.ApprovedAnomalies) || !anomalies.Contains(ApprovedMismatch))
            throw new InvalidOperationException("The signed approved-anomaly list does not exactly match preflight.");
    }

    private static async Task ApplyAsync(MySqlConnection target, Options options, Guid runId, ImportPlan plan)
    {
        await using var transaction = await target.BeginTransactionAsync();
        await RevalidateApplyStateAsync(target, transaction, options, plan.Events);

        foreach (var batch in plan.Events.Chunk(500))
        {
            await InsertEventBatchAsync(target, transaction, options, runId, batch);
            await InsertCrosswalkBatchAsync(target, transaction, options.TenantId, runId, batch);
        }

        foreach (var batch in plan.Exceptions.Chunk(500))
            await InsertExceptionBatchAsync(target, transaction, options.TenantId, runId, batch);

        await ReconcileRunAsync(target, transaction, options, runId, plan);

        var summary = JsonSerializer.Serialize(new
        {
            importScope = MappingVersion,
            caseEventsInserted = plan.CaseInserts,
            lienEventsInserted = plan.LienInserts,
            caseEventsAlreadyImported = plan.CaseSkips,
            lienEventsAlreadyImported = plan.LienSkips,
            excluded = plan.Exceptions.Count,
            outOfScope = plan.OutOfScopeCount,
            aggregateChecksum = plan.AggregateChecksum,
        });
        const string completeSql = """
UPDATE liens_LegacyImportRuns
SET Status = 'Completed', CompletedAtUtc = UTC_TIMESTAMP(6), SummaryJson = @summary, ErrorSummary = NULL
WHERE Id = @runId AND Status = 'Running';
""";
        await using (var command = new MySqlCommand(completeSql, target, transaction))
        {
            command.Parameters.AddWithValue("@runId", runId.ToString());
            command.Parameters.AddWithValue("@summary", summary);
            if (await command.ExecuteNonQueryAsync() != 1)
                throw new InvalidOperationException("Import run could not be completed atomically.");
        }
        await transaction.CommitAsync();
    }

    private static async Task RevalidateApplyStateAsync(
        MySqlConnection target,
        MySqlTransaction transaction,
        Options options,
        IReadOnlyList<EventPlan> events)
    {
        foreach (var batch in events.Chunk(500))
            await RevalidateApplyBatchAsync(target, transaction, options, batch);
    }

    private static async Task RevalidateApplyBatchAsync(
        MySqlConnection target,
        MySqlTransaction transaction,
        Options options,
        IReadOnlyList<EventPlan> batch)
    {
        if (batch.Count == 0)
            return;

        var sourceRows = new StringBuilder();
        await using var command = new MySqlCommand { Connection = target, Transaction = transaction };
        for (var index = 0; index < batch.Count; index++)
        {
            if (index > 0) sourceRows.AppendLine("UNION ALL");
            var cast = index == 0 ? "CAST" : string.Empty;
            var open = index == 0 ? "(" : string.Empty;
            var close36 = index == 0 ? " AS CHAR(36))" : string.Empty;
            var close20 = index == 0 ? " AS CHAR(20))" : string.Empty;
            var close100 = index == 0 ? " AS CHAR(100))" : string.Empty;
            sourceRows.Append("SELECT ")
                .Append(cast).Append(open).Append("@caseId").Append(index).Append(close36).Append(" AS CaseId, ")
                .Append(cast).Append(open).Append("@lienId").Append(index).Append(close36).Append(" AS LienId, ")
                .Append(cast).Append(open).Append("@scope").Append(index).Append(close20).Append(" AS Scope, ")
                .Append(cast).Append(open).Append("@parentTable").Append(index).Append(close100).Append(" AS ParentTable, ")
                .Append(cast).Append(open).Append("@parentLegacyId").Append(index).Append(close100).Append(" AS ParentLegacyId, ")
                .Append(cast).Append(open).Append("@parentTargetId").Append(index).Append(close36).Append(" AS ParentTargetId, ")
                .Append(cast).Append(open).Append("@canonicalCaseLegacyId").Append(index).Append(close100).Append(" AS CanonicalCaseLegacyId, ")
                .Append(cast).Append(open).Append("@sourceTable").Append(index).Append(close100).Append(" AS SourceTable, ")
                .Append(cast).Append(open).Append("@legacyId").Append(index).Append(close100).AppendLine(" AS LegacyId");

            var item = batch[index];
            command.Parameters.AddWithValue($"@caseId{index}", item.CaseId.ToString());
            command.Parameters.AddWithValue($"@lienId{index}", item.LienId?.ToString() ?? (object)DBNull.Value);
            command.Parameters.AddWithValue($"@scope{index}", item.Source.Scope);
            command.Parameters.AddWithValue($"@parentTable{index}", item.Source.Scope == "Case" ? "SL_CASE" : "SL_LEINS_MEDICAL");
            command.Parameters.AddWithValue($"@parentLegacyId{index}", item.Source.ParentLegacyId);
            command.Parameters.AddWithValue($"@parentTargetId{index}", (item.Source.Scope == "Case" ? item.CaseId : item.LienId!.Value).ToString());
            command.Parameters.AddWithValue($"@canonicalCaseLegacyId{index}", item.Source.CanonicalCaseLegacyId);
            command.Parameters.AddWithValue($"@sourceTable{index}", item.Source.SourceTable);
            command.Parameters.AddWithValue($"@legacyId{index}", item.Source.Id.ToString(CultureInfo.InvariantCulture));
        }

        command.CommandText = $"""
WITH source_rows AS (
{sourceRows}
)
SELECT COUNT(*)
FROM source_rows s
WHERE NOT EXISTS (
        SELECT 1 FROM liens_Cases c
        WHERE c.Id = s.CaseId AND c.TenantId = @tenantId AND c.OrgId = @orgId)
   OR (s.LienId IS NOT NULL AND NOT EXISTS (
        SELECT 1 FROM liens_Liens l
        WHERE l.Id = s.LienId AND l.CaseId = s.CaseId
          AND l.TenantId = @tenantId AND l.OrgId = @orgId))
   OR NOT EXISTS (
        SELECT 1
        FROM liens_LegacyIdCrosswalks x
        INNER JOIN liens_LegacyImportRuns r ON r.Id = x.ImportRunId
        WHERE x.TenantId = @tenantId AND x.SourceSystem = @sourceSystem
          AND x.SourceTable = s.ParentTable AND x.LegacyId = s.ParentLegacyId
          AND x.TargetEntity = s.Scope AND x.TargetId = s.ParentTargetId
          AND NULLIF(x.SourceHash, '') IS NOT NULL
          AND r.TenantId = @tenantId AND r.OrgId = @orgId
          AND r.SourceSystem = @sourceSystem AND r.LegacyProgram = '1'
          AND r.Status = 'Completed' AND r.SourceFingerprint = @fingerprint
          AND r.MappingVersion <> @updateMappingVersion)
   OR EXISTS (
        SELECT 1 FROM liens_LegacyIdCrosswalks x
        WHERE x.SourceSystem = @sourceSystem AND x.SourceTable = s.SourceTable
          AND x.LegacyId = s.LegacyId)
   OR EXISTS (
        SELECT 1 FROM liens_LegacyIdCrosswalks x
        WHERE x.TenantId <> @tenantId AND x.SourceSystem = @sourceSystem
          AND x.SourceTable = s.ParentTable AND x.LegacyId = s.ParentLegacyId)
   OR (s.Scope = 'Lien' AND NOT EXISTS (
        SELECT 1
        FROM liens_LegacyIdCrosswalks x
        INNER JOIN liens_LegacyImportRuns r ON r.Id = x.ImportRunId
        WHERE x.TenantId = @tenantId AND x.SourceSystem = @sourceSystem
          AND x.SourceTable = 'SL_CASE' AND x.LegacyId = s.CanonicalCaseLegacyId
          AND x.TargetEntity = 'Case' AND x.TargetId = s.CaseId
          AND NULLIF(x.SourceHash, '') IS NOT NULL
          AND r.TenantId = @tenantId AND r.OrgId = @orgId
          AND r.SourceSystem = @sourceSystem AND r.LegacyProgram = '1'
          AND r.Status = 'Completed' AND r.SourceFingerprint = @fingerprint
          AND r.MappingVersion <> @updateMappingVersion));
""";
        command.Parameters.AddWithValue("@tenantId", options.TenantId.ToString());
        command.Parameters.AddWithValue("@orgId", options.OrgId.ToString());
        command.Parameters.AddWithValue("@sourceSystem", SourceSystem);
        command.Parameters.AddWithValue("@fingerprint", ApprovedFingerprint);
        command.Parameters.AddWithValue("@updateMappingVersion", MappingVersion);
        if (Convert.ToInt32(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture) != 0)
            throw new InvalidOperationException("Target ownership or source crosswalk state changed after preflight.");
    }

    private static async Task InsertEventBatchAsync(
        MySqlConnection target,
        MySqlTransaction transaction,
        Options options,
        Guid runId,
        IReadOnlyList<EventPlan> batch)
    {
        var values = new StringBuilder();
        await using var command = new MySqlCommand { Connection = target, Transaction = transaction };
        for (var index = 0; index < batch.Count; index++)
        {
            if (index > 0) values.Append(',');
            values.Append($"(@id{index},@tenantId,@orgId,@caseId{index},@lienId{index},@scope{index},@action{index},@description{index},@actor{index},@occurredAt{index},(SELECT StartedAtUtc FROM liens_LegacyImportRuns WHERE Id = @runId AND Status = 'Running'),@runId,@sourceSystem,@sourceTable{index},@legacyId{index},@legacySequence{index})");
            var item = batch[index];
            command.Parameters.AddWithValue($"@id{index}", item.Id.ToString());
            command.Parameters.AddWithValue($"@caseId{index}", item.CaseId.ToString());
            command.Parameters.AddWithValue($"@lienId{index}", item.LienId?.ToString() ?? (object)DBNull.Value);
            command.Parameters.AddWithValue($"@scope{index}", item.Source.Scope);
            command.Parameters.AddWithValue($"@action{index}", item.Source.Action);
            command.Parameters.AddWithValue($"@description{index}", item.Source.Description ?? (object)DBNull.Value);
            command.Parameters.AddWithValue($"@actor{index}", item.Source.Actor ?? (object)DBNull.Value);
            command.Parameters.AddWithValue($"@occurredAt{index}", item.OccurredAtUtc);
            command.Parameters.AddWithValue($"@sourceTable{index}", item.Source.SourceTable);
            command.Parameters.AddWithValue($"@legacyId{index}", item.Source.Id.ToString(CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue($"@legacySequence{index}", item.Source.Id);
        }
        command.CommandText = $"""
INSERT INTO liens_LegacyUpdateEvents
    (Id, TenantId, OrgId, CaseId, LienId, Scope, Action, Description, ActorDisplayName,
     OccurredAtUtc, ImportedAtUtc, ImportRunId, SourceSystem, SourceTable, LegacyId, LegacySequence)
VALUES {values};
""";
        command.Parameters.AddWithValue("@tenantId", options.TenantId.ToString());
        command.Parameters.AddWithValue("@orgId", options.OrgId.ToString());
        command.Parameters.AddWithValue("@runId", runId.ToString());
        command.Parameters.AddWithValue("@sourceSystem", SourceSystem);
        if (await command.ExecuteNonQueryAsync() != batch.Count)
            throw new InvalidOperationException("Update-event batch insert count did not match preflight.");
    }

    private static async Task InsertCrosswalkBatchAsync(
        MySqlConnection target,
        MySqlTransaction transaction,
        Guid tenantId,
        Guid runId,
        IReadOnlyList<EventPlan> batch)
    {
        var values = new StringBuilder();
        await using var command = new MySqlCommand { Connection = target, Transaction = transaction };
        for (var index = 0; index < batch.Count; index++)
        {
            if (index > 0) values.Append(',');
            values.Append($"(@id{index},@tenantId,@sourceSystem,@sourceTable{index},@legacyId{index},'LegacyUpdateEvent',@targetId{index},@sourceHash{index},@runId,UTC_TIMESTAMP(6))");
            var item = batch[index];
            command.Parameters.AddWithValue($"@id{index}", Guid.CreateVersion7().ToString());
            command.Parameters.AddWithValue($"@sourceTable{index}", item.Source.SourceTable);
            command.Parameters.AddWithValue($"@legacyId{index}", item.Source.Id.ToString(CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue($"@targetId{index}", item.Id.ToString());
            command.Parameters.AddWithValue($"@sourceHash{index}", item.Source.SourceHash);
        }
        command.CommandText = $"""
INSERT INTO liens_LegacyIdCrosswalks
    (Id, TenantId, SourceSystem, SourceTable, LegacyId, TargetEntity, TargetId, SourceHash, ImportRunId, CreatedAtUtc)
VALUES {values};
""";
        command.Parameters.AddWithValue("@tenantId", tenantId.ToString());
        command.Parameters.AddWithValue("@sourceSystem", SourceSystem);
        command.Parameters.AddWithValue("@runId", runId.ToString());
        if (await command.ExecuteNonQueryAsync() != batch.Count)
            throw new InvalidOperationException("Update-event crosswalk batch insert count did not match preflight.");
    }

    private static async Task InsertExceptionBatchAsync(
        MySqlConnection target,
        MySqlTransaction transaction,
        Guid tenantId,
        Guid runId,
        IReadOnlyList<ImportExceptionPlan> batch)
    {
        var values = new StringBuilder();
        await using var command = new MySqlCommand { Connection = target, Transaction = transaction };
        for (var index = 0; index < batch.Count; index++)
        {
            if (index > 0) values.Append(',');
            values.Append($"(@id{index},@tenantId,@runId,@sourceTable{index},@legacyId{index},'Warning',@errorCode{index},'Legacy update event excluded by approved migration policy.',@sourceHash{index},UTC_TIMESTAMP(6))");
            var item = batch[index];
            command.Parameters.AddWithValue($"@id{index}", Guid.CreateVersion7().ToString());
            command.Parameters.AddWithValue($"@sourceTable{index}", item.Source.SourceTable);
            command.Parameters.AddWithValue($"@legacyId{index}", item.Source.Id.ToString(CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue($"@errorCode{index}", item.ErrorCode);
            command.Parameters.AddWithValue($"@sourceHash{index}", item.Source.SourceHash);
        }
        command.CommandText = $"""
INSERT INTO liens_LegacyImportExceptions
    (Id, TenantId, ImportRunId, SourceTable, LegacyId, Severity, ErrorCode, Message, SourceHash, CreatedAtUtc)
VALUES {values};
""";
        command.Parameters.AddWithValue("@tenantId", tenantId.ToString());
        command.Parameters.AddWithValue("@runId", runId.ToString());
        if (await command.ExecuteNonQueryAsync() != batch.Count)
            throw new InvalidOperationException("Update-history exception batch insert count did not match preflight.");
    }

    private static async Task ReconcileRunAsync(
        MySqlConnection target,
        MySqlTransaction transaction,
        Options options,
        Guid runId,
        ImportPlan plan)
    {
        const string eventSql = """
SELECT e.Id, e.SourceTable, e.LegacyId, e.CaseId, e.LienId, x.SourceHash
FROM liens_LegacyUpdateEvents e
INNER JOIN liens_LegacyIdCrosswalks x
  ON x.TargetId = e.Id AND x.ImportRunId = e.ImportRunId AND x.TargetEntity = 'LegacyUpdateEvent'
WHERE e.ImportRunId = @runId AND e.TenantId = @tenantId AND e.OrgId = @orgId
  AND x.TenantId = @tenantId AND x.SourceSystem = @sourceSystem;
""";
        await using var eventCommand = new MySqlCommand(eventSql, target, transaction);
        eventCommand.Parameters.AddWithValue("@runId", runId.ToString());
        eventCommand.Parameters.AddWithValue("@tenantId", options.TenantId.ToString());
        eventCommand.Parameters.AddWithValue("@orgId", options.OrgId.ToString());
        eventCommand.Parameters.AddWithValue("@sourceSystem", SourceSystem);
        await using var reader = await eventCommand.ExecuteReaderAsync();
        var actual = new Dictionary<string, (Guid Id, Guid CaseId, Guid? LienId, string Hash)>(StringComparer.Ordinal);
        while (await reader.ReadAsync())
        {
            var key = Key(reader.GetString(1), reader.GetString(2));
            actual.Add(key, (
                Guid.Parse(reader.GetString(0)),
                Guid.Parse(reader.GetString(3)),
                reader.IsDBNull(4) ? null : Guid.Parse(reader.GetString(4)),
                reader.GetString(5)));
        }
        await reader.DisposeAsync();

        if (actual.Count != plan.Events.Count || plan.Events.Any(item =>
        {
            var key = Key(item.Source.SourceTable, item.Source.Id.ToString(CultureInfo.InvariantCulture));
            return !actual.TryGetValue(key, out var row)
                || row.Id != item.Id || row.CaseId != item.CaseId || row.LienId != item.LienId
                || !string.Equals(row.Hash, item.Source.SourceHash, StringComparison.Ordinal);
        }))
            throw new InvalidOperationException("Run event/crosswalk cardinality, ownership, or source-hash reconciliation failed.");

        const string exceptionSql = """
SELECT SourceTable, LegacyId, ErrorCode, SourceHash
FROM liens_LegacyImportExceptions
WHERE ImportRunId = @runId AND TenantId = @tenantId;
""";
        await using var exceptionCommand = new MySqlCommand(exceptionSql, target, transaction);
        exceptionCommand.Parameters.AddWithValue("@runId", runId.ToString());
        exceptionCommand.Parameters.AddWithValue("@tenantId", options.TenantId.ToString());
        await using var exceptionReader = await exceptionCommand.ExecuteReaderAsync();
        var exceptionEvidence = new Dictionary<string, (string ErrorCode, string Hash)>(StringComparer.Ordinal);
        while (await exceptionReader.ReadAsync())
            exceptionEvidence.Add(
                Key(exceptionReader.GetString(0), exceptionReader.GetString(1)),
                (exceptionReader.GetString(2), exceptionReader.GetString(3)));
        await exceptionReader.DisposeAsync();
        if (exceptionEvidence.Count != plan.Exceptions.Count || plan.Exceptions.Any(item =>
        {
            var key = Key(item.Source.SourceTable, item.Source.Id.ToString(CultureInfo.InvariantCulture));
            return !exceptionEvidence.TryGetValue(key, out var evidence)
                || !string.Equals(evidence.ErrorCode, item.ErrorCode, StringComparison.Ordinal)
                || !string.Equals(evidence.Hash, item.Source.SourceHash, StringComparison.Ordinal);
        }))
            throw new InvalidOperationException("Run exception reconciliation failed.");

        const string ledgerSql = """
SELECT SourceTable, LegacyId, SourceHash
FROM liens_LegacyIdCrosswalks
WHERE TenantId = @tenantId AND SourceSystem = @sourceSystem
  AND SourceTable IN ('SL_CASE_UPDATE_LOG','SL_LIENS_UPDATE_LOG')
  AND TargetEntity = 'LegacyUpdateEvent';
""";
        await using var ledgerCommand = new MySqlCommand(ledgerSql, target, transaction);
        ledgerCommand.Parameters.AddWithValue("@tenantId", options.TenantId.ToString());
        ledgerCommand.Parameters.AddWithValue("@sourceSystem", SourceSystem);
        await using var ledgerReader = await ledgerCommand.ExecuteReaderAsync();
        var verifiedHashes = new Dictionary<string, string>(StringComparer.Ordinal);
        while (await ledgerReader.ReadAsync())
            verifiedHashes.Add(Key(ledgerReader.GetString(0), ledgerReader.GetString(1)), ledgerReader.GetString(2));
        await ledgerReader.DisposeAsync();
        foreach (var exception in exceptionEvidence)
            verifiedHashes[exception.Key] = exception.Value.Hash;

        foreach (var disposition in plan.Dispositions.Where(item => item.Row.Eligible))
        {
            var key = Key(disposition.Row.SourceTable, disposition.Row.Id.ToString(CultureInfo.InvariantCulture));
            if (!verifiedHashes.TryGetValue(key, out var hash)
                || !string.Equals(hash, disposition.Row.SourceHash, StringComparison.Ordinal))
                throw new InvalidOperationException("A persisted source hash does not match the approved preflight disposition.");
        }

        var reconciledChecksum = ComputeAggregateChecksum(plan.Dispositions, verifiedHashes);
        if (!string.Equals(reconciledChecksum, plan.AggregateChecksum, StringComparison.Ordinal))
            throw new InvalidOperationException("Run aggregate checksum reconciliation failed.");
    }

    private static string ComputeAggregateChecksum(
        IEnumerable<(SourceRow Row, string Disposition)> dispositions,
        IReadOnlyDictionary<string, string>? verifiedHashes = null)
    {
        var content = string.Concat(dispositions
            .OrderBy(item => item.Row.SourceTable, StringComparer.Ordinal)
            .ThenBy(item => item.Row.Id)
            .Select(item =>
            {
                var key = Key(item.Row.SourceTable, item.Row.Id.ToString(CultureInfo.InvariantCulture));
                var hash = verifiedHashes is not null && verifiedHashes.TryGetValue(key, out var verifiedHash)
                    ? verifiedHash
                    : item.Row.SourceHash;
                var disposition = item.Disposition is "Insert" or "AlreadyImported" ? "Imported" : item.Disposition;
                return $"{item.Row.SourceTable}|{item.Row.Id}|{hash}|{disposition}\n";
            }));
        return Sha256(Encoding.UTF8.GetBytes(content));
    }

    private static async Task<Dictionary<string, Crosswalk>> LoadCrosswalksAsync(MySqlConnection target, Guid tenantId)
    {
        const string sql = """
SELECT SourceTable, LegacyId, TargetId, TargetEntity, SourceHash, ImportRunId
FROM liens_LegacyIdCrosswalks
WHERE TenantId = @tenantId AND SourceSystem = @sourceSystem
  AND SourceTable IN ('SL_CASE','SL_LEINS_MEDICAL','SL_CASE_UPDATE_LOG','SL_LIENS_UPDATE_LOG');
""";
        await using var command = new MySqlCommand(sql, target);
        command.Parameters.AddWithValue("@tenantId", tenantId.ToString());
        command.Parameters.AddWithValue("@sourceSystem", SourceSystem);
        await using var reader = await command.ExecuteReaderAsync();
        var result = new Dictionary<string, Crosswalk>(StringComparer.Ordinal);
        while (await reader.ReadAsync())
        {
            if (!Guid.TryParse(reader.GetString(2), out var targetId) || !Guid.TryParse(reader.GetString(5), out var runId))
                throw new InvalidOperationException("A legacy crosswalk contains a malformed target or run ID.");
            result.Add(Key(reader.GetString(0), reader.GetString(1)), new Crosswalk(targetId, reader.GetString(3), reader.GetString(4), runId));
        }
        return result;
    }

    private static async Task<HashSet<string>> LoadForeignCrosswalkKeysAsync(MySqlConnection target, Guid tenantId)
    {
        const string sql = """
SELECT SourceTable, LegacyId
FROM liens_LegacyIdCrosswalks
WHERE TenantId <> @tenantId AND SourceSystem = @sourceSystem
  AND SourceTable IN ('SL_CASE','SL_LEINS_MEDICAL','SL_CASE_UPDATE_LOG','SL_LIENS_UPDATE_LOG');
""";
        await using var command = new MySqlCommand(sql, target);
        command.Parameters.AddWithValue("@tenantId", tenantId.ToString());
        command.Parameters.AddWithValue("@sourceSystem", SourceSystem);
        await using var reader = await command.ExecuteReaderAsync();
        var result = new HashSet<string>(StringComparer.Ordinal);
        while (await reader.ReadAsync())
            result.Add(Key(reader.GetString(0), reader.GetString(1)));
        return result;
    }

    private static async Task<Dictionary<Guid, TargetCase>> LoadTargetCasesAsync(MySqlConnection target, Guid tenantId)
    {
        await using var command = new MySqlCommand("SELECT Id, OrgId FROM liens_Cases WHERE TenantId = @tenantId;", target);
        command.Parameters.AddWithValue("@tenantId", tenantId.ToString());
        await using var reader = await command.ExecuteReaderAsync();
        var result = new Dictionary<Guid, TargetCase>();
        while (await reader.ReadAsync())
        {
            if (Guid.TryParse(reader.GetString(0), out var id) && Guid.TryParse(reader.GetString(1), out var orgId))
                result[id] = new TargetCase(id, orgId);
        }
        return result;
    }

    private static async Task<Dictionary<Guid, TargetLien>> LoadTargetLiensAsync(MySqlConnection target, Guid tenantId)
    {
        await using var command = new MySqlCommand("SELECT Id, OrgId, CaseId FROM liens_Liens WHERE TenantId = @tenantId;", target);
        command.Parameters.AddWithValue("@tenantId", tenantId.ToString());
        await using var reader = await command.ExecuteReaderAsync();
        var result = new Dictionary<Guid, TargetLien>();
        while (await reader.ReadAsync())
        {
            if (!Guid.TryParse(reader.GetString(0), out var id) || !Guid.TryParse(reader.GetString(1), out var orgId))
                continue;
            var caseId = reader.IsDBNull(2) ? (Guid?)null : Guid.Parse(reader.GetString(2));
            result[id] = new TargetLien(id, orgId, caseId);
        }
        return result;
    }

    private static async Task<Dictionary<Guid, ExistingEvent>> LoadExistingEventsAsync(MySqlConnection target, Options options)
    {
        const string sql = """
SELECT e.Id, e.TenantId, e.OrgId, e.CaseId, e.LienId, e.Scope,
       e.Action, e.Description, e.ActorDisplayName, e.OccurredAtUtc, e.ImportedAtUtc,
       e.SourceSystem, e.SourceTable, e.LegacyId, e.LegacySequence,
       r.StartedAtUtc, r.CompletedAtUtc, e.ImportRunId
FROM liens_LegacyUpdateEvents e
INNER JOIN liens_LegacyImportRuns r ON r.Id = e.ImportRunId
WHERE e.TenantId = @tenantId AND r.TenantId = @tenantId AND r.OrgId = @orgId
  AND r.SourceSystem = @sourceSystem AND r.SourceFingerprint = @fingerprint
  AND r.LegacyProgram = '1' AND r.MappingVersion = @mappingVersion
  AND r.Status = 'Completed' AND r.CompletedAtUtc IS NOT NULL;
""";
        await using var command = new MySqlCommand(sql, target);
        command.Parameters.AddWithValue("@tenantId", options.TenantId.ToString());
        command.Parameters.AddWithValue("@orgId", options.OrgId.ToString());
        command.Parameters.AddWithValue("@sourceSystem", SourceSystem);
        command.Parameters.AddWithValue("@fingerprint", ApprovedFingerprint);
        command.Parameters.AddWithValue("@mappingVersion", MappingVersion);
        await using var reader = await command.ExecuteReaderAsync();
        var result = new Dictionary<Guid, ExistingEvent>();
        while (await reader.ReadAsync())
        {
            var id = Guid.Parse(reader.GetString(0));
            result.Add(id, new ExistingEvent(
                id,
                Guid.Parse(reader.GetString(1)),
                Guid.Parse(reader.GetString(2)),
                Guid.Parse(reader.GetString(3)),
                reader.IsDBNull(4) ? null : Guid.Parse(reader.GetString(4)),
                reader.GetString(5),
                reader.GetString(6),
                Text(reader, 7),
                Text(reader, 8),
                reader.GetDateTime(9),
                reader.GetDateTime(10),
                reader.GetString(11),
                reader.GetString(12),
                reader.GetString(13),
                reader.GetInt64(14),
                reader.GetDateTime(15),
                reader.GetDateTime(16),
                Guid.Parse(reader.GetString(17))));
        }
        return result;
    }

    private static async Task VerifySingleCoreImportAsync(MySqlConnection target, Options options, string fingerprint)
    {
        const string sql = """
SELECT COUNT(DISTINCT x.ImportRunId),
       COUNT(DISTINCT CASE
         WHEN r.Status = 'Completed' AND r.SourceSystem = @sourceSystem
              AND r.SourceFingerprint = @fingerprint
              AND r.MappingVersion <> @updateMappingVersion
         THEN x.ImportRunId END)
FROM liens_LegacyIdCrosswalks x
INNER JOIN liens_LegacyImportRuns r ON r.Id = x.ImportRunId
WHERE x.TenantId = @tenantId AND x.SourceSystem = @sourceSystem
  AND x.SourceTable IN ('SL_CASE','SL_LEINS_MEDICAL')
  AND r.TenantId = @tenantId AND r.OrgId = @orgId AND r.LegacyProgram = '1';
""";
        await using var command = new MySqlCommand(sql, target);
        command.Parameters.AddWithValue("@tenantId", options.TenantId.ToString());
        command.Parameters.AddWithValue("@orgId", options.OrgId.ToString());
        command.Parameters.AddWithValue("@sourceSystem", SourceSystem);
        command.Parameters.AddWithValue("@fingerprint", fingerprint);
        command.Parameters.AddWithValue("@updateMappingVersion", MappingVersion);
        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();
        var totalRuns = reader.GetInt32(0);
        var compatibleRuns = reader.GetInt32(1);
        if (totalRuns != 1 || compatibleRuns != 1)
            throw new InvalidOperationException("Exactly one compatible completed Program 1 core import is required before update-history import.");
    }

    private static async Task VerifyTargetSchemaAsync(MySqlConnection target)
    {
        const string sql = """
SELECT COUNT(*) FROM information_schema.tables
WHERE table_schema = DATABASE()
  AND table_name IN ('liens_LegacyUpdateEvents','liens_LegacyImportRuns','liens_LegacyIdCrosswalks','liens_LegacyImportExceptions');
""";
        await using var command = new MySqlCommand(sql, target);
        if (Convert.ToInt32(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture) != 4)
            throw new InvalidOperationException("Required update-history and legacy control-plane tables are missing.");
    }

    private static async Task VerifySourceContractAsync(MySqlConnection source, string fingerprint)
    {
        const string columnSql = """
SELECT COUNT(*) FROM information_schema.columns
WHERE table_schema = DATABASE() AND (
 (table_name = 'SL_CASE_UPDATE_LOG' AND column_name IN ('CUL_ID','CUL_CASE_ID','CUL_LIEN_ID','CUL_ACTION','CUL_DESCRIPTION','CUL_UPDATED_BY','CUL_TIMESTAMP')) OR
 (table_name = 'SL_LIENS_UPDATE_LOG' AND column_name IN ('LU_ID','LU_CASE_ID','LU_LIEN_ID','LU_ACTION','LU_DESCRIPTION','LU_UPDATED_BY','LU_TIMESTAMP')));
""";
        await using (var command = new MySqlCommand(columnSql, source))
        {
            if (Convert.ToInt32(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture) != 14)
                throw new InvalidOperationException("The legacy update-log source column contract is incomplete.");
        }

        const string receiptSql = """
SELECT SOURCE_FINGERPRINT, IMPORT_SCOPE, TIMESTAMP_SEMANTICS
FROM SL_MIGRATION_SOURCE_PROVENANCE
WHERE PROVENANCE_KEY = @key;
""";
        try
        {
            await using var command = new MySqlCommand(receiptSql, source);
            command.Parameters.AddWithValue("@key", ProvenanceKey);
            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                throw new InvalidOperationException("The dedicated update-history controlled-restore receipt is missing.");
            if (!string.Equals(Text(reader, 0), fingerprint, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(Text(reader, 1), MappingVersion, StringComparison.Ordinal)
                || !string.Equals(Text(reader, 2), TimestampSemantics, StringComparison.Ordinal))
                throw new InvalidOperationException("The update-history restore receipt does not match fingerprint, scope, and timestamp semantics.");
            if (await reader.ReadAsync())
                throw new InvalidOperationException("More than one update-history restore receipt exists.");
        }
        catch (MySqlException exception) when (exception.Number is 1054 or 1146)
        {
            throw new InvalidOperationException("The dedicated provenance table/column contract is missing; do not reuse the sl-core-current receipt.");
        }
    }

    private static async Task ValidateUtcAnchorsAsync(MySqlConnection source)
    {
        const string sql = """
SELECT CN_ID, CN_NOTE, DATE_FORMAT(CN_CREATED, '%Y-%m-%d %H:%i:%s.%f')
FROM SL_CASE_NOTES
WHERE CN_NOTE REGEXP '[0-9]{4}-[0-9]{2}-[0-9]{2} [0-9]{2}:[0-9]{2}:[0-9]{2} UTC'
ORDER BY CN_ID;
""";
        await using var command = new MySqlCommand(sql, source);
        await using var reader = await command.ExecuteReaderAsync();
        var anchors = 0;
        while (await reader.ReadAsync())
        {
            var description = Text(reader, 1) ?? string.Empty;
            var match = UtcAnchorRegex.Match(description);
            if (!match.Success)
                continue;
            var expectedUtc = DateTime.SpecifyKind(
                DateTime.ParseExact(match.Groups["instant"].Value, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                DateTimeKind.Utc);
            var wallClock = ParseWallClock(Text(reader, 2), "SL_CASE_NOTES", reader.GetInt64(0), true);
            if (ConvertPacificToUtc(wallClock) != expectedUtc)
                throw new InvalidOperationException("The embedded UTC anchor validation disproves the declared Pacific wall-clock semantics.");
            anchors++;
        }
        if (anchors != 19)
            throw new InvalidOperationException($"Expected 19 embedded UTC timestamp anchors but validated {anchors}.");
    }

    private static async Task<Guid> CreateRunAsync(MySqlConnection target, Options options, string fingerprint, SignedManifest manifest)
    {
        var id = Guid.CreateVersion7();
        const string sql = """
INSERT INTO liens_LegacyImportRuns
    (Id, TenantId, OrgId, SourceSystem, SourceFingerprint, LegacyProgram, MappingVersion,
     MappingManifestHash, MappingApprovalReference, Status, StartedAtUtc, CreatedByUserId)
VALUES
    (@id, @tenantId, @orgId, @sourceSystem, @fingerprint, '1', @mappingVersion,
     @manifestHash, @approvalReference, 'Running', UTC_TIMESTAMP(6), @actorId);
""";
        await using var command = new MySqlCommand(sql, target);
        command.Parameters.AddWithValue("@id", id.ToString());
        command.Parameters.AddWithValue("@tenantId", options.TenantId.ToString());
        command.Parameters.AddWithValue("@orgId", options.OrgId.ToString());
        command.Parameters.AddWithValue("@sourceSystem", SourceSystem);
        command.Parameters.AddWithValue("@fingerprint", fingerprint);
        command.Parameters.AddWithValue("@mappingVersion", MappingVersion);
        command.Parameters.AddWithValue("@manifestHash", manifest.Hash);
        command.Parameters.AddWithValue("@approvalReference", manifest.ApprovalReference);
        command.Parameters.AddWithValue("@actorId", options.MigrationUserId.ToString());
        await command.ExecuteNonQueryAsync();
        return id;
    }

    private static async Task<bool> HasIdenticalCompletedRunAsync(
        MySqlConnection target,
        Options options,
        string fingerprint,
        SignedManifest manifest,
        ImportPlan plan)
    {
        const string sql = """
SELECT Id,
       CAST(JSON_UNQUOTE(JSON_EXTRACT(SummaryJson, '$.caseEventsInserted')) AS UNSIGNED)
       + CAST(JSON_UNQUOTE(JSON_EXTRACT(SummaryJson, '$.lienEventsInserted')) AS UNSIGNED)
FROM liens_LegacyImportRuns
WHERE TenantId = @tenantId AND OrgId = @orgId AND SourceSystem = @sourceSystem
  AND SourceFingerprint = @fingerprint AND LegacyProgram = '1' AND MappingVersion = @mappingVersion
  AND Status = 'Completed'
  AND JSON_UNQUOTE(JSON_EXTRACT(SummaryJson, '$.aggregateChecksum')) = @checksum
  AND CAST(JSON_UNQUOTE(JSON_EXTRACT(SummaryJson, '$.caseEventsInserted')) AS UNSIGNED)
      + CAST(JSON_UNQUOTE(JSON_EXTRACT(SummaryJson, '$.caseEventsAlreadyImported')) AS UNSIGNED) = @caseCount
  AND CAST(JSON_UNQUOTE(JSON_EXTRACT(SummaryJson, '$.lienEventsInserted')) AS UNSIGNED)
      + CAST(JSON_UNQUOTE(JSON_EXTRACT(SummaryJson, '$.lienEventsAlreadyImported')) AS UNSIGNED) = @lienCount
  AND CAST(JSON_UNQUOTE(JSON_EXTRACT(SummaryJson, '$.excluded')) AS UNSIGNED) = @excludedCount;
""";
        await using var command = new MySqlCommand(sql, target);
        command.Parameters.AddWithValue("@tenantId", options.TenantId.ToString());
        command.Parameters.AddWithValue("@orgId", options.OrgId.ToString());
        command.Parameters.AddWithValue("@sourceSystem", SourceSystem);
        command.Parameters.AddWithValue("@fingerprint", fingerprint);
        command.Parameters.AddWithValue("@mappingVersion", MappingVersion);
        command.Parameters.AddWithValue("@checksum", manifest.AggregateChecksum);
        command.Parameters.AddWithValue("@caseCount", manifest.ExpectedImportedCaseCount);
        command.Parameters.AddWithValue("@lienCount", manifest.ExpectedImportedLienCount);
        command.Parameters.AddWithValue("@excludedCount", manifest.ExpectedExcludedCount);
        var candidates = new List<CompletedRunCandidate>();
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
                candidates.Add(new CompletedRunCandidate(
                    Guid.Parse(reader.GetString(0)),
                    Convert.ToInt32(reader.GetValue(1), CultureInfo.InvariantCulture)));
        }

        if (candidates.Count == 0)
            return false;
        if (candidates.Count != 1)
            throw new InvalidOperationException("More than one matching completed update-history run exists; disable reads and repair forward.");

        if (!await HasCompleteRunEvidenceAsync(target, options, candidates[0], plan))
            throw new InvalidOperationException(
                "A matching completed run exists but its event, crosswalk, or exception evidence is incomplete; disable reads and repair forward.");
        return true;
    }

    private static async Task<bool> HasCompleteRunEvidenceAsync(
        MySqlConnection target,
        Options options,
        CompletedRunCandidate candidate,
        ImportPlan plan)
    {
        const string countSql = """
SELECT
    (SELECT COUNT(*) FROM liens_LegacyUpdateEvents WHERE ImportRunId = @runId),
    (SELECT COUNT(*) FROM liens_LegacyIdCrosswalks
     WHERE ImportRunId = @runId AND TargetEntity = 'LegacyUpdateEvent'),
    (SELECT COUNT(*)
     FROM liens_LegacyUpdateEvents e
     INNER JOIN liens_LegacyIdCrosswalks x
       ON x.TargetId = e.Id AND x.ImportRunId = e.ImportRunId
      AND x.TenantId = e.TenantId AND x.SourceSystem = e.SourceSystem
      AND x.SourceTable = e.SourceTable AND x.LegacyId = e.LegacyId
      AND x.TargetEntity = 'LegacyUpdateEvent'
     WHERE e.ImportRunId = @runId AND e.TenantId = @tenantId AND e.OrgId = @orgId
       AND x.SourceSystem = @sourceSystem);
""";
        await using (var countCommand = new MySqlCommand(countSql, target))
        {
            countCommand.Parameters.AddWithValue("@runId", candidate.Id.ToString());
            countCommand.Parameters.AddWithValue("@tenantId", options.TenantId.ToString());
            countCommand.Parameters.AddWithValue("@orgId", options.OrgId.ToString());
            countCommand.Parameters.AddWithValue("@sourceSystem", SourceSystem);
            await using var reader = await countCommand.ExecuteReaderAsync();
            if (!await reader.ReadAsync()
                || reader.GetInt32(0) != candidate.InsertedEventCount
                || reader.GetInt32(1) != candidate.InsertedEventCount
                || reader.GetInt32(2) != candidate.InsertedEventCount)
                return false;
        }

        const string eventEvidenceSql = """
SELECT e.Id, e.TenantId, e.OrgId, e.SourceSystem, e.SourceTable, e.LegacyId,
       x.TargetId, x.SourceHash, x.ImportRunId
FROM liens_LegacyUpdateEvents e
INNER JOIN liens_LegacyIdCrosswalks x
  ON x.TargetId = e.Id AND x.ImportRunId = e.ImportRunId
 AND x.TenantId = e.TenantId AND x.SourceSystem = e.SourceSystem
 AND x.SourceTable = e.SourceTable AND x.LegacyId = e.LegacyId
 AND x.TargetEntity = 'LegacyUpdateEvent'
WHERE e.ImportRunId = @runId;
""";
        await using (var eventEvidenceCommand = new MySqlCommand(eventEvidenceSql, target))
        {
            eventEvidenceCommand.Parameters.AddWithValue("@runId", candidate.Id.ToString());
            await using var eventEvidenceReader = await eventEvidenceCommand.ExecuteReaderAsync();
            var actualEventEvidence = new List<string>();
            while (await eventEvidenceReader.ReadAsync())
            {
                actualEventEvidence.Add(EventEvidenceKey(
                    Guid.Parse(eventEvidenceReader.GetString(0)),
                    Guid.Parse(eventEvidenceReader.GetString(1)),
                    Guid.Parse(eventEvidenceReader.GetString(2)),
                    eventEvidenceReader.GetString(3),
                    eventEvidenceReader.GetString(4),
                    eventEvidenceReader.GetString(5),
                    Guid.Parse(eventEvidenceReader.GetString(6)),
                    eventEvidenceReader.GetString(7),
                    Guid.Parse(eventEvidenceReader.GetString(8))));
            }

            var expectedEventEvidence = plan.ImportedEvidence
                .Where(item => item.ImportRunId == candidate.Id)
                .Select(item => EventEvidenceKey(
                    item.EventId,
                    options.TenantId,
                    options.OrgId,
                    SourceSystem,
                    item.SourceTable,
                    item.LegacyId,
                    item.EventId,
                    item.SourceHash,
                    item.ImportRunId))
                .ToList();
            if (expectedEventEvidence.Count != candidate.InsertedEventCount
                || !actualEventEvidence.Order(StringComparer.Ordinal).SequenceEqual(
                    expectedEventEvidence.Order(StringComparer.Ordinal),
                    StringComparer.Ordinal))
                return false;
        }

        const string exceptionSql = """
SELECT TenantId, SourceTable, LegacyId, Severity, ErrorCode, Message, SourceHash
FROM liens_LegacyImportExceptions
WHERE ImportRunId = @runId;
""";
        await using var exceptionCommand = new MySqlCommand(exceptionSql, target);
        exceptionCommand.Parameters.AddWithValue("@runId", candidate.Id.ToString());
        await using var exceptionReader = await exceptionCommand.ExecuteReaderAsync();
        var actual = new List<string>();
        while (await exceptionReader.ReadAsync())
        {
            actual.Add(ExceptionEvidenceKey(
                Guid.Parse(exceptionReader.GetString(0)),
                exceptionReader.GetString(1),
                exceptionReader.GetString(2),
                exceptionReader.GetString(3),
                exceptionReader.GetString(4),
                exceptionReader.GetString(5),
                Text(exceptionReader, 6)));
        }

        var expected = plan.Exceptions.Select(item => ExceptionEvidenceKey(
            options.TenantId,
            item.Source.SourceTable,
            item.Source.Id.ToString(CultureInfo.InvariantCulture),
            "Warning",
            item.ErrorCode,
            "Legacy update event excluded by approved migration policy.",
            item.Source.SourceHash));
        return actual.Order(StringComparer.Ordinal).SequenceEqual(expected.Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }

    private static string EventEvidenceKey(
        Guid eventId,
        Guid tenantId,
        Guid orgId,
        string sourceSystem,
        string sourceTable,
        string legacyId,
        Guid targetId,
        string sourceHash,
        Guid importRunId) =>
        $"{eventId:D}\u001f{tenantId:D}\u001f{orgId:D}\u001f{sourceSystem}\u001f{sourceTable}\u001f{legacyId}\u001f{targetId:D}\u001f{sourceHash}\u001f{importRunId:D}";

    private static string ExceptionEvidenceKey(
        Guid tenantId,
        string sourceTable,
        string legacyId,
        string severity,
        string errorCode,
        string message,
        string? sourceHash) => $"{tenantId:D}\u001f{sourceTable}\u001f{legacyId}\u001f{severity}\u001f{errorCode}\u001f{message}\u001f{sourceHash}";

    private static async Task MarkRunFailedAsync(MySqlConnection target, Guid runId, string error)
    {
        const string sql = """
UPDATE liens_LegacyImportRuns
SET Status = 'Failed', CompletedAtUtc = UTC_TIMESTAMP(6), ErrorSummary = @error
WHERE Id = @id AND Status = 'Running';
""";
        await using var command = new MySqlCommand(sql, target);
        command.Parameters.AddWithValue("@id", runId.ToString());
        command.Parameters.AddWithValue("@error", Truncate(error, 2000));
        await command.ExecuteNonQueryAsync();
    }

    private static async Task SetUtcSessionAsync(MySqlConnection connection)
    {
        await using var command = new MySqlCommand("SET time_zone = '+00:00';", connection);
        await command.ExecuteNonQueryAsync();
    }

    private static DateTime ConvertPacificToUtc(DateTime wallClock)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles");
        var unspecified = DateTime.SpecifyKind(wallClock, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(unspecified) || zone.IsAmbiguousTime(unspecified))
            throw new InvalidOperationException($"Pacific wall-clock timestamp '{unspecified:yyyy-MM-dd HH:mm:ss}' is invalid or ambiguous.");
        return TimeZoneInfo.ConvertTimeToUtc(unspecified, zone);
    }

    private static DateTime ParseWallClock(string? value, string table, long id, bool required)
    {
        if (DateTime.TryParseExact(value, "yyyy-MM-dd HH:mm:ss.ffffff", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            return DateTime.SpecifyKind(parsed, DateTimeKind.Unspecified);
        if (required)
            throw new InvalidOperationException($"{table}:{id} has a missing or invalid wall-clock timestamp.");
        return DateTime.UnixEpoch;
    }

    private static async Task<string> ResolveFingerprintAsync(Options options)
    {
        if (!string.IsNullOrWhiteSpace(options.SourceDumpPath))
        {
            if (!File.Exists(options.SourceDumpPath))
                throw new ArgumentException("The --source-dump path does not exist.");
            await using var stream = File.OpenRead(options.SourceDumpPath);
            return Convert.ToHexString(await SHA256.HashDataAsync(stream)).ToLowerInvariant();
        }
        if (!IsSha256(options.SourceFingerprint))
            throw new ArgumentException("--source-fingerprint must be a SHA-256 hexadecimal value.");
        return options.SourceFingerprint!.ToLowerInvariant();
    }

    private static string VersionedRowHash(string fingerprint, string table, long id, params string?[] values)
    {
        var payload = string.Join('\u001f', new[] { MappingVersion, fingerprint, table, id.ToString(CultureInfo.InvariantCulture) }
            .Concat(values.Select(value => value ?? "<NULL>")));
        return $"update-history-v2:{Sha256(Encoding.UTF8.GetBytes(payload))}";
    }

    private static bool IsDeleted(string? value) => string.Equals(value?.Trim(), "Y", StringComparison.OrdinalIgnoreCase);
    private static string? Text(MySqlDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    private static string Key(string table, string id) => $"{table}\u001f{id}";
    private static string Sha256(byte[] value) => Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();
    private static bool IsSha256(string? value) => value?.Length == 64 && value.All(char.IsAsciiHexDigit);
    private static string Truncate(string value, int length) => value.Length <= length ? value : value[..length];
}
