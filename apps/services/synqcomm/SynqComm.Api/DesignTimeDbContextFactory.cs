using SynqComm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SynqComm.Api;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<SynqCommDbContext>
{
    public SynqCommDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("SynqCommDb")
            ?? throw new InvalidOperationException("Connection string 'SynqCommDb' is not configured.");

        var optionsBuilder = new DbContextOptionsBuilder<SynqCommDbContext>();
        optionsBuilder.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 0)));

        return new SynqCommDbContext(optionsBuilder.Options);
    }
}
