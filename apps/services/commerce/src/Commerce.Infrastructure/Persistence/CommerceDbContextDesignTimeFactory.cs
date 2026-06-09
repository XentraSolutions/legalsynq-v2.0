using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Commerce.Infrastructure.Persistence;

/// <summary>
/// Design-time DbContext factory used by <c>dotnet ef migrations</c>.
/// Reads the connection string from configuration so EF commands can target
/// the intended database during local validation while still keeping a
/// deterministic fallback for discovery-only tooling scenarios.
/// </summary>
public sealed class CommerceDbContextDesignTimeFactory
    : IDesignTimeDbContextFactory<CommerceDbContext>
{
    public CommerceDbContext CreateDbContext(string[] args)
    {
        var config = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();

        var connectionString = config["Database:ConnectionString"]
            ?? "Server=localhost;Port=3306;Database=commerce;Uid=commerce;Pwd=commerce;";

        var options = new DbContextOptionsBuilder<CommerceDbContext>()
            .UseMySql(
                connectionString,
                new MySqlServerVersion(new Version(8, 0, 36)))
            .Options;
        return new CommerceDbContext(options);
    }
}
