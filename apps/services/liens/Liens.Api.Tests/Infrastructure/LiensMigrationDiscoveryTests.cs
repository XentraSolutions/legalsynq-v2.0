using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace Liens.Api.Tests.Infrastructure;

public class LiensMigrationDiscoveryTests
{
    [Fact]
    public void MigrationsAssembly_Contains_HandAuthoredMigrationFixes()
    {
        using var services = BuildServices();
        using var scope = services.CreateScope();
        using var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();

        var migrationsAssembly = db.GetService<IMigrationsAssembly>();
        var migrationIds = migrationsAssembly.Migrations.Keys.ToHashSet(StringComparer.Ordinal);

        Assert.Contains("20260418200000_AddWorkflowTransitions", migrationIds);
        Assert.Contains("20260420000001_AddTaskGovernanceSettings", migrationIds);
        Assert.Contains("20260420000002_AddTaskFlowLinkage", migrationIds);
        Assert.Contains("20260421000002_DropLiensConfigTables", migrationIds);
        Assert.Contains("20260513000001_SeedLookupValues", migrationIds);
        Assert.Contains("20260625000001_AddLienLegacyMedicalFields", migrationIds);
        Assert.Contains("20260629164601_AddFacilityLinkedContactSubtype", migrationIds);
    }

    [Fact]
    public void HandAuthoredMigrationTypes_AreBoundTo_LiensDbContext()
    {
        var migrationTypes = new[]
        {
            typeof(Liens.Infrastructure.Persistence.Migrations.AddWorkflowTransitions),
            typeof(Liens.Infrastructure.Persistence.Migrations.AddTaskGovernanceSettings),
            typeof(Liens.Infrastructure.Persistence.Migrations.AddTaskFlowLinkage),
            typeof(Liens.Infrastructure.Persistence.Migrations.DropLiensConfigTables),
            typeof(Liens.Infrastructure.Persistence.Migrations.SeedLookupValues),
            typeof(Liens.Infrastructure.Persistence.Migrations.AddLienLegacyMedicalFields),
            typeof(Liens.Infrastructure.Persistence.Migrations.AddFacilityLinkedContactSubtype),
        };

        foreach (var migrationType in migrationTypes)
        {
            var dbContextAttribute = migrationType.GetCustomAttributes(typeof(DbContextAttribute), inherit: false)
                .Cast<DbContextAttribute>()
                .SingleOrDefault();

            Assert.NotNull(dbContextAttribute);
            Assert.Equal(typeof(LiensDbContext), dbContextAttribute!.ContextType);

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
        services.AddDbContext<LiensDbContext>(options =>
            options.UseMySql(
                "Server=127.0.0.1;Port=3306;Database=discovery_only;User=root;Password=ignored;",
                new MySqlServerVersion(new Version(8, 0, 0))));

        return services.BuildServiceProvider();
    }
}
