using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.EnvironmentVariables;

namespace Commerce.Infrastructure.Persistence;

/// <summary>
/// Design-time factory used by `dotnet ef` tooling so migrations can be added
/// without spinning up the host. Reads connection string from environment or
/// the API project's appsettings.
/// </summary>
public sealed class CommerceDbContextFactory : IDesignTimeDbContextFactory<CommerceDbContext>
{
    public CommerceDbContext CreateDbContext(string[] args)
    {
        var config = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();

        var connectionString = config["Database:ConnectionString"]
            ?? "Server=localhost;Port=3306;Database=commerce;Uid=commerce;Pwd=commerce;";

        // Design-time only: pin a known server version so EF tooling can run
        // migrations without a live MySQL server (e.g. in CI). Runtime uses
        // ServerVersion.AutoDetect via DependencyInjection.
        var serverVersion = new MySqlServerVersion(new Version(8, 0, 36));

        var optionsBuilder = new DbContextOptionsBuilder<CommerceDbContext>();
        optionsBuilder.UseMySql(
            connectionString,
            serverVersion,
            mysql => mysql.MigrationsAssembly(typeof(CommerceDbContext).Assembly.FullName));

        return new CommerceDbContext(optionsBuilder.Options);
    }
}
