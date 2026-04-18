using System.Text.RegularExpressions;
using BuildingBlocks.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using PlatformAuditEventService.Data;
using Testcontainers.MySql;

namespace PlatformAuditEventService.Tests.IntegrationTests;

/// <summary>
/// MySQL integration tests for the audit service's EF schema (Task #73).
///
/// Spins up a real MySQL 8 container via Testcontainers, applies the audit service's
/// EF migrations against it, and asserts that <see cref="MigrationCoverageProbe"/>
/// logs the "passed" line — proving the migrated schema and the EF model are in sync.
///
/// This test lives inside the audit service's own test project rather than in
/// BuildingBlocks.IntegrationTests because PlatformAuditEventService.csproj uses
/// Sdk="Microsoft.NET.Sdk.Web", and referencing a Web-SDK project from a plain
/// class-library test project causes dotnet restore to hang during project-graph
/// resolution. The audit.Tests project already uses the Web SDK project reference
/// via Microsoft.AspNetCore.Mvc.Testing, so it is not affected by this limitation.
///
/// CI note: these tests require Docker (for Testcontainers container spin-up).
/// </summary>
public sealed class AuditSchemaMigrationTests : IAsyncLifetime
{
    private static readonly MySqlServerVersion ServerVersion = new(new Version(8, 0, 0));

    private readonly MySqlContainer _container = new MySqlBuilder()
        .WithImage("mysql:8.0")
        .WithDatabase("master")
        .WithUsername("root")
        .WithPassword("Test1234!")
        .Build();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    // =========================================================================
    // Happy-path: apply all audit migrations and assert probe passes
    // =========================================================================

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Probe_Passes_Audit()
    {
        var cs = await CreateDatabaseAsync("it_audit");

        await using var db = new AuditEventDbContext(
            new DbContextOptionsBuilder<AuditEventDbContext>()
                .UseMySql(cs, ServerVersion)
                .Options);

        await db.Database.MigrateAsync();

        var logger = new CapturingLogger();
        await MigrationCoverageProbe.RunAsync(db, logger);

        var passedEntry = logger.Entries
            .SingleOrDefault(e => e.Level == LogLevel.Information && e.Message.Contains("passed"));

        Assert.True(passedEntry is not null,
            "Expected MigrationCoverageProbe to log 'passed' after MigrateAsync() on the audit " +
            "service schema, but it did not. " +
            $"Actual log entries: [{string.Join(" | ", logger.Entries.Select(e => $"{e.Level}: {e.Message}"))}]. " +
            "This usually means the EF model has columns or tables that were never added to a migration.");

        Assert.DoesNotContain(logger.Entries, e => e.Level >= LogLevel.Error);
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private async Task<string> CreateDatabaseAsync(string name)
    {
        var rootCs = _container.GetConnectionString();
        await using var conn = new MySqlConnection(rootCs);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"CREATE DATABASE IF NOT EXISTS `{name}`";
        await cmd.ExecuteNonQueryAsync();

        return Regex.Replace(rootCs, @"Database=[^;]+", $"Database={name}",
            RegexOptions.IgnoreCase);
    }

    // =========================================================================
    // Captured log assertion helper (mirrors BuildingBlocks.IntegrationTests)
    // =========================================================================

    private sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);

    private sealed class CapturingLogger : ILogger
    {
        public List<LogEntry> Entries { get; } = new();

        public IDisposable BeginScope<TState>(TState state) where TState : notnull
            => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Entries.Add(new LogEntry(logLevel, formatter(state, exception), exception));

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }
}
