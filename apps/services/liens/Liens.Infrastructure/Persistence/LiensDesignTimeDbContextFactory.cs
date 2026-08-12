using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Liens.Infrastructure.Persistence;

public sealed class LiensDesignTimeDbContextFactory : IDesignTimeDbContextFactory<LiensDbContext>
{
    private const string FallbackConnectionString =
        "Server=127.0.0.1;Port=3306;Database=liens_design_time;User=root;Password=;";

    public LiensDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("LiensDb")
            ?? FallbackConnectionString;

        var optionsBuilder = new DbContextOptionsBuilder<LiensDbContext>();
        optionsBuilder.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 0)));

        return new LiensDbContext(optionsBuilder.Options);
    }
}
