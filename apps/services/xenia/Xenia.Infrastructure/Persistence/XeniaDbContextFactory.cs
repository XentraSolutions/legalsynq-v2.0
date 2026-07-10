using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Xenia.Infrastructure.Persistence;

/// <summary>
/// Design-time factory required for <c>dotnet ef migrations add</c> tooling.
/// Reads the connection string from environment variables or appsettings.json.
/// Not used at runtime — DI provides the DbContext in the live service.
/// </summary>
public sealed class XeniaDbContextFactory : IDesignTimeDbContextFactory<XeniaDbContext>
{
    public XeniaDbContext CreateDbContext(string[] args)
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = config.GetConnectionString("XeniaDb")
            ?? "Server=127.0.0.1;Port=3306;Database=xenia_db;User=xenia;Password=xenia;";

        var optionsBuilder = new DbContextOptionsBuilder<XeniaDbContext>();
        optionsBuilder.UseMySql(
            connectionString,
            new MySqlServerVersion(new Version(8, 0, 36)),
            o => o.MigrationsAssembly(typeof(XeniaDbContext).Assembly.GetName().Name));

        return new XeniaDbContext(optionsBuilder.Options);
    }
}
