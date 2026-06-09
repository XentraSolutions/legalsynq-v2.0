using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace TenantBilling.Infrastructure.Data;

/// <summary>
/// Design-time factory used by `dotnet ef` so migrations can be added without
/// spinning up the host. Pins a known MySQL version so EF tooling can run
/// without a live MySQL server.
/// </summary>
public sealed class TenantBillingDbContextFactory : IDesignTimeDbContextFactory<TenantBillingDbContext>
{
    public TenantBillingDbContext CreateDbContext(string[] args)
    {
        var config = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();

        var connectionString = config["ConnectionStrings:DefaultConnection"]
            ?? "Server=localhost;Port=3306;Database=tenant_billing;Uid=tenant_billing;Pwd=tenant_billing;";

        var serverVersion = new MySqlServerVersion(new Version(8, 0, 36));

        var optionsBuilder = new DbContextOptionsBuilder<TenantBillingDbContext>();
        optionsBuilder.UseMySql(
            connectionString,
            serverVersion,
            mysql => mysql.MigrationsAssembly(typeof(TenantBillingDbContext).Assembly.FullName));

        return new TenantBillingDbContext(optionsBuilder.Options);
    }
}
