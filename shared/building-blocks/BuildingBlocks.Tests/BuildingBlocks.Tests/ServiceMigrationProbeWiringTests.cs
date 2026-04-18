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
