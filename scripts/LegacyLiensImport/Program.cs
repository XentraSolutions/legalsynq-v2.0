using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using MySqlConnector;

return await LegacyLiensImportProgram.RunAsync(args);

internal static class LegacyLiensImportProgram
{
    private const string SourceSystem = "SL-CORE";
    private const string CaseTable = "SL_CASE";
    private const string LienTable = "SL_LEINS_MEDICAL";
    private const string CaseNoteTable = "SL_CASE_NOTES";
    private const string ImportScope = "sl-core-core-liens-v1";
    private const string IdentityManifestCertificateSubject = "CN=LegalSynq Identity Migration Signing";

    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Any(arg => arg is "--backfill-case-relationships" or "--backfill-v3-report-fields"))
            return await CaseRelationshipBackfill.RunAsync(args);

        if (args.Any(arg => arg is "--help" or "-h"))
        {
            WriteUsage();
            return 0;
        }

        Options options;
        try
        {
            options = Options.Parse(args);
        }
        catch (ArgumentException exception)
        {
            Console.Error.WriteLine($"ERROR: {exception.Message}");
            Console.Error.WriteLine("Run with --help for usage.");
            return 2;
        }

        try
        {
            var sourceFingerprint = await ResolveSourceFingerprintAsync(options);
            var mappingManifest = options.Apply
                ? await MappingManifest.LoadAndValidateAsync(options, sourceFingerprint)
                : null;
            await using var source = new MySqlConnection(options.LegacyConnectionString);
            await using var target = new MySqlConnection(options.TargetConnectionString);
            await source.OpenAsync();
            await target.OpenAsync();

            var targetStore = new TargetStore(target, options);
            await targetStore.EnsureRequiredSchemaAsync();

            await SourceData.VerifyRestoreProvenanceAsync(source, sourceFingerprint);
            var sourceData = await SourceData.LoadAsync(source, options.LegacyProgram);
            var crosswalks = await targetStore.LoadCrosswalksAsync();
            var targetNumbers = await targetStore.LoadTargetNumbersAsync();
            var plan = ImportPlan.Create(options, sourceData, crosswalks, targetNumbers, sourceFingerprint);

            Console.WriteLine($"Tenant: {options.TenantId}");
            Console.WriteLine($"Legacy program: {options.LegacyProgram}");
            Console.WriteLine($"Mode: {(options.Apply ? "APPLY" : "DRY RUN")}");
            Console.WriteLine($"Cases: {plan.CasesToInsert.Count} insert, {plan.Cases.Count - plan.CasesToInsert.Count} already imported");
            Console.WriteLine($"Liens: {plan.LiensToInsert.Count} insert, {plan.Liens.Count - plan.LiensToInsert.Count} already imported");
            Console.WriteLine($"Case notes: {plan.NotesToInsert.Count} insert, {plan.Notes.Count - plan.NotesToInsert.Count} already imported");

            if (plan.Blockers.Count > 0)
            {
                Console.Error.WriteLine("Preflight failed. No target business rows were written.");
                foreach (var blocker in plan.Blockers.Take(20))
                    Console.Error.WriteLine($"  - {blocker}");
                if (plan.Blockers.Count > 20)
                    Console.Error.WriteLine($"  - {plan.Blockers.Count - 20} additional blocker(s) omitted");
                return 3;
            }

            if (!options.Apply)
            {
                Console.WriteLine("Dry run passed. Re-run with --apply only after the reported mapping choices are approved.");
                return 0;
            }

            var runId = await targetStore.CreateRunAsync(sourceFingerprint, mappingManifest!);
            try
            {
                var importResult = await targetStore.ImportAsync(runId, plan);
                await targetStore.CompleteRunAsync(runId, importResult);
                Console.WriteLine($"Import completed. Run ID: {runId}");
                return 0;
            }
            catch (Exception exception)
            {
                await targetStore.FailRunAsync(runId, exception.GetType().Name);
                throw;
            }
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"ERROR: {exception.Message}");
            return 1;
        }
    }

    private static async Task<string> ResolveSourceFingerprintAsync(Options options)
    {
        if (options.Apply && !string.IsNullOrWhiteSpace(options.SourceFingerprint))
            throw new ArgumentException("--source-fingerprint cannot be used with --apply; provide the immutable --source-dump so its SHA-256 can be verified against the signed approval.");

        if (string.IsNullOrWhiteSpace(options.SourceDumpPath))
        {
            if (!string.IsNullOrWhiteSpace(options.SourceFingerprint))
            {
                if (!IsSha256(options.SourceFingerprint))
                    throw new ArgumentException("--source-fingerprint must be a 64-character SHA-256 hexadecimal value.");
                return options.SourceFingerprint;
            }
            throw new ArgumentException("Provide --source-fingerprint or --source-dump.");
        }

        if (!File.Exists(options.SourceDumpPath))
            throw new ArgumentException("The --source-dump path does not exist.");

        await using var stream = File.OpenRead(options.SourceDumpPath);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream)).ToLowerInvariant();
    }

    private static bool IsSha256(string value) => value.Length == 64 && value.All(character => char.IsAsciiHexDigit(character));

    private static void WriteUsage()
    {
        Console.WriteLine("""
LegacyLiensImport imports the approved SL-CORE core Liens scope for exactly one tenant.

Required arguments:
  --tenant-id <guid>              Destination LegalSynq tenant ID.
  --org-id <guid>                 Existing destination owner organization ID.
  --migration-user-id <guid>      Existing Identity user ID recorded as import actor.
  --legacy-program <1|2|3>        Source SL-CORE program to migrate.
  --legacy-connection <value>     Staging SL-CORE MySQL connection string, or set LegacySlCoreConnectionString.
  --target-connection <value>     LiensDb MySQL connection string, or set ConnectionStrings__LiensDb.
  --source-dump <path>            Dump file for a SHA-256 import fingerprint, or --source-fingerprint <sha256>.

Apply controls:
  --apply                          Required to write. Without it the tool only runs preflight.
  --lien-amount-source billing|purchase
                                  Required with --apply; determines OriginalAmount/CurrentBalance.
  --mapping-manifest <path>       Identity-issued, signed tenant/org/actor/source mapping manifest; required with --apply.
  --mapping-manifest-signature <path>
                                  Detached Base64 RSA-SHA256 signature for the mapping manifest; required with --apply.
  --case-number-collision fail|suffix-legacy-id
  --lien-number-collision fail|suffix-legacy-id
                                  Default is fail; suffix mode must be explicitly approved.

Scope: cases, medical-lien headers, and case notes only. It records durable import runs,
crosswalks, and reserves non-sensitive exception storage. It does not migrate documents, detailed medical
charges/facility links, payments/settlements, contacts, or workflow state.

Relationship repair:
  --backfill-v3-report-fields    Preferred all-fields v3 repair entry point.
                                  Migrates case parity values and relationships,
                                  medical codes/providers, facilities, and last activity.
  --backfill-case-relationships  Runs the guarded Program 1 law-firm,
                                  case-manager, attorney, typed report-parity,
                                  accident-type, case-status-label, and
                                  lien-facility repair.
                                  Use --backfill-case-relationships --help for
                                  its required arguments and dry-run/apply flow.
""");
    }

    private sealed record Options(
        Guid TenantId,
        Guid OrgId,
        Guid MigrationUserId,
        long LegacyProgram,
        string LegacyConnectionString,
        string TargetConnectionString,
        string? SourceDumpPath,
        string? SourceFingerprint,
        string? MappingManifestPath,
        string? MappingManifestSignaturePath,
        bool Apply,
        AmountSource? LienAmountSource,
        CollisionMode CaseNumberCollision,
        CollisionMode LienNumberCollision)
    {
        public static Options Parse(string[] args)
        {
            var values = ParseArguments(args);
            var apply = values.ContainsKey("apply");
            var tenantId = RequireGuid(values, "tenant-id");
            var orgId = RequireGuid(values, "org-id");
            var migrationUserId = RequireGuid(values, "migration-user-id");
            var legacyProgram = RequireLong(values, "legacy-program");
            if (legacyProgram <= 0)
                throw new ArgumentException("--legacy-program must be positive.");

            var legacyConnection = Optional(values, "legacy-connection")
                ?? Environment.GetEnvironmentVariable("LegacySlCoreConnectionString")
                ?? throw new ArgumentException("Provide --legacy-connection or set LegacySlCoreConnectionString.");
            var targetConnection = Optional(values, "target-connection")
                ?? Environment.GetEnvironmentVariable("ConnectionStrings__LiensDb")
                ?? throw new ArgumentException("Provide --target-connection or set ConnectionStrings__LiensDb.");

            AmountSource? lienAmountSource = Optional(values, "lien-amount-source") is { } rawAmountSource
                ? ParseAmountSource(rawAmountSource)
                : null;
            if (apply && lienAmountSource is null)
                throw new ArgumentException("--lien-amount-source is required with --apply.");

            var mappingManifestPath = Optional(values, "mapping-manifest");
            var mappingManifestSignaturePath = Optional(values, "mapping-manifest-signature");
            if (apply && (string.IsNullOrWhiteSpace(mappingManifestPath) || string.IsNullOrWhiteSpace(mappingManifestSignaturePath)))
                throw new ArgumentException("--mapping-manifest and --mapping-manifest-signature are required with --apply.");
            if (apply && string.IsNullOrWhiteSpace(Optional(values, "source-dump")))
                throw new ArgumentException("--source-dump is required with --apply so the signed source fingerprint can be locally verified.");
            if (apply && !string.IsNullOrWhiteSpace(Optional(values, "source-fingerprint")))
                throw new ArgumentException("--source-fingerprint is only allowed for dry runs; --apply hashes --source-dump locally.");

            return new Options(
                tenantId,
                orgId,
                migrationUserId,
                legacyProgram,
                legacyConnection,
                targetConnection,
                Optional(values, "source-dump"),
                Optional(values, "source-fingerprint"),
                mappingManifestPath,
                mappingManifestSignaturePath,
                apply,
                lienAmountSource,
                ParseCollisionMode(Optional(values, "case-number-collision") ?? "fail", "case-number-collision"),
                ParseCollisionMode(Optional(values, "lien-number-collision") ?? "fail", "lien-number-collision"));
        }

        private static Dictionary<string, string?> ParseArguments(string[] args)
        {
            var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < args.Length; index++)
            {
                var argument = args[index];
                if (!argument.StartsWith("--", StringComparison.Ordinal))
                    throw new ArgumentException($"Unexpected argument '{argument}'.");

                var key = argument[2..];
                if (key == "apply")
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

        private static Guid RequireGuid(IReadOnlyDictionary<string, string?> values, string key)
        {
            if (!Guid.TryParse(Optional(values, key), out var result) || result == Guid.Empty)
                throw new ArgumentException($"--{key} must be a non-empty GUID.");
            return result;
        }

        private static long RequireLong(IReadOnlyDictionary<string, string?> values, string key)
        {
            if (!long.TryParse(Optional(values, key), NumberStyles.None, CultureInfo.InvariantCulture, out var result))
                throw new ArgumentException($"--{key} must be an integer.");
            return result;
        }

        private static AmountSource ParseAmountSource(string value) => value.Trim().ToLowerInvariant() switch
        {
            "billing" => AmountSource.Billing,
            "purchase" => AmountSource.Purchase,
            _ => throw new ArgumentException("--lien-amount-source must be 'billing' or 'purchase'.")
        };

        private static CollisionMode ParseCollisionMode(string value, string argumentName) => value.Trim().ToLowerInvariant() switch
        {
            "fail" => CollisionMode.Fail,
            "suffix-legacy-id" => CollisionMode.SuffixLegacyId,
            _ => throw new ArgumentException($"--{argumentName} must be 'fail' or 'suffix-legacy-id'.")
        };
    }

    private enum AmountSource { Billing, Purchase }
    private enum CollisionMode { Fail, SuffixLegacyId }

    private sealed record MappingManifest(Guid TenantId, Guid OrgId, Guid MigrationUserId, long LegacyProgram, string MappingVersion, string ApprovalReference, string Hash)
    {
        private sealed record Payload(string? TenantId, string? OrgId, string? MigrationUserId, long? LegacyProgram, string? MappingVersion, string? ApprovalReference, string? SourceFingerprint, string? ImportScope);

        public static async Task<MappingManifest> LoadAndValidateAsync(Options options, string sourceFingerprint)
        {
            var manifestPath = options.MappingManifestPath!;
            var signaturePath = options.MappingManifestSignaturePath!;
            if (!File.Exists(manifestPath) || !File.Exists(signaturePath))
                throw new ArgumentException("The mapping manifest or its signature file does not exist.");

            var bytes = await File.ReadAllBytesAsync(manifestPath);
            byte[] signature;
            try
            {
                signature = Convert.FromBase64String((await File.ReadAllTextAsync(signaturePath)).Trim());
            }
            catch (FormatException)
            {
                throw new ArgumentException("The mapping manifest signature must be Base64-encoded.");
            }

            using var certificate = GetIdentityManifestSigningCertificate();
            using var rsa = certificate.GetRSAPublicKey()
                ?? throw new ArgumentException("The protected Identity manifest signing certificate does not contain an RSA public key.");
            if (!rsa.VerifyData(bytes, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
                throw new ArgumentException("The mapping manifest signature is invalid for the protected Identity signing certificate.");

            Payload? payload;
            try
            {
                payload = JsonSerializer.Deserialize<Payload>(bytes, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (JsonException)
            {
                throw new ArgumentException("The mapping manifest is not valid JSON.");
            }

            if (payload is null
                || !Guid.TryParse(payload.TenantId, out var tenantId)
                || !Guid.TryParse(payload.OrgId, out var orgId)
                || !Guid.TryParse(payload.MigrationUserId, out var migrationUserId)
                || tenantId == Guid.Empty
                || orgId == Guid.Empty
                || migrationUserId == Guid.Empty
                || payload.LegacyProgram is null or <= 0
                || string.IsNullOrWhiteSpace(payload.MappingVersion)
                || string.IsNullOrWhiteSpace(payload.ApprovalReference)
                || string.IsNullOrWhiteSpace(payload.SourceFingerprint)
                || string.IsNullOrWhiteSpace(payload.ImportScope))
                throw new ArgumentException("The signed mapping manifest is missing a required tenant, organization, actor, program, mapping version, approval reference, source fingerprint, or import scope.");

            if (tenantId != options.TenantId || orgId != options.OrgId || migrationUserId != options.MigrationUserId || payload.LegacyProgram.Value != options.LegacyProgram)
                throw new ArgumentException("The signed mapping manifest does not exactly match --tenant-id, --org-id, --migration-user-id, and --legacy-program.");
            if (!string.Equals(payload.SourceFingerprint, sourceFingerprint, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("The signed mapping manifest does not authorize the SHA-256 fingerprint of --source-dump.");
            if (!string.Equals(payload.ImportScope, ImportScope, StringComparison.Ordinal))
                throw new ArgumentException($"The signed mapping manifest import scope must be '{ImportScope}'.");
            if (payload.MappingVersion.Trim().Length > 100 || payload.ApprovalReference.Trim().Length > 200)
                throw new ArgumentException("The signed mapping manifest mapping version or approval reference exceeds the control-plane limits.");

            return new MappingManifest(
                tenantId,
                orgId,
                migrationUserId,
                payload.LegacyProgram.Value,
                payload.MappingVersion.Trim(),
                payload.ApprovalReference.Trim(),
                Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
        }

        private static X509Certificate2 GetIdentityManifestSigningCertificate()
        {
            using var store = new X509Store(StoreName.TrustedPeople, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadOnly);
            var certificates = store.Certificates
                .Find(X509FindType.FindBySubjectDistinguishedName, IdentityManifestCertificateSubject, validOnly: false)
                .Where(certificate => certificate.NotBefore <= DateTime.Now
                    && certificate.NotAfter >= DateTime.Now
                    && HasRsaPublicKey(certificate))
                .ToList();

            return certificates.Count switch
            {
                1 => new X509Certificate2(certificates[0]),
                0 => throw new ArgumentException($"Install the Identity manifest signing certificate with subject '{IdentityManifestCertificateSubject}' in LocalMachine\\TrustedPeople before using --apply."),
                _ => throw new ArgumentException($"More than one usable Identity manifest signing certificate with subject '{IdentityManifestCertificateSubject}' is installed. Resolve the trust-store ambiguity before using --apply.")
            };
        }

        private static bool HasRsaPublicKey(X509Certificate2 certificate)
        {
            using var rsa = certificate.GetRSAPublicKey();
            return rsa is not null;
        }
    }

    private sealed class SourceData
    {
        public IReadOnlyList<LegacyCase> Cases { get; init; } = [];
        public IReadOnlyList<LegacyLien> Liens { get; init; } = [];
        public IReadOnlyList<LegacyCaseNote> Notes { get; init; } = [];
        public IReadOnlyDictionary<long, LienAmounts> AmountsByLienId { get; init; } = new Dictionary<long, LienAmounts>();

        public static async Task<SourceData> LoadAsync(MySqlConnection connection, long legacyProgram)
        {
            await VerifyCaseNoteContractAsync(connection);
            var cases = await LoadCasesAsync(connection, legacyProgram);
            var liens = await LoadLiensAsync(connection, legacyProgram);
            var notes = await LoadNotesAsync(connection, legacyProgram);
            var amounts = await LoadLienAmountsAsync(connection, legacyProgram);
            return new SourceData
            {
                Cases = cases,
                Liens = liens,
                Notes = notes,
                AmountsByLienId = amounts
            };
        }

        private static async Task VerifyCaseNoteContractAsync(MySqlConnection connection)
        {
            const string sql = """
SELECT COUNT(*)
FROM information_schema.columns
WHERE table_schema = DATABASE()
  AND table_name = 'SL_CASE_NOTES'
  AND column_name IN ('CN_ID','CN_CASE_ID','CN_NOTE','CN_CREATED','CN_CREATED_BY','CN_IS_DELETED','CN_USER_ID');
""";
            await using var command = new MySqlCommand(sql, connection);
            var count = Convert.ToInt32(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
            if (count != 7)
                throw new InvalidOperationException("SL_CASE_NOTES source column contract is incomplete; CN_USER_ID is required for tracking/feed classification.");
        }

        public static async Task VerifyRestoreProvenanceAsync(MySqlConnection connection, string expectedFingerprint)
        {
            const string sql = """
SELECT SOURCE_FINGERPRINT, IMPORT_SCOPE
FROM SL_MIGRATION_SOURCE_PROVENANCE
WHERE PROVENANCE_KEY = 'sl-core-current';
""";
            try
            {
                await using var command = new MySqlCommand(sql, connection);
                await using var reader = await command.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                    throw new InvalidOperationException("SL-CORE staging provenance is missing. Restore the approved dump through the controlled restore process and record its fingerprint before running this tool.");

                var sourceFingerprint = Text(reader, 0);
                var importScope = Text(reader, 1);
                if (!string.Equals(sourceFingerprint, expectedFingerprint, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(importScope, ImportScope, StringComparison.Ordinal))
                    throw new InvalidOperationException("SL-CORE staging provenance does not match the approved source fingerprint and import scope. Do not continue until the controlled restore receipt is corrected.");

                if (await reader.ReadAsync())
                    throw new InvalidOperationException("SL-CORE staging provenance has more than one current receipt. Resolve the staging restore state before running this tool.");
            }
            catch (MySqlException exception) when (exception.Number == 1146)
            {
                throw new InvalidOperationException("SL-CORE staging provenance table is missing. Restore the approved dump through the controlled restore process and create its provenance receipt before running this tool.");
            }
        }

        private static async Task<List<LegacyCase>> LoadCasesAsync(MySqlConnection connection, long legacyProgram)
        {
            const string sql = """
SELECT CASE_ID, CASE_CODE, CASE_FNAME, CASE_LNAME, CASE_DOB, CASE_ADDRESS, CASE_CITY,
       CASE_STATE, CASE_ZIPCODE, CASE_STATUS, CASE_DATE_OF_LOSS, CASE_NOTE, CASE_CREATED,
       CASE_UPDATED, CASE_IS_BULK, CASE_IS_SERVICING, CASE_LAW_FIRM, CASE_MANAGER
FROM SL_CASE
WHERE CASE_PROGRAM = @program AND COALESCE(CASE_IS_DELETED, 'N') <> 'Y'
ORDER BY CASE_ID;
""";
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@program", legacyProgram);
            await using var reader = await command.ExecuteReaderAsync();
            var result = new List<LegacyCase>();
            while (await reader.ReadAsync())
            {
                result.Add(new LegacyCase(
                    reader.GetInt64(0),
                    Text(reader, 1),
                    Text(reader, 2) ?? string.Empty,
                    Text(reader, 3) ?? string.Empty,
                    Date(reader, 4),
                    Text(reader, 5),
                    Text(reader, 6),
                    Text(reader, 7),
                    Text(reader, 8),
                    Text(reader, 9),
                    Text(reader, 10),
                    Text(reader, 11),
                    DateTimeValue(reader, 12),
                    DateTimeValue(reader, 13),
                    Text(reader, 14),
                    Text(reader, 15),
                    Text(reader, 16),
                    Text(reader, 17)));
            }
            return result;
        }

        private static async Task<List<LegacyLien>> LoadLiensAsync(MySqlConnection connection, long legacyProgram)
        {
            const string sql = """
SELECT lm.LM_ID, lm.LM_CASE_ID, lm.LM_STATUS, lm.LM_PURCHASE_DATE, lm.LM_INITIAL_SERVICE_DATE,
       lm.LM_END_SERVICE_DATE, lm.LM_NOTE, lm.LM_CREATED, lm.LM_UPDATED, lm.LM_CODE,
       lm.LM_IS_BULK, lm.LM_IS_SERVICING, c.CASE_DATE_OF_LOSS, c.CASE_FNAME, c.CASE_LNAME,
       c.CASE_ACCIDENT_STATE
FROM SL_LEINS_MEDICAL lm
INNER JOIN SL_CASE c ON c.CASE_ID = lm.LM_CASE_ID
WHERE c.CASE_PROGRAM = @program
  AND COALESCE(c.CASE_IS_DELETED, 'N') <> 'Y'
  AND COALESCE(lm.LM_IS_DELETED, 'N') <> 'Y'
ORDER BY lm.LM_ID;
""";
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@program", legacyProgram);
            await using var reader = await command.ExecuteReaderAsync();
            var result = new List<LegacyLien>();
            while (await reader.ReadAsync())
            {
                result.Add(new LegacyLien(
                    reader.GetInt64(0),
                    reader.GetInt64(1),
                    Text(reader, 2),
                    Date(reader, 3),
                    Date(reader, 4),
                    Date(reader, 5),
                    Text(reader, 6),
                    DateTimeValue(reader, 7),
                    DateTimeValue(reader, 8),
                    Text(reader, 9),
                    Text(reader, 10),
                    Text(reader, 11),
                    Text(reader, 12),
                    Text(reader, 13),
                    Text(reader, 14),
                    Text(reader, 15)));
            }
            return result;
        }

        private static async Task<List<LegacyCaseNote>> LoadNotesAsync(MySqlConnection connection, long legacyProgram)
        {
            const string sql = """
SELECT n.CN_ID, n.CN_CASE_ID, n.CN_NOTE, n.CN_CREATED, n.CN_CREATED_BY, n.CN_IS_DELETED, n.CN_USER_ID
FROM SL_CASE_NOTES n
INNER JOIN SL_CASE c ON c.CASE_ID = n.CN_CASE_ID
WHERE c.CASE_PROGRAM = @program AND COALESCE(c.CASE_IS_DELETED, 'N') <> 'Y'
ORDER BY n.CN_ID;
""";
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@program", legacyProgram);
            await using var reader = await command.ExecuteReaderAsync();
            var result = new List<LegacyCaseNote>();
            while (await reader.ReadAsync())
            {
                result.Add(new LegacyCaseNote(
                    reader.GetInt64(0),
                    reader.GetInt64(1),
                    Text(reader, 2),
                    DateTimeValue(reader, 3),
                    Text(reader, 4),
                    Text(reader, 5),
                    Text(reader, 6)));
            }
            return result;
        }

        private static async Task<Dictionary<long, LienAmounts>> LoadLienAmountsAsync(MySqlConnection connection, long legacyProgram)
        {
            const string sql = """
SELECT code.LMC_LM_ID, code.LMC_BILLING_AMOUNT, code.LMC_PURCHASE_AMOUNT
FROM SL_LEINS_MEDICAL_CODE code
INNER JOIN SL_LEINS_MEDICAL lm ON lm.LM_ID = code.LMC_LM_ID
INNER JOIN SL_CASE c ON c.CASE_ID = lm.LM_CASE_ID
WHERE c.CASE_PROGRAM = @program
  AND COALESCE(c.CASE_IS_DELETED, 'N') <> 'Y'
  AND COALESCE(lm.LM_IS_DELETED, 'N') <> 'Y'
ORDER BY code.LMC_LM_ID, code.LMC_ID;
""";
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@program", legacyProgram);
            await using var reader = await command.ExecuteReaderAsync();
            var result = new Dictionary<long, LienAmounts>();
            while (await reader.ReadAsync())
            {
                var lienId = reader.GetInt64(0);
                result.TryGetValue(lienId, out var amounts);
                result[lienId] = amounts.Add(Text(reader, 1), Text(reader, 2));
            }
            return result;
        }

        private static string? Text(MySqlDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

        private static DateOnly? Date(MySqlDataReader reader, int ordinal)
        {
            if (reader.IsDBNull(ordinal)) return null;
            return DateOnly.FromDateTime(reader.GetDateTime(ordinal));
        }

        private static DateTime? DateTimeValue(MySqlDataReader reader, int ordinal) =>
            reader.IsDBNull(ordinal) ? null : DateTime.SpecifyKind(reader.GetDateTime(ordinal), DateTimeKind.Utc);
    }

    private readonly record struct LienAmounts(
        decimal Billing,
        decimal Purchase,
        int BillingValues,
        int PurchaseValues,
        int InvalidBillingValues,
        int InvalidPurchaseValues,
        string SourceHash)
    {
        public LienAmounts Add(string? rawBilling, string? rawPurchase)
        {
            var billing = ParseLegacyMoney(rawBilling);
            var purchase = ParseLegacyMoney(rawPurchase);
            return new LienAmounts(
                Billing + (billing.Value ?? 0m),
                Purchase + (purchase.Value ?? 0m),
                BillingValues + (billing.Value.HasValue ? 1 : 0),
                PurchaseValues + (purchase.Value.HasValue ? 1 : 0),
                InvalidBillingValues + (billing.IsInvalid ? 1 : 0),
                InvalidPurchaseValues + (purchase.IsInvalid ? 1 : 0),
                Hash($"{SourceHash}|{rawBilling}|{rawPurchase}"));
        }
    }

    private sealed record LegacyCase(
        long Id, string? Code, string FirstName, string LastName, DateOnly? Dob, string? Address,
        string? City, string? State, string? Zip, string? Status, string? DateOfLoss, string? Notes,
        DateTime? CreatedAtUtc, DateTime? UpdatedAtUtc, string? IsBulk, string? IsServicing,
        string? LawFirmId, string? CaseManagerId)
    {
        public string SourceHash => Hash($"{Id}|{Code}|{FirstName}|{LastName}|{Dob}|{Address}|{City}|{State}|{Zip}|{Status}|{DateOfLoss}|{Notes}|{CreatedAtUtc:o}|{UpdatedAtUtc:o}|{IsBulk}|{IsServicing}|{LawFirmId}|{CaseManagerId}");
    }

    private sealed record LegacyLien(
        long Id, long CaseId, string? Status, DateOnly? PurchaseDate, DateOnly? InitialServiceDate,
        DateOnly? EndServiceDate, string? Notes, DateTime? CreatedAtUtc, DateTime? UpdatedAtUtc,
        string? Code, string? IsBulk, string? IsServicing, string? DateOfLoss,
        string? SubjectFirstName, string? SubjectLastName, string? Jurisdiction)
    {
        public string SourceHash => Hash($"{Id}|{CaseId}|{Status}|{PurchaseDate}|{InitialServiceDate}|{EndServiceDate}|{Notes}|{CreatedAtUtc:o}|{UpdatedAtUtc:o}|{Code}|{IsBulk}|{IsServicing}|{DateOfLoss}|{SubjectFirstName}|{SubjectLastName}|{Jurisdiction}");
    }

    private sealed record LegacyCaseNote(
        long Id,
        long CaseId,
        string? Content,
        DateTime? CreatedAtUtc,
        string? CreatedByName,
        string? IsDeleted,
        string? LegacyUserId)
    {
        public string VersionedSourceHash(string sourceFingerprint)
            => $"case-note-v2:{Hash($"{Id}|{CaseId}|{Content}|{CreatedAtUtc:o}|{CreatedByName}|{IsDeleted}|{LegacyUserId}|{sourceFingerprint}")}";
    }

    private sealed record ExistingCrosswalk(Guid TargetId, string SourceHash);

    private sealed class TargetNumbers
    {
        public ISet<string> CaseNumbers { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public ISet<string> LienNumbers { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class ImportPlan
    {
        public List<CasePlan> Cases { get; } = [];
        public List<LienPlan> Liens { get; } = [];
        public List<NotePlan> Notes { get; } = [];
        public List<string> Blockers { get; } = [];
        public IReadOnlyList<CasePlan> CasesToInsert => Cases.Where(plan => plan.ShouldInsert).ToList();
        public IReadOnlyList<LienPlan> LiensToInsert => Liens.Where(plan => plan.ShouldInsert).ToList();
        public IReadOnlyList<NotePlan> NotesToInsert => Notes.Where(plan => plan.ShouldInsert).ToList();

        public static ImportPlan Create(
            Options options,
            SourceData sourceData,
            IReadOnlyDictionary<string, ExistingCrosswalk> crosswalks,
            TargetNumbers targetNumbers,
            string sourceFingerprint)
        {
            var result = new ImportPlan();
            var caseNumbers = ResolveNumbers(
                sourceData.Cases,
                source => source.Id,
                source => source.Code,
                "SL-CORE-CASE",
                options.CaseNumberCollision,
                "case",
                result.Blockers);

            foreach (var source in sourceData.Cases)
            {
                var crosswalkKey = Key(CaseTable, source.Id);
                var existing = crosswalks.GetValueOrDefault(crosswalkKey);
                if (existing is not null && !string.Equals(existing.SourceHash, source.SourceHash, StringComparison.Ordinal))
                    result.Blockers.Add($"Legacy case {source.Id} changed after an earlier import; delta migration is not supported by this tool.");
                var targetId = existing?.TargetId ?? Guid.CreateVersion7();
                result.Cases.Add(new CasePlan(source, targetId, caseNumbers[source.Id], existing is null));
            }

            var casesByLegacyId = result.Cases.ToDictionary(plan => plan.Source.Id);
            var lienNumbers = ResolveNumbers(
                sourceData.Liens,
                source => source.Id,
                source => source.Code,
                "SL-CORE-LIEN",
                options.LienNumberCollision,
                "lien",
                result.Blockers);

            foreach (var source in sourceData.Liens)
            {
                if (!casesByLegacyId.TryGetValue(source.CaseId, out var casePlan))
                {
                    result.Blockers.Add($"Legacy lien {source.Id} has no in-scope source case.");
                    continue;
                }

                sourceData.AmountsByLienId.TryGetValue(
                    source.Id,
                    out var amounts);
                if (amounts.SourceHash is null)
                    amounts = new LienAmounts(0m, 0m, 0, 0, 0, 0, string.Empty);

                var sourceHash = Hash($"{source.SourceHash}|{amounts.SourceHash}");
                var crosswalkKey = Key(LienTable, source.Id);
                var existing = crosswalks.GetValueOrDefault(crosswalkKey);
                if (existing is not null && !string.Equals(existing.SourceHash, sourceHash, StringComparison.Ordinal))
                    result.Blockers.Add($"Legacy lien {source.Id} changed after an earlier import; delta migration is not supported by this tool.");
                var targetId = existing?.TargetId ?? Guid.CreateVersion7();
                result.Liens.Add(new LienPlan(source, targetId, casePlan.TargetId, lienNumbers[source.Id], amounts, sourceHash, existing is null));
            }

            foreach (var source in sourceData.Notes)
            {
                if (!casesByLegacyId.TryGetValue(source.CaseId, out var casePlan))
                {
                    result.Blockers.Add($"Legacy case note {source.Id} has no in-scope source case.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(source.Content))
                {
                    result.Blockers.Add($"Legacy case note {source.Id} has no content and cannot satisfy the target required field.");
                    continue;
                }

                if (source.Content.Trim().Length > 5000)
                {
                    result.Blockers.Add($"Legacy case note {source.Id} exceeds the target 5,000-character limit.");
                    continue;
                }

                var sourceHash = source.VersionedSourceHash(sourceFingerprint);
                var crosswalkKey = Key(CaseNoteTable, source.Id);
                var existing = crosswalks.GetValueOrDefault(crosswalkKey);
                if (existing is not null && !string.Equals(existing.SourceHash, sourceHash, StringComparison.Ordinal))
                    result.Blockers.Add($"Legacy case note {source.Id} changed after an earlier import; delta migration is not supported by this tool.");
                result.Notes.Add(new NotePlan(source, existing?.TargetId ?? Guid.CreateVersion7(), casePlan.TargetId, sourceHash, existing is null));
            }

            ValidateTargetNumberCollisions(result.CasesToInsert, plan => plan.CaseNumber, targetNumbers.CaseNumbers, "case", result.Blockers);
            ValidateTargetNumberCollisions(result.LiensToInsert, plan => plan.LienNumber, targetNumbers.LienNumbers, "lien", result.Blockers);
            ValidateSourceValues(options, result);

            return result;
        }

        private static Dictionary<long, string> ResolveNumbers<T>(
            IReadOnlyList<T> records,
            Func<T, long> id,
            Func<T, string?> candidate,
            string fallbackPrefix,
            CollisionMode collisionMode,
            string entityName,
            ICollection<string> blockers)
        {
            var preliminary = records.ToDictionary(
                item => id(item),
                item => NormalizeNumber(candidate(item), fallbackPrefix, id(item)));

            foreach (var invalid in preliminary.Where(pair => pair.Value.Length > 50))
                blockers.Add($"Legacy {entityName} {invalid.Key} has a number longer than the target 50-character limit.");

            var duplicates = preliminary
                .GroupBy(pair => pair.Value, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .ToList();
            if (duplicates.Count == 0)
                return preliminary;

            if (collisionMode == CollisionMode.Fail)
            {
                blockers.Add($"{duplicates.Count} duplicate {entityName} number(s) require an explicit collision policy; use suffix-legacy-id only after business approval.");
                return preliminary;
            }

            foreach (var duplicate in duplicates)
            {
                foreach (var pair in duplicate)
                {
                    var suffix = $"-{pair.Key}";
                    preliminary[pair.Key] = Truncate(duplicate.Key, 50 - suffix.Length) + suffix;
                }
            }

            return preliminary;
        }

        private static void ValidateTargetNumberCollisions<T>(
            IEnumerable<T> records,
            Func<T, string> number,
            ISet<string> existing,
            string entityName,
            ICollection<string> blockers)
        {
            var collisions = records.Select(number).Where(existing.Contains).Distinct(StringComparer.OrdinalIgnoreCase).Count();
            if (collisions > 0)
                blockers.Add($"{collisions} {entityName} number(s) already exist for this tenant without a matching legacy crosswalk.");
        }

        private static void ValidateSourceValues(Options options, ImportPlan plan)
        {
            foreach (var casePlan in plan.Cases)
            {
                if (casePlan.Source.FirstName.Trim().Length == 0 || casePlan.Source.LastName.Trim().Length == 0)
                    plan.Blockers.Add($"Legacy case {casePlan.Source.Id} is missing a required first or last name.");
                if (!IsKnownCaseStatus(casePlan.Source.Status))
                    plan.Blockers.Add($"Legacy case {casePlan.Source.Id} has an unsupported status mapping.");
                if (!TryParseDate(casePlan.Source.DateOfLoss, out _) && !string.IsNullOrWhiteSpace(casePlan.Source.DateOfLoss))
                    plan.Blockers.Add($"Legacy case {casePlan.Source.Id} has an unparseable date of loss.");
            }

            foreach (var lienPlan in plan.Liens)
            {
                if (!IsSupportedCoreLienStatus(lienPlan.Source.Status))
                    plan.Blockers.Add($"Legacy lien {lienPlan.Source.Id} has a lifecycle status outside this core-import scope.");
                if (!TryParseDate(lienPlan.Source.DateOfLoss, out _) && !string.IsNullOrWhiteSpace(lienPlan.Source.DateOfLoss))
                    plan.Blockers.Add($"Legacy lien {lienPlan.Source.Id} has an unparseable linked date of loss.");
                if (lienPlan.Amounts.InvalidBillingValues > 0 || lienPlan.Amounts.InvalidPurchaseValues > 0)
                    plan.Blockers.Add($"Legacy lien {lienPlan.Source.Id} has one or more unparseable medical-code amount values.");
                if (lienPlan.Amounts.Billing < 0m || lienPlan.Amounts.Purchase < 0m
                    || lienPlan.Amounts.Billing > MaximumImportAmount || lienPlan.Amounts.Purchase > MaximumImportAmount)
                    plan.Blockers.Add($"Legacy lien {lienPlan.Source.Id} has an unsupported negative or out-of-range financial total.");
                if (options.Apply && lienPlan.Amounts.ValueCount(options.LienAmountSource!.Value) == 0)
                    plan.Blockers.Add($"Legacy lien {lienPlan.Source.Id} has no usable {options.LienAmountSource.Value.ToString().ToLowerInvariant()} amount.");
            }
        }

        private static string NormalizeNumber(string? value, string fallbackPrefix, long legacyId)
        {
            var normalized = value?.Trim();
            return string.IsNullOrWhiteSpace(normalized) ? $"{fallbackPrefix}-{legacyId}" : normalized;
        }
    }

    private sealed record CasePlan(LegacyCase Source, Guid TargetId, string CaseNumber, bool ShouldInsert);
    private sealed record LienPlan(LegacyLien Source, Guid TargetId, Guid TargetCaseId, string LienNumber, LienAmounts Amounts, string SourceHash, bool ShouldInsert);
    private sealed record NotePlan(LegacyCaseNote Source, Guid TargetId, Guid TargetCaseId, string SourceHash, bool ShouldInsert);

    private sealed class TargetStore(MySqlConnection connection, Options options)
    {
        public async Task EnsureRequiredSchemaAsync()
        {
            const string sql = """
SELECT COUNT(*)
FROM information_schema.tables
WHERE table_schema = DATABASE()
  AND table_name IN (
      'liens_Cases', 'liens_Liens', 'liens_CaseNotes',
      'liens_LegacyImportRuns', 'liens_LegacyIdCrosswalks', 'liens_LegacyImportExceptions');
""";
            await using var command = new MySqlCommand(sql, connection);
            var count = Convert.ToInt32(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
            if (count != 6)
                throw new InvalidOperationException("Liens target schema is incomplete. Apply the Liens EF migrations, including AddLegacyImportControlPlane, before running this tool.");
        }

        public async Task<Dictionary<string, ExistingCrosswalk>> LoadCrosswalksAsync()
        {
            const string sql = """
SELECT SourceTable, LegacyId, TargetId, SourceHash, TargetEntity
FROM liens_LegacyIdCrosswalks
WHERE TenantId = @tenantId AND SourceSystem = @sourceSystem
  AND SourceTable IN ('SL_CASE', 'SL_LEINS_MEDICAL', 'SL_CASE_NOTES');
""";
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@tenantId", options.TenantId.ToString());
            command.Parameters.AddWithValue("@sourceSystem", SourceSystem);
            await using var reader = await command.ExecuteReaderAsync();
            var result = new Dictionary<string, ExistingCrosswalk>(StringComparer.Ordinal);
            var invalidEntries = new List<string>();
            while (await reader.ReadAsync())
            {
                var sourceTable = reader.GetString(0);
                var legacyId = reader.GetString(1);
                if (!Guid.TryParse(reader.GetString(2), out var targetId) || targetId == Guid.Empty)
                {
                    invalidEntries.Add($"{sourceTable}:{legacyId} has an invalid target ID");
                    continue;
                }

                var expectedEntity = sourceTable switch
                {
                    CaseTable => "Case",
                    LienTable => "Lien",
                    CaseNoteTable => "CaseNote",
                    _ => null
                };
                if (!string.Equals(expectedEntity, reader.GetString(4), StringComparison.Ordinal))
                {
                    invalidEntries.Add($"{sourceTable}:{legacyId} has target entity '{reader.GetString(4)}' instead of '{expectedEntity}'");
                    continue;
                }

                result[Key(sourceTable, legacyId)] = new ExistingCrosswalk(targetId, reader.GetString(3));
            }
            await reader.DisposeAsync();

            if (invalidEntries.Count > 0)
                throw new InvalidOperationException($"Legacy crosswalk validation failed: {string.Join("; ", invalidEntries.Take(5))}. Repair the control-plane ledger before rerunning.");

            var existingTargets = await LoadCrosswalkTargetsAsync();
            var targetIssues = result
                .Select(entry => new
                {
                    Entry = entry,
                    SourceTable = entry.Key.Split('\u001f')[0],
                    Target = existingTargets.GetValueOrDefault(Key(entry.Key.Split('\u001f')[0], entry.Value.TargetId.ToString()))
                })
                .Where(entry => entry.Target is null || entry.Target.OrgId != options.OrgId)
                .Take(5)
                .Select(entry => $"{entry.Entry.Key.Replace('\u001f', ':')} ({(entry.Target is null ? "target missing from tenant" : "organization differs from approved mapping")})")
                .ToList();
            if (targetIssues.Count > 0)
                throw new InvalidOperationException($"Legacy crosswalk validation found invalid target ownership: {string.Join(", ", targetIssues)}. Do not rerun; repair the target or crosswalk ledger through an audited procedure.");

            return result;
        }

        private sealed record CrosswalkTarget(Guid OrgId);

        private async Task<Dictionary<string, CrosswalkTarget>> LoadCrosswalkTargetsAsync()
        {
            const string sql = """
SELECT 'SL_CASE', Id, OrgId FROM liens_Cases WHERE TenantId = @tenantId
UNION ALL
SELECT 'SL_LEINS_MEDICAL', Id, OrgId FROM liens_Liens WHERE TenantId = @tenantId
UNION ALL
SELECT 'SL_CASE_NOTES', note.Id, caseRecord.OrgId
FROM liens_CaseNotes note
INNER JOIN liens_Cases caseRecord ON caseRecord.Id = note.CaseId
WHERE note.TenantId = @tenantId AND caseRecord.TenantId = @tenantId;
""";
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@tenantId", options.TenantId.ToString());
            await using var reader = await command.ExecuteReaderAsync();
            var result = new Dictionary<string, CrosswalkTarget>(StringComparer.Ordinal);
            while (await reader.ReadAsync())
            {
                if (Guid.TryParse(reader.GetString(1), out var targetId)
                    && Guid.TryParse(reader.GetString(2), out var orgId))
                    result[Key(reader.GetString(0), targetId.ToString())] = new CrosswalkTarget(orgId);
            }
            return result;
        }

        public async Task<TargetNumbers> LoadTargetNumbersAsync()
        {
            var result = new TargetNumbers();
            await LoadNumbersAsync("liens_Cases", "CaseNumber", result.CaseNumbers);
            await LoadNumbersAsync("liens_Liens", "LienNumber", result.LienNumbers);
            return result;
        }

        private async Task LoadNumbersAsync(string tableName, string columnName, ISet<string> values)
        {
            var sql = $"SELECT `{columnName}` FROM `{tableName}` WHERE TenantId = @tenantId;";
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@tenantId", options.TenantId.ToString());
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync()) values.Add(reader.GetString(0));
        }

        public async Task<Guid> CreateRunAsync(string sourceFingerprint, MappingManifest mappingManifest)
        {
            var id = Guid.CreateVersion7();
            const string sql = """
INSERT INTO liens_LegacyImportRuns
    (Id, TenantId, OrgId, SourceSystem, SourceFingerprint, LegacyProgram, MappingVersion,
     MappingManifestHash, MappingApprovalReference, Status, StartedAtUtc, CreatedByUserId)
VALUES
    (@id, @tenantId, @orgId, @sourceSystem, @sourceFingerprint, @legacyProgram, @mappingVersion,
     @mappingManifestHash, @mappingApprovalReference, 'Running', UTC_TIMESTAMP(6), @createdByUserId);
""";
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@id", id.ToString());
            command.Parameters.AddWithValue("@tenantId", options.TenantId.ToString());
            command.Parameters.AddWithValue("@orgId", mappingManifest.OrgId.ToString());
            command.Parameters.AddWithValue("@sourceSystem", SourceSystem);
            command.Parameters.AddWithValue("@sourceFingerprint", sourceFingerprint);
            command.Parameters.AddWithValue("@legacyProgram", mappingManifest.LegacyProgram.ToString(CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("@mappingVersion", mappingManifest.MappingVersion);
            command.Parameters.AddWithValue("@mappingManifestHash", mappingManifest.Hash);
            command.Parameters.AddWithValue("@mappingApprovalReference", mappingManifest.ApprovalReference);
            command.Parameters.AddWithValue("@createdByUserId", mappingManifest.MigrationUserId.ToString());
            await command.ExecuteNonQueryAsync();
            return id;
        }

        public async Task<ImportResult> ImportAsync(Guid runId, ImportPlan plan)
        {
            var cases = await ImportCasesAsync(runId, plan.CasesToInsert);
            var liens = await ImportLiensAsync(runId, plan.LiensToInsert);
            var notes = await ImportNotesAsync(runId, plan.NotesToInsert);
            return new ImportResult(cases, liens, notes, options.LienAmountSource!.Value.ToString());
        }

        public async Task CompleteRunAsync(Guid runId, ImportResult result)
        {
            const string sql = """
UPDATE liens_LegacyImportRuns
SET Status = 'Completed', CompletedAtUtc = UTC_TIMESTAMP(6), SummaryJson = @summaryJson, ErrorSummary = NULL
WHERE Id = @id;
""";
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@id", runId.ToString());
            command.Parameters.AddWithValue("@summaryJson", JsonSerializer.Serialize(result));
            await command.ExecuteNonQueryAsync();
        }

        public async Task FailRunAsync(Guid runId, string errorSummary)
        {
            const string sql = """
UPDATE liens_LegacyImportRuns
SET Status = 'Failed', CompletedAtUtc = UTC_TIMESTAMP(6), ErrorSummary = @errorSummary
WHERE Id = @id;
""";
            await using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@id", runId.ToString());
            command.Parameters.AddWithValue("@errorSummary", Truncate(errorSummary, 2000));
            await command.ExecuteNonQueryAsync();
        }

        private async Task<int> ImportCasesAsync(Guid runId, IReadOnlyList<CasePlan> plans)
        {
            if (plans.Count == 0) return 0;
            await using var transaction = await connection.BeginTransactionAsync();
            foreach (var plan in plans)
            {
                var dateOfLoss = TryParseDate(plan.Source.DateOfLoss, out var parsedDate) ? parsedDate : null;
                const string sql = """
INSERT INTO liens_Cases
    (Id, TenantId, OrgId, CaseNumber, ExternalReference, Title, ClientFirstName, ClientLastName,
     ClientDob, ClientPhone, ClientEmail, ClientAddress, Status, DateOfIncident, OpenedAtUtc,
     ClosedAtUtc, InsuranceCarrier, PolicyNumber, ClaimNumber, DemandAmount, SettlementAmount,
     Description, Notes, CreatedByUserId, UpdatedByUserId, CreatedAtUtc, UpdatedAtUtc)
VALUES
    (@id, @tenantId, @orgId, @caseNumber, @externalReference, NULL, @firstName, @lastName,
     @dob, NULL, NULL, @address, @status, @dateOfIncident, @openedAtUtc,
     @closedAtUtc, NULL, NULL, NULL, NULL, NULL,
     NULL, @notes, @createdByUserId, @updatedByUserId, @createdAtUtc, @updatedAtUtc);
""";
                await using var command = new MySqlCommand(sql, connection, transaction);
                command.Parameters.AddWithValue("@id", plan.TargetId.ToString());
                command.Parameters.AddWithValue("@tenantId", options.TenantId.ToString());
                command.Parameters.AddWithValue("@orgId", options.OrgId.ToString());
                command.Parameters.AddWithValue("@caseNumber", plan.CaseNumber);
                command.Parameters.AddWithValue("@externalReference", $"SL-CORE:{CaseTable}:{plan.Source.Id}");
                command.Parameters.AddWithValue("@firstName", Truncate(plan.Source.FirstName.Trim(), 100));
                command.Parameters.AddWithValue("@lastName", Truncate(plan.Source.LastName.Trim(), 100));
                AddNullable(command, "@dob", plan.Source.Dob?.ToDateTime(TimeOnly.MinValue));
                AddNullable(command, "@address", BuildAddress(plan.Source));
                command.Parameters.AddWithValue("@status", MapCaseStatus(plan.Source.Status));
                AddNullable(command, "@dateOfIncident", dateOfLoss?.ToDateTime(TimeOnly.MinValue));
                AddNullable(command, "@openedAtUtc", plan.Source.CreatedAtUtc ?? DateTime.UtcNow);
                AddNullable(command, "@closedAtUtc", IsTerminalCaseStatus(plan.Source.Status) ? plan.Source.UpdatedAtUtc : null);
                AddNullable(command, "@notes", BuildCaseNotes(plan.Source));
                command.Parameters.AddWithValue("@createdByUserId", options.MigrationUserId.ToString());
                command.Parameters.AddWithValue("@updatedByUserId", options.MigrationUserId.ToString());
                command.Parameters.AddWithValue("@createdAtUtc", plan.Source.CreatedAtUtc ?? DateTime.UtcNow);
                command.Parameters.AddWithValue("@updatedAtUtc", plan.Source.UpdatedAtUtc ?? plan.Source.CreatedAtUtc ?? DateTime.UtcNow);
                await command.ExecuteNonQueryAsync();
                await InsertCrosswalkAsync(transaction, runId, CaseTable, plan.Source.Id, "Case", plan.TargetId, plan.Source.SourceHash);
            }
            await transaction.CommitAsync();
            return plans.Count;
        }

        private async Task<int> ImportLiensAsync(Guid runId, IReadOnlyList<LienPlan> plans)
        {
            if (plans.Count == 0) return 0;
            await using var transaction = await connection.BeginTransactionAsync();
            foreach (var plan in plans)
            {
                var incidentDate = TryParseDate(plan.Source.DateOfLoss, out var parsedDate) ? parsedDate : null;
                var originalAmount = plan.Amounts.Value(options.LienAmountSource!.Value);
                const string sql = """
INSERT INTO liens_Liens
    (Id, TenantId, OrgId, LienNumber, ExternalReference, LienType, Status, CaseId, FacilityId,
     SubjectPartyId, SubjectFirstName, SubjectLastName, IsConfidential, OriginalAmount, CurrentBalance,
     OfferPrice, PurchasePrice, PayoffAmount, Jurisdiction, Description, Notes, IncidentDate,
     InitialServiceDate, EndServiceDate, IsBulk, IsServicing, OpenedAtUtc, ClosedAtUtc,
     SellingOrgId, BuyingOrgId, HoldingOrgId, SellerStatus, ListingVisibility,
     CreatedByUserId, UpdatedByUserId, CreatedAtUtc, UpdatedAtUtc)
VALUES
    (@id, @tenantId, @orgId, @lienNumber, @externalReference, 'MedicalLien', @status, @caseId, NULL,
     NULL, @subjectFirstName, @subjectLastName, 0, @originalAmount, @currentBalance,
     NULL, @purchasePrice, NULL, @jurisdiction, NULL, @notes, @incidentDate,
     @initialServiceDate, @endServiceDate, @isBulk, @isServicing, @openedAtUtc, @closedAtUtc,
     @sellingOrgId, NULL, NULL, 'Draft', 'Private',
     @createdByUserId, @updatedByUserId, @createdAtUtc, @updatedAtUtc);
""";
                await using var command = new MySqlCommand(sql, connection, transaction);
                command.Parameters.AddWithValue("@id", plan.TargetId.ToString());
                command.Parameters.AddWithValue("@tenantId", options.TenantId.ToString());
                command.Parameters.AddWithValue("@orgId", options.OrgId.ToString());
                command.Parameters.AddWithValue("@lienNumber", plan.LienNumber);
                command.Parameters.AddWithValue("@externalReference", $"SL-CORE:{LienTable}:{plan.Source.Id}");
                command.Parameters.AddWithValue("@status", MapLienStatus(plan.Source.Status));
                command.Parameters.AddWithValue("@caseId", plan.TargetCaseId.ToString());
                AddNullable(command, "@subjectFirstName", TruncateOrNull(plan.Source.SubjectFirstName, 100));
                AddNullable(command, "@subjectLastName", TruncateOrNull(plan.Source.SubjectLastName, 100));
                command.Parameters.AddWithValue("@originalAmount", originalAmount);
                command.Parameters.AddWithValue("@currentBalance", originalAmount);
                AddNullable(command, "@purchasePrice", plan.Amounts.PurchaseValues > 0 ? plan.Amounts.Purchase : null);
                AddNullable(command, "@jurisdiction", TruncateOrNull(plan.Source.Jurisdiction, 100));
                AddNullable(command, "@notes", TruncateOrNull(plan.Source.Notes, 4000));
                AddNullable(command, "@incidentDate", incidentDate?.ToDateTime(TimeOnly.MinValue));
                AddNullable(command, "@initialServiceDate", plan.Source.InitialServiceDate?.ToDateTime(TimeOnly.MinValue));
                AddNullable(command, "@endServiceDate", plan.Source.EndServiceDate?.ToDateTime(TimeOnly.MinValue));
                AddNullable(command, "@isBulk", NormalizeFlag(plan.Source.IsBulk));
                AddNullable(command, "@isServicing", NormalizeFlag(plan.Source.IsServicing));
                AddNullable(command, "@openedAtUtc", plan.Source.CreatedAtUtc ?? DateTime.UtcNow);
                AddNullable(command, "@closedAtUtc", IsTerminalLienStatus(plan.Source.Status) ? plan.Source.UpdatedAtUtc : null);
                command.Parameters.AddWithValue("@sellingOrgId", options.OrgId.ToString());
                command.Parameters.AddWithValue("@createdByUserId", options.MigrationUserId.ToString());
                command.Parameters.AddWithValue("@updatedByUserId", options.MigrationUserId.ToString());
                command.Parameters.AddWithValue("@createdAtUtc", plan.Source.CreatedAtUtc ?? DateTime.UtcNow);
                command.Parameters.AddWithValue("@updatedAtUtc", plan.Source.UpdatedAtUtc ?? plan.Source.CreatedAtUtc ?? DateTime.UtcNow);
                await command.ExecuteNonQueryAsync();
                await InsertCrosswalkAsync(transaction, runId, LienTable, plan.Source.Id, "Lien", plan.TargetId, plan.SourceHash);
            }
            await transaction.CommitAsync();
            return plans.Count;
        }

        private async Task<int> ImportNotesAsync(Guid runId, IReadOnlyList<NotePlan> plans)
        {
            if (plans.Count == 0) return 0;
            await using var transaction = await connection.BeginTransactionAsync();
            foreach (var plan in plans)
            {
                const string sql = """
INSERT INTO liens_CaseNotes
    (Id, CaseId, TenantId, Content, Category, IsPinned, CreatedByUserId, CreatedByName,
     IsEdited, IsDeleted, CreatedAtUtc, UpdatedAtUtc)
VALUES
    (@id, @caseId, @tenantId, @content, @category, 0, @createdByUserId, @createdByName,
     0, @isDeleted, @createdAtUtc, NULL);
""";
                await using var command = new MySqlCommand(sql, connection, transaction);
                command.Parameters.AddWithValue("@id", plan.TargetId.ToString());
                command.Parameters.AddWithValue("@caseId", plan.TargetCaseId.ToString());
                command.Parameters.AddWithValue("@tenantId", options.TenantId.ToString());
                command.Parameters.AddWithValue("@content", plan.Source.Content!.Trim());
                command.Parameters.AddWithValue("@category", plan.Source.LegacyUserId is null ? "general" : "feed");
                command.Parameters.AddWithValue("@createdByUserId", options.MigrationUserId.ToString());
                command.Parameters.AddWithValue("@createdByName", Truncate(string.IsNullOrWhiteSpace(plan.Source.CreatedByName) ? "Legacy SL-CORE" : plan.Source.CreatedByName.Trim(), 250));
                command.Parameters.AddWithValue("@isDeleted", string.Equals(plan.Source.IsDeleted, "Y", StringComparison.OrdinalIgnoreCase));
                command.Parameters.AddWithValue("@createdAtUtc", plan.Source.CreatedAtUtc ?? DateTime.UtcNow);
                await command.ExecuteNonQueryAsync();
                await InsertCrosswalkAsync(transaction, runId, CaseNoteTable, plan.Source.Id, "CaseNote", plan.TargetId, plan.SourceHash);
            }
            await transaction.CommitAsync();
            return plans.Count;
        }

        private async Task InsertCrosswalkAsync(MySqlTransaction transaction, Guid runId, string sourceTable, long legacyId, string targetEntity, Guid targetId, string sourceHash)
        {
            const string sql = """
INSERT INTO liens_LegacyIdCrosswalks
    (Id, TenantId, SourceSystem, SourceTable, LegacyId, TargetEntity, TargetId, SourceHash, ImportRunId, CreatedAtUtc)
VALUES
    (@id, @tenantId, @sourceSystem, @sourceTable, @legacyId, @targetEntity, @targetId, @sourceHash, @importRunId, UTC_TIMESTAMP(6));
""";
            await using var command = new MySqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("@id", Guid.CreateVersion7().ToString());
            command.Parameters.AddWithValue("@tenantId", options.TenantId.ToString());
            command.Parameters.AddWithValue("@sourceSystem", SourceSystem);
            command.Parameters.AddWithValue("@sourceTable", sourceTable);
            command.Parameters.AddWithValue("@legacyId", legacyId.ToString(CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("@targetEntity", targetEntity);
            command.Parameters.AddWithValue("@targetId", targetId.ToString());
            command.Parameters.AddWithValue("@sourceHash", sourceHash);
            command.Parameters.AddWithValue("@importRunId", runId.ToString());
            await command.ExecuteNonQueryAsync();
        }
    }

    private sealed record ImportResult(int CasesInserted, int LiensInserted, int CaseNotesInserted, string LienAmountSource);

    private static ParsedMoney ParseLegacyMoney(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return new ParsedMoney(null, false);
        var normalized = value.Trim().Replace(",", string.Empty, StringComparison.Ordinal).Replace("$", string.Empty, StringComparison.Ordinal);
        return decimal.TryParse(normalized, NumberStyles.Number | NumberStyles.AllowExponent | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var amount)
            ? new ParsedMoney(decimal.Round(amount, 2, MidpointRounding.AwayFromZero), false)
            : new ParsedMoney(null, true);
    }

    private static bool TryParseDate(string? value, out DateOnly? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(value)) return true;
        if (!DateOnly.TryParseExact(value.Trim(), ["yyyy-MM-dd", "MM/dd/yyyy", "M/d/yyyy"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            return false;
        result = parsed;
        return true;
    }

    private static string MapCaseStatus(string? sourceStatus) => sourceStatus?.Trim().ToUpperInvariant() switch
    {
        "N" or "P" or "PD" or "NEW" or "PROCESSING" or "PRE-DEMAND" or "PREDEMAND" => "PreDemand",
        "DS" or "DEMAND SENT" => "DemandSent",
        "NT" or "LP" or "LO" or "LC" or "NEGOTIATIONS" or "LITIGATION" or "LITIGATION (PENDING)" or "LITIGATION (OPEN)" or "LITIGATION (CLOSED)" => "InNegotiation",
        "CS" or "CASE SETTLED" => "CaseSettled",
        "C" or "CLOSED" => "Closed",
        _ => "PreDemand"
    };

    private static bool IsTerminalCaseStatus(string? sourceStatus) => MapCaseStatus(sourceStatus) is "Closed" or "CaseSettled";

    private const decimal MaximumImportAmount = 9_999_999_999_999_999.99m;

    private static bool IsKnownCaseStatus(string? sourceStatus) => sourceStatus?.Trim().ToUpperInvariant() is null or ""
        or "N" or "P" or "PD" or "NEW" or "PROCESSING" or "PRE-DEMAND" or "PREDEMAND"
        or "DS" or "DEMAND SENT"
        or "NT" or "LP" or "LO" or "LC" or "NEGOTIATIONS" or "LITIGATION" or "LITIGATION (PENDING)" or "LITIGATION (OPEN)" or "LITIGATION (CLOSED)"
        or "CS" or "CASE SETTLED" or "C" or "CLOSED";

    private static bool IsSupportedCoreLienStatus(string? sourceStatus) => sourceStatus?.Trim().ToUpperInvariant() is null or "" or "DRAFT" or "OPEN" or "ACTIVE";

    private static string MapLienStatus(string? sourceStatus) => sourceStatus?.Trim().ToUpperInvariant() switch
    {
        "OPEN" => "Active",
        "" or null => "Draft",
        "ACTIVE" => "Active",
        "DRAFT" => "Draft",
        _ => throw new InvalidOperationException("Unsupported lien status passed import preflight.")
    };

    private static bool IsTerminalLienStatus(string? sourceStatus) => false;

    private static string? NormalizeFlag(string? value) => value?.Trim().ToUpperInvariant() switch
    {
        "Y" or "YES" => "Yes",
        "N" or "NO" => "No",
        _ => null
    };

    private static string? BuildAddress(LegacyCase source)
    {
        var parts = new[] { source.Address, source.City, source.State, source.Zip }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim());
        var address = string.Join(", ", parts);
        return string.IsNullOrWhiteSpace(address) ? null : Truncate(address, 500);
    }

    private static string? BuildCaseNotes(LegacyCase source)
    {
        var values = new List<string>();
        if (!string.IsNullOrWhiteSpace(source.Notes)) values.Add(source.Notes.Trim());
        if (!string.IsNullOrWhiteSpace(source.LawFirmId)) values.Add($"[legacy-meta] lawFirmLegacyId={source.LawFirmId.Trim()}");
        if (!string.IsNullOrWhiteSpace(source.CaseManagerId)) values.Add($"[legacy-meta] caseManagerLegacyId={source.CaseManagerId.Trim()}");
        if (!string.IsNullOrWhiteSpace(source.IsBulk)) values.Add($"[legacy-meta] isBulk={source.IsBulk.Trim()}");
        if (!string.IsNullOrWhiteSpace(source.IsServicing)) values.Add($"[legacy-meta] isServicing={source.IsServicing.Trim()}");
        var notes = string.Join("\n", values);
        return string.IsNullOrWhiteSpace(notes) ? null : Truncate(notes, 4000);
    }

    private static void AddNullable(MySqlCommand command, string name, object? value) => command.Parameters.AddWithValue(name, value ?? DBNull.Value);
    private static string? TruncateOrNull(string? value, int maxLength) => string.IsNullOrWhiteSpace(value) ? null : Truncate(value.Trim(), maxLength);
    private static string Truncate(string value, int maxLength) => value.Length <= maxLength ? value : value[..maxLength];
    private static string Key(string sourceTable, long legacyId) => Key(sourceTable, legacyId.ToString(CultureInfo.InvariantCulture));
    private static string Key(string sourceTable, string legacyId) => $"{sourceTable}\u001f{legacyId}";
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private readonly record struct ParsedMoney(decimal? Value, bool IsInvalid);

    private static decimal Value(this LienAmounts amounts, AmountSource source) => source == AmountSource.Billing ? amounts.Billing : amounts.Purchase;
    private static int ValueCount(this LienAmounts amounts, AmountSource source) => source == AmountSource.Billing ? amounts.BillingValues : amounts.PurchaseValues;
}
