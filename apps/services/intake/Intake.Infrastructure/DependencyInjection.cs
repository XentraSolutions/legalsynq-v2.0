using Intake.Application.Configuration;
using Intake.Infrastructure.Audit;
using Intake.Infrastructure.Health;
using Intake.Infrastructure.Persistence;
using LegalSynq.AuditClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Intake.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("IntakeDatabase");

        services.AddDbContextFactory<IntakeDbContext>(options =>
        {
            // Development can start without a database so /health remains
            // useful. Readiness reports the missing/unreachable database.
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                options.UseMySql(
                    connectionString,
                    new MySqlServerVersion(new Version(8, 0, 0)));
            }
        });

        services.AddScoped<IIntakeConfigurationRepository, EfIntakeConfigurationRepository>();
        services.AddSingleton<IProcessingProfileRegistry, ProcessingProfileRegistry>();
        services.AddScoped<IIntakeConfigurationAuditSink, IntakeConfigurationAuditSink>();
        services.AddScoped<IIntakeConfigurationService, IntakeConfigurationService>();
        services.AddAuditEventClient(configuration);

        services.AddHealthChecks()
            .AddCheck(
                "process",
                () => HealthCheckResult.Healthy("Intake process is running"),
                tags: ["live"])
            .AddCheck<IntakeDatabaseHealthCheck>(
                "database",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["ready"]);

        return services;
    }
}