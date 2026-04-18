using System.Text.RegularExpressions;
using MySqlConnector;
using Testcontainers.MySql;

namespace BuildingBlocks.IntegrationTests;

/// <summary>
/// Starts a single MySQL 8 container for the entire test collection and exposes
/// a helper that creates isolated per-test databases inside it.
/// </summary>
public sealed class MySqlContainerFixture : IAsyncLifetime
{
    private readonly MySqlContainer _container = new MySqlBuilder()
        .WithImage("mysql:8.0")
        .WithDatabase("master")
        .WithUsername("root")
        .WithPassword("Test1234!")
        .Build();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    /// <summary>
    /// Creates a fresh database with the supplied name and returns its
    /// fully-qualified connection string (suitable for Pomelo/EF).
    /// Calling this a second time with the same name is a no-op.
    /// </summary>
    public async Task<string> CreateDatabaseAsync(string name)
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
}

/// <summary>
/// Binds the test collection so that all integration tests share the same
/// MySQL container instance, keeping startup cost to a single container spin-up.
/// </summary>
[CollectionDefinition("MySqlCollection")]
public class MySqlCollection : ICollectionFixture<MySqlContainerFixture> { }
