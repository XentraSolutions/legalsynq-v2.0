using CareConnect.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Internal;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CareConnect.Tests.Infrastructure;

public class CareConnectMigrationDiscoveryTests
{
    [Fact]
    public void MigrationsAssembly_Contains_HandAuthoredMigrationFixes()
    {
        using var services = BuildServices();
        using var scope = services.CreateScope();
        using var db = scope.ServiceProvider.GetRequiredService<CareConnectDbContext>();

        var migrationsAssembly = db.GetService<IMigrationsAssembly>();
        var migrationIds = migrationsAssembly.Migrations.Keys.ToHashSet(StringComparer.Ordinal);

        Assert.Contains("20260402000000_ReferralInProgressState", migrationIds);
        Assert.Contains("20260402010000_LSCC01004_BlockedProviderAccessLog", migrationIds);
        Assert.Contains("20260422000000_AddProviderReassignmentLog", migrationIds);
        Assert.Contains("20260422120000_AddProviderNpi", migrationIds);
        Assert.Contains("20260422130000_AddProviderAccessStage", migrationIds);
        Assert.Contains("20260423230000_AddProviderOnboardingRecoveryState", migrationIds);
        Assert.Contains("20260429120000_AddReferralComments", migrationIds);
        Assert.Contains("20260501000000_AddMissingProviderCategories", migrationIds);
    }

    [Fact]
    public void HandAuthoredMigrationTypes_AreBoundTo_CareConnectDbContext()
    {
        var migrationTypes = new[]
        {
            typeof(CareConnect.Infrastructure.Data.Migrations.ReferralInProgressState),
            typeof(CareConnect.Infrastructure.Data.Migrations.LSCC01004_BlockedProviderAccessLog),
            typeof(CareConnect.Infrastructure.Data.Migrations.AddProviderReassignmentLog),
            typeof(CareConnect.Infrastructure.Data.Migrations.AddProviderNpi),
            typeof(CareConnect.Infrastructure.Data.Migrations.AddProviderAccessStage),
            typeof(CareConnect.Infrastructure.Data.Migrations.AddProviderOnboardingRecoveryState),
            typeof(CareConnect.Infrastructure.Data.Migrations.AddReferralComments),
            typeof(CareConnect.Infrastructure.Data.Migrations.AddMissingProviderCategories),
        };

        foreach (var migrationType in migrationTypes)
        {
            var dbContextAttribute = migrationType.GetCustomAttributes(typeof(DbContextAttribute), inherit: false)
                .Cast<DbContextAttribute>()
                .SingleOrDefault();

            Assert.NotNull(dbContextAttribute);
            Assert.Equal(typeof(CareConnectDbContext), dbContextAttribute!.ContextType);

            var migrationAttribute = migrationType.GetCustomAttributes(typeof(MigrationAttribute), inherit: false)
                .Cast<MigrationAttribute>()
                .SingleOrDefault();

            Assert.NotNull(migrationAttribute);
            Assert.False(string.IsNullOrWhiteSpace(migrationAttribute!.Id));
        }
    }

    private static ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<CareConnectDbContext>(options =>
            options.UseMySql(
                "Server=127.0.0.1;Port=3306;Database=discovery_only;User=root;Password=ignored;",
                new MySqlServerVersion(new Version(8, 0, 0))));

        return services.BuildServiceProvider();
    }
}
