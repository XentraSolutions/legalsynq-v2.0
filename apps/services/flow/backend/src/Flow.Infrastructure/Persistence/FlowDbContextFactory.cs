using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Flow.Infrastructure.Persistence;

public class FlowDbContextFactory : IDesignTimeDbContextFactory<FlowDbContext>
{
    public FlowDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("FLOW_DB_CONNECTION_STRING")
            ?? "Server=localhost;Database=flow_db;User=root;Password=;";

        var optionsBuilder = new DbContextOptionsBuilder<FlowDbContext>();
        optionsBuilder.UseMySql(
            connectionString,
            new MySqlServerVersion(new Version(8, 0, 36)),
            options => options.EnableRetryOnFailure(3));

        return new FlowDbContext(optionsBuilder.Options);
    }
}
