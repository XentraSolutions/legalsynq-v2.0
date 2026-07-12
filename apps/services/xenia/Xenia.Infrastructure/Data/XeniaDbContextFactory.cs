using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Xenia.Infrastructure.Data;

public sealed class XeniaDbContextFactory : IDesignTimeDbContextFactory<XeniaDbContext>
{
    public XeniaDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__XeniaDb")
            ?? Environment.GetEnvironmentVariable("XENIA_DB_CONNECTION_STRING")
            ?? "Server=localhost;Database=xenia_db;User=root;Password=;";

        var optionsBuilder = new DbContextOptionsBuilder<XeniaDbContext>();
        optionsBuilder.UseMySql(
            connectionString,
            new MySqlServerVersion(new Version(8, 0, 36)),
            options => options.EnableRetryOnFailure(3));

        return new XeniaDbContext(optionsBuilder.Options);
    }
}
