using Intake.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Intake.Api;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<IntakeDbContext>
{
    public IntakeDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("IntakeDatabase")
            ?? "server=localhost;port=3306;database=intake_design;user=root;password=root";

        var optionsBuilder = new DbContextOptionsBuilder<IntakeDbContext>();
        optionsBuilder.UseMySql(
            connectionString,
            new MySqlServerVersion(new Version(8, 0, 0)));

        return new IntakeDbContext(optionsBuilder.Options);
    }
}