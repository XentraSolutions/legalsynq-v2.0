using System.Globalization;
using MySqlConnector;

internal static class CaseRelationshipBackfill
{
    private const string ScriptFileName = "backfill-sl-core-case-relationships.sql";

    public static async Task<int> RunAsync(string[] args)
    {
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
            WriteUsage();
            return 2;
        }

        try
        {
            var scriptPath = FindScriptPath();
            var script = ApplySettings(await File.ReadAllTextAsync(scriptPath), options);

            var connectionString = new MySqlConnectionStringBuilder(options.TargetConnectionString)
            {
                AllowUserVariables = true
            }.ConnectionString;
            await using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();

            var coreLockName = $"liens:slcore:{options.TenantId:D}";
            var contactsLockName = $"liens:slcore:contacts:{options.TenantId:D}";
            var coreLockHeld = false;
            var contactsLockHeld = false;

            try
            {
                coreLockHeld = await TryAcquireLockAsync(connection, coreLockName);
                if (!coreLockHeld)
                    throw new InvalidOperationException("Could not acquire the SL-CORE core-import lock within 10 seconds. Retry after the active import or repair completes.");

                contactsLockHeld = await TryAcquireLockAsync(connection, contactsLockName);
                if (!contactsLockHeld)
                    throw new InvalidOperationException("Could not acquire the SL-CORE contacts-import lock within 10 seconds. Retry after the active import or repair completes.");

                try
                {
                    await using (var command = new MySqlCommand(script, connection))
                        await command.ExecuteNonQueryAsync();

                    var result = await ReadResultAsync(connection);
                    if (!options.Apply)
                    {
                        await RollbackAsync(connection);
                        WriteResult(options, result, committed: false);
                        return result.PreflightPassed ? 0 : 3;
                    }

                    if (!result.ApplySucceeded)
                    {
                        await RollbackAsync(connection);
                        WriteResult(options, result, committed: false);
                        return 3;
                    }

                    await CommitAsync(connection);
                    WriteResult(options, result, committed: true);
                    return 0;
                }
                catch
                {
                    await RollbackAsync(connection);
                    throw;
                }
            }
            finally
            {
                if (contactsLockHeld)
                    await ReleaseLockAsync(connection, contactsLockName);
                if (coreLockHeld)
                    await ReleaseLockAsync(connection, coreLockName);
            }
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"ERROR: {exception.Message}");
            return 1;
        }
    }

    private static async Task<Result> ReadResultAsync(MySqlConnection connection)
    {
        const string sql = """
            SELECT
                COALESCE(@preflight_ok, 0) AS PreflightPassed,
                COALESCE(@apply_permitted, 0) AS ApplyPermitted,
                COALESCE(@apply_succeeded, 0) AS ApplySucceeded,
                COALESCE(@case_update_count, 0) AS CaseRowsToUpdate,
                COALESCE(@facility_update_count, 0) AS LienFacilityRowsToUpdate,
                COALESCE(@case_rows_updated, 0) AS CaseRowsUpdated,
                COALESCE(@facility_rows_updated, 0) AS LienFacilityRowsUpdated,
                COALESCE(@case_conflict_count, 0) AS CaseConflicts,
                COALESCE(@facility_conflict_count, 0) AS FacilityConflicts,
                COALESCE(@case_postcondition_errors, 0) AS CasePostconditionErrors,
                COALESCE(@facility_postcondition_errors, 0) AS FacilityPostconditionErrors;
            """;

        await using var command = new MySqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            throw new InvalidOperationException("The relationship backfill did not return its validation summary.");

        return new Result(
            reader.GetInt32("PreflightPassed") == 1,
            reader.GetInt32("ApplyPermitted") == 1,
            reader.GetInt32("ApplySucceeded") == 1,
            reader.GetInt32("CaseRowsToUpdate"),
            reader.GetInt32("LienFacilityRowsToUpdate"),
            reader.GetInt32("CaseRowsUpdated"),
            reader.GetInt32("LienFacilityRowsUpdated"),
            reader.GetInt32("CaseConflicts"),
            reader.GetInt32("FacilityConflicts"),
            reader.GetInt32("CasePostconditionErrors"),
            reader.GetInt32("FacilityPostconditionErrors"));
    }

    private static async Task CommitAsync(MySqlConnection connection)
    {
        await using var command = new MySqlCommand("COMMIT;", connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task RollbackAsync(MySqlConnection connection)
    {
        try
        {
            await using var command = new MySqlCommand("ROLLBACK;", connection);
            await command.ExecuteNonQueryAsync();
        }
        catch (MySqlException)
        {
            // A connection-level failure has already rolled back the server transaction.
        }
    }

    private static async Task<bool> TryAcquireLockAsync(MySqlConnection connection, string lockName)
    {
        await using var command = new MySqlCommand("SELECT GET_LOCK(@lock_name, 10);", connection);
        command.Parameters.AddWithValue("@lock_name", lockName);
        return Convert.ToInt32(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture) == 1;
    }

    private static async Task ReleaseLockAsync(MySqlConnection connection, string lockName)
    {
        try
        {
            await using var command = new MySqlCommand("SELECT RELEASE_LOCK(@lock_name);", connection);
            command.Parameters.AddWithValue("@lock_name", lockName);
            await command.ExecuteNonQueryAsync();
        }
        catch (MySqlException)
        {
            // A connection-level failure releases the server advisory lock.
        }
    }

    private static string ApplySettings(string script, Options options)
    {
        const string defaultTenant = "SET @tenant_id = '019f6ae6-4348-784a-aae0-f4d636f843ad';";
        const string defaultApply = "SET @apply = 0;";
        const string defaultCaseCount = "SET @expected_case_updates = -1;";
        const string defaultFacilityCount = "SET @expected_lien_facility_updates = -1;";

        if (!script.Contains(defaultTenant, StringComparison.Ordinal)
            || !script.Contains(defaultApply, StringComparison.Ordinal)
            || !script.Contains(defaultCaseCount, StringComparison.Ordinal)
            || !script.Contains(defaultFacilityCount, StringComparison.Ordinal))
            throw new InvalidOperationException("The checked-in relationship-backfill SQL template has an unexpected header.");

        return script
            .Replace(defaultTenant, $"SET @tenant_id = '{options.TenantId:D}';", StringComparison.Ordinal)
            .Replace(defaultApply, $"SET @apply = {(options.Apply ? 1 : 0)};", StringComparison.Ordinal)
            .Replace(defaultCaseCount, $"SET @expected_case_updates = {options.ExpectedCaseUpdates};", StringComparison.Ordinal)
            .Replace(defaultFacilityCount, $"SET @expected_lien_facility_updates = {options.ExpectedLienFacilityUpdates};", StringComparison.Ordinal);
    }

    private static string FindScriptPath()
    {
        for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory()); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "scripts", "LegacyLiensImport", ScriptFileName);
            if (File.Exists(candidate))
                return candidate;
        }

        throw new InvalidOperationException($"Could not find scripts/LegacyLiensImport/{ScriptFileName}. Run this command from the repository or one of its subdirectories.");
    }

    private static void WriteResult(Options options, Result result, bool committed)
    {
        Console.WriteLine($"Tenant: {options.TenantId}");
        Console.WriteLine($"Mode: {(options.Apply ? "APPLY" : "DRY RUN")}");
        Console.WriteLine($"Preflight passed: {result.PreflightPassed}");
        Console.WriteLine($"Case rows to update: {result.CaseRowsToUpdate}");
        Console.WriteLine($"Lien facility rows to update: {result.LienFacilityRowsToUpdate}");
        Console.WriteLine($"Case conflicts: {result.CaseConflicts}");
        Console.WriteLine($"Facility conflicts: {result.FacilityConflicts}");
        Console.WriteLine($"Case postcondition errors: {result.CasePostconditionErrors}");
        Console.WriteLine($"Facility postcondition errors: {result.FacilityPostconditionErrors}");
        Console.WriteLine($"Transaction committed: {committed}");

        if (options.Apply && !result.ApplyPermitted)
            Console.Error.WriteLine("Apply was refused: use the exact dry-run counts and resolve every reported conflict.");
    }

    private static void WriteUsage()
    {
        Console.WriteLine("""
            Backfill omitted SL-CORE Program 1 case relationships.

            Required:
              --backfill-case-relationships
              --tenant-id <guid>
              --target-connection <connection-string>
                Or set ConnectionStrings__LiensDb.

            Apply controls:
              --apply
              --expected-case-updates <dry-run-count>
              --expected-lien-facility-updates <dry-run-count>

            Run without --apply first. The apply must use the two exact counts
            reported by that dry run. The target connection's selected database
            must be LS_QA_LIENS or LS_LIENS, and SL-CORE must be on the same server.
            """);
    }

    private sealed record Result(
        bool PreflightPassed,
        bool ApplyPermitted,
        bool ApplySucceeded,
        int CaseRowsToUpdate,
        int LienFacilityRowsToUpdate,
        int CaseRowsUpdated,
        int LienFacilityRowsUpdated,
        int CaseConflicts,
        int FacilityConflicts,
        int CasePostconditionErrors,
        int FacilityPostconditionErrors);

    private sealed record Options(
        Guid TenantId,
        string TargetConnectionString,
        bool Apply,
        int ExpectedCaseUpdates,
        int ExpectedLienFacilityUpdates)
    {
        public static Options Parse(string[] args)
        {
            var values = ParseArguments(args);
            if (!values.ContainsKey("backfill-case-relationships"))
                throw new ArgumentException("--backfill-case-relationships is required.");

            if (!Guid.TryParse(Require(values, "tenant-id"), out var tenantId) || tenantId == Guid.Empty)
                throw new ArgumentException("--tenant-id must be a non-empty GUID.");

            var apply = values.ContainsKey("apply");
            var targetConnection = Optional(values, "target-connection")
                ?? Environment.GetEnvironmentVariable("ConnectionStrings__LiensDb")
                ?? throw new ArgumentException("Provide --target-connection or set ConnectionStrings__LiensDb.");

            var expectedCaseUpdates = ParseExpected(Optional(values, "expected-case-updates"), "expected-case-updates", apply);
            var expectedLienFacilityUpdates = ParseExpected(Optional(values, "expected-lien-facility-updates"), "expected-lien-facility-updates", apply);
            return new Options(tenantId, targetConnection, apply, expectedCaseUpdates, expectedLienFacilityUpdates);
        }

        private static int ParseExpected(string? value, string name, bool apply)
        {
            if (!apply && value is null)
                return -1;
            if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
                || (apply ? parsed < 0 : parsed != -1))
                throw new ArgumentException(apply
                    ? $"--{name} must be a non-negative dry-run count when --apply is used."
                    : $"--{name} may only be omitted or set to -1 during a dry run.");
            return parsed;
        }

        private static Dictionary<string, string?> ParseArguments(string[] args)
        {
            var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            var flags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "backfill-case-relationships", "apply"
            };

            for (var index = 0; index < args.Length; index++)
            {
                var argument = args[index];
                if (!argument.StartsWith("--", StringComparison.Ordinal))
                    throw new ArgumentException($"Unexpected argument '{argument}'.");

                var key = argument[2..];
                if (!result.TryAdd(key, null))
                    throw new ArgumentException($"Argument --{key} was specified more than once.");
                if (flags.Contains(key))
                    continue;

                if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
                    throw new ArgumentException($"Argument --{key} requires a value.");
                result[key] = args[++index];
            }

            return result;
        }

        private static string Require(IReadOnlyDictionary<string, string?> values, string key) =>
            Optional(values, key) ?? throw new ArgumentException($"Argument --{key} is required.");

        private static string? Optional(IReadOnlyDictionary<string, string?> values, string key) =>
            values.TryGetValue(key, out var value) ? value : null;
    }
}
