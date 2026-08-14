using Intake.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Intake.Infrastructure.Health;

public sealed class IntakeDatabaseHealthCheck(
    IDbContextFactory<IntakeDbContext> dbContextFactory,
    ILogger<IntakeDatabaseHealthCheck> logger) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var canConnect = await db.Database.CanConnectAsync(cancellationToken);

            return canConnect
                ? HealthCheckResult.Healthy("Intake database reachable")
                : HealthCheckResult.Unhealthy("Intake database is not reachable");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Intake database readiness check failed");
            return HealthCheckResult.Unhealthy("Intake database check failed");
        }
    }
}