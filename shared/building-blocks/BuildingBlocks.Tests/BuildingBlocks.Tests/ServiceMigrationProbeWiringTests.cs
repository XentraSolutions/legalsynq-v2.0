using System.Text.RegularExpressions;

namespace BuildingBlocks.Tests;

/// <summary>
/// CI guard for Task #66.
///
/// Task #62/#65 introduced <see cref="BuildingBlocks.Diagnostics.MigrationCoverageProbe"/>,
/// the boot-time self-test that compares every EF-mapped table/column against
/// the live database schema and screams if a migration was committed without
/// its [Migration] attribute (the Task #58 regression class).
///
/// The probe is only useful if every .NET service actually wires it into
/// startup. Without this guard, a service could silently drop the
/// <c>MigrationCoverageProbe.RunAsync(...)</c> call and we'd only find out
/// in production. This fixture asserts on every build that each known
/// service's <c>Program.cs</c> calls <c>MigrationCoverageProbe.RunAsync(</c>
/// at least once. The probe itself does the model-vs-migrations comparison
/// at production startup; this guard's job is purely to ensure the call is
/// present so the probe can do that work.
///
/// Failing the assertion fails the build.
/// </summary>
public class ServiceMigrationProbeWiringTests
{
    // Single source of truth: every .NET service that owns a DbContext +
    // migrations and therefore must wire the probe at boot. Adding a new
    // service requires adding it here so this guard can't silently fall
    // out of date as the service catalogue grows.
    public static readonly (string Name, string ProgramRelPath)[] Services =
    {
        ("audit",         "apps/services/audit/Program.cs"),
        ("careconnect",   "apps/services/careconnect/CareConnect.Api/Program.cs"),
        ("comms",         "apps/services/comms/Comms.Api/Program.cs"),
        ("documents",     "apps/services/documents/Documents.Api/Program.cs"),
        ("flow",          "apps/services/flow/backend/src/Flow.Api/Program.cs"),
        ("fund",          "apps/services/fund/Fund.Api/Program.cs"),
        ("identity",      "apps/services/identity/Identity.Api/Program.cs"),
        ("liens",         "apps/services/liens/Liens.Api/Program.cs"),
        ("notifications", "apps/services/notifications/Notifications.Api/Program.cs"),
        ("reports",       "apps/services/reports/src/Reports.Api/Program.cs"),
    };

    public static IEnumerable<object[]> ServiceCases =>
        Services.Select(s => new object[] { s.Name, s.ProgramRelPath });

    [Theory]
    [MemberData(nameof(ServiceCases))]
    public void EveryService_WiresMigrationCoverageProbeInProgram(
        string name, string programRelPath)
    {
        var repoRoot = FindRepoRoot();
        var programPath = Path.Combine(repoRoot, programRelPath.Replace('/', Path.DirectorySeparatorChar));

        Assert.True(File.Exists(programPath),
            $"Expected service entry point at '{programPath}'. " +
            $"If the service moved or was renamed, update the Services list in {nameof(ServiceMigrationProbeWiringTests)}.");

        var source = File.ReadAllText(programPath);

        // Match either the fully-qualified call or a using-shortened call.
        // Allow whitespace before the `(` to be tolerant of formatting.
        var pattern = new Regex(@"MigrationCoverageProbe\s*\.\s*RunAsync\s*\(", RegexOptions.CultureInvariant);

        Assert.True(pattern.IsMatch(source),
            $"Service '{name}' ({programRelPath}) does not wire MigrationCoverageProbe.RunAsync(...) at boot. " +
            "Without this call the boot-time schema/EF-model self-test from Task #62 is silently disabled, " +
            "re-opening the Task #58 regression class (a migration committed without its [Migration] " +
            "attribute would silently leave the live schema out of sync with the EF model). " +
            "Add a `using var scope = app.Services.CreateScope(); var db = scope.ServiceProvider." +
            "GetRequiredService<TDbContext>(); await BuildingBlocks.Diagnostics." +
            "MigrationCoverageProbe.RunAsync(db, app.Logger);` block to Program.cs.");
    }

    /// <summary>
    /// Walks <c>apps/services/</c> under the repo root and returns the
    /// repo-relative path (sorted, forward-slash-separated) of every
    /// directory whose name is exactly <c>Migrations</c> (case-sensitive,
    /// which is the project-wide convention — no service uses a lowercase
    /// variant).  Results are sorted so that failure output is stable
    /// across environments.
    ///
    /// This replaces the former hand-maintained <c>MigrationDirs</c> array
    /// so that new services are covered automatically on their first
    /// migration commit without any manual update to this file.
    /// </summary>
    private static IEnumerable<string> DiscoverMigrationDirs(string repoRoot)
    {
        var servicesRoot = Path.Combine(repoRoot, "apps", "services");
        if (!Directory.Exists(servicesRoot))
            yield break;

        var dirs = Directory
            .EnumerateDirectories(servicesRoot, "Migrations", SearchOption.AllDirectories)
            .Select(d => Path.GetRelativePath(repoRoot, d).Replace(Path.DirectorySeparatorChar, '/'))
            .OrderBy(d => d, StringComparer.Ordinal);

        foreach (var rel in dirs)
            yield return rel;
    }

    private static readonly Regex MigrationClassPattern =
        new(@":\s*Migration\b", RegexOptions.CultureInvariant);

    private static readonly Regex MigrationAttributePattern =
        new(@"\[Migration\s*\(", RegexOptions.CultureInvariant);

    /// <summary>
    /// CI guard complementing <see cref="EveryService_WiresMigrationCoverageProbeInProgram"/>.
    ///
    /// The theory above checks every entry in the hand-maintained <see cref="Services"/>
    /// array, but it cannot catch a new service that adds a Migrations folder without
    /// also adding itself to that array.  This fact closes the gap: it walks
    /// <c>apps/services/</c> with <see cref="DiscoverMigrationDirs"/>, maps every
    /// migration directory to its service root (the first path segment beneath
    /// <c>apps/services/</c>), resolves the canonical API <c>Program.cs</c> via
    /// <see cref="FindApiProgramCs"/>, and asserts the
    /// <c>MigrationCoverageProbe.RunAsync(</c> call is present — with no manual
    /// update required when a new service adds its first migration.
    ///
    /// Services with multiple migration directories (e.g. sharded contexts) are
    /// deduplicated by the resolved <c>Program.cs</c> path so each entry point is
    /// only verified once.
    /// </summary>
    [Fact]
    public void EveryDiscoveredMigrationService_WiresMigrationCoverageProbeInProgram()
    {
        var repoRoot = FindRepoRoot();
        var servicesRoot = Path.GetFullPath(Path.Combine(repoRoot, "apps", "services"));

        var probePattern = new Regex(
            @"MigrationCoverageProbe\s*\.\s*RunAsync\s*\(",
            RegexOptions.CultureInvariant);

        var noProgramFound = new List<string>();
        var probeNotWired  = new List<string>();

        // Track already-checked Program.cs paths so a service with several
        // migration folders (sharded contexts, etc.) is only verified once.
        var checkedPrograms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var relMigrationDir in DiscoverMigrationDirs(repoRoot))
        {
            // Resolve the service root: the first directory beneath apps/services/.
            // e.g. "apps/services/careconnect/CareConnect.Infrastructure/Data/Migrations"
            //   →  "<repoRoot>/apps/services/careconnect"
            var parts = relMigrationDir.Split('/');
            if (parts.Length < 3)
                continue; // unexpected structure — "apps/services/<name>/..."

            var serviceRoot = Path.GetFullPath(
                Path.Combine(servicesRoot, parts[2]));

            var programPath = FindApiProgramCs(serviceRoot);

            if (programPath is null)
            {
                noProgramFound.Add(relMigrationDir);
                continue;
            }

            if (!checkedPrograms.Add(programPath))
                continue; // already verified for a sibling migration directory

            var source = File.ReadAllText(programPath);
            if (!probePattern.IsMatch(source))
            {
                var relProgram = Path.GetRelativePath(repoRoot, programPath)
                    .Replace(Path.DirectorySeparatorChar, '/');
                probeNotWired.Add(relProgram);
            }
        }

        var messages = new List<string>();

        if (noProgramFound.Count > 0)
            messages.Add(
                "Could not find a Program.cs for the following auto-discovered migration " +
                "directories. FindApiProgramCs searches the service root for a directory " +
                "whose name ends with 'Api' and falls back to a root-level Program.cs. " +
                "If the service layout differs, update FindApiProgramCs:\n\n" +
                string.Join("\n", noProgramFound));

        if (probeNotWired.Count > 0)
            messages.Add(
                "The following Program.cs files were auto-discovered via their Migrations " +
                "folder but do not wire MigrationCoverageProbe.RunAsync(...) at boot. " +
                "Without this call the boot-time schema/EF-model self-test from Task #62 " +
                "is silently disabled, re-opening the Task #58 regression class. " +
                "Add `await BuildingBlocks.Diagnostics.MigrationCoverageProbe.RunAsync(db, " +
                "app.Logger);` to the service's Program.cs:\n\n" +
                string.Join("\n", probeNotWired));

        Assert.True(messages.Count == 0, string.Join("\n\n", messages));
    }

    /// <summary>
    /// Locates the canonical API entry-point <c>Program.cs</c> within
    /// <paramref name="serviceRoot"/>.
    ///
    /// Resolution order:
    /// <list type="number">
    ///   <item>A <c>Program.cs</c> whose immediate parent directory name ends with
    ///   <c>Api</c> (case-insensitive) — covers the typical
    ///   <c>*.Api/Program.cs</c> layout used by most services.</item>
    ///   <item>A <c>Program.cs</c> located directly in <paramref name="serviceRoot"/>
    ///   — covers flat single-project services such as <c>audit</c>.</item>
    /// </list>
    ///
    /// Returns <c>null</c> when no suitable <c>Program.cs</c> is found so the
    /// caller can report an actionable error.
    /// </summary>
    private static string? FindApiProgramCs(string serviceRoot)
    {
        if (!Directory.Exists(serviceRoot))
            return null;

        var allPrograms = Directory
            .EnumerateFiles(serviceRoot, "Program.cs", SearchOption.AllDirectories)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (allPrograms.Count == 0)
            return null;

        // 1. Prefer Program.cs whose parent directory name ends with "Api".
        var apiProgram = allPrograms.FirstOrDefault(p =>
            new DirectoryInfo(p).Parent?.Name
                .EndsWith("Api", StringComparison.OrdinalIgnoreCase) == true);
        if (apiProgram is not null)
            return apiProgram;

        // 2. Fall back to a Program.cs sitting directly in the service root
        //    (single-project layout, e.g. audit/).
        var rootProgram = allPrograms.FirstOrDefault(p =>
            string.Equals(
                Path.GetFullPath(Path.GetDirectoryName(p)!),
                Path.GetFullPath(serviceRoot),
                StringComparison.OrdinalIgnoreCase));

        return rootProgram; // null if nothing matched
    }

    /// <summary>
    /// Static guard for Task #67.
    ///
    /// EF Core silently ignores a migration whose partial class has no
    /// <c>[Migration("...")]</c> attribute — the class is simply not
    /// discovered when <c>dotnet ef database update</c> runs.  This was
    /// the exact regression that broke a fresh-DB setup (Task #58).
    ///
    /// For every migration file (non-Designer, non-Snapshot) across the
    /// known service migration directories, this test asserts that the
    /// <c>[Migration("...")]</c> attribute is present either in the main
    /// file itself (single-file style) or in its companion
    /// <c>.Designer.cs</c>.  Failing this test means a recently added
    /// migration will be silently skipped on any clean database.
    /// </summary>
    [Fact]
    public void EveryMigrationFile_HasMigrationAttribute()
    {
        var repoRoot = FindRepoRoot();
        var failures = new List<string>();

        foreach (var relDir in DiscoverMigrationDirs(repoRoot))
        {
            var dir = Path.Combine(repoRoot, relDir.Replace('/', Path.DirectorySeparatorChar));

            if (!Directory.Exists(dir))
            {
                failures.Add($"Migration directory not found: {relDir}");
                continue;
            }

            var mainFiles = Directory.GetFiles(dir, "*.cs")
                .Where(f => !f.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase)
                         && !f.EndsWith("ModelSnapshot.cs", StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => f);

            foreach (var file in mainFiles)
            {
                var source = File.ReadAllText(file);

                // Only check files that actually declare a migration class.
                if (!MigrationClassPattern.IsMatch(source))
                    continue;

                // The [Migration("...")] attribute may live in the main file
                // (single-file style) or in the companion Designer.cs.
                var hasAttribute = MigrationAttributePattern.IsMatch(source);

                if (!hasAttribute)
                {
                    var designerPath = Path.ChangeExtension(file, null) + ".Designer.cs";
                    if (File.Exists(designerPath))
                        hasAttribute = MigrationAttributePattern.IsMatch(File.ReadAllText(designerPath));
                }

                if (!hasAttribute)
                {
                    var relPath = Path.GetRelativePath(repoRoot, file)
                        .Replace(Path.DirectorySeparatorChar, '/');
                    failures.Add(relPath);
                }
            }
        }

        Assert.True(failures.Count == 0,
            "The following migration files are missing the [Migration(\"...\")] attribute. " +
            "EF Core silently skips migrations without this attribute, leaving fresh-database " +
            "schemas incomplete (Task #58 / Task #67 regression class). " +
            "Add [Migration(\"<timestamp>_<ClassName>\")] directly to the partial class " +
            "or regenerate its companion .Designer.cs file:\n\n" +
            string.Join("\n", failures));
    }

    // Walk up from the test assembly until we find the repo root marker.
    // We look for `LegalSynq.sln` so this test works whether it's run from
    // `dotnet test`, the IDE, or CI's working directory.
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "LegalSynq.sln")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException(
            "Could not locate repo root (LegalSynq.sln) walking up from " + AppContext.BaseDirectory);
    }
}
