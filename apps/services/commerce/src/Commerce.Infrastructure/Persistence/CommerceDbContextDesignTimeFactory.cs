using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Commerce.Infrastructure.Persistence;

/// <summary>
/// Design-time DbContext factory used by <c>dotnet ef migrations</c>.
/// Always selects the MySQL provider (with a fixed server version)
/// so migrations are scaffolded with relational column types,
/// regardless of whether a real connection string is available in
/// the environment. Never opens a connection.
/// </summary>
public sealed class CommerceDbContextDesignTimeFactory
    : IDesignTimeDbContextFactory<CommerceDbContext>
{
    public CommerceDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<CommerceDbContext>()
            .UseMySql(
                "Server=design-time;Database=commerce;User=root;Password=root",
                new MySqlServerVersion(new Version(8, 0, 36)))
            .Options;
        return new CommerceDbContext(options);
    }
}
