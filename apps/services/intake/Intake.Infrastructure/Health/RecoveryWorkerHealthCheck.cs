using Intake.Application.Operations;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Intake.Infrastructure.Health;

public sealed class RecoveryWorkerHealthCheck(
    RecoveryWorkerState state,
    IntakeRecoveryOptions options) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var snapshot = state.Snapshot();
        if (!options.Enabled)
            return Task.FromResult(HealthCheckResult.Healthy("Recovery worker is disabled."));
        if (snapshot.LastScanAt is null)
            return Task.FromResult(HealthCheckResult.Healthy("Recovery worker is starting."));
        if (snapshot.LastFailureCode is not null &&
            (snapshot.LastSuccessfulScanAt is null ||
             snapshot.LastSuccessfulScanAt < DateTimeOffset.UtcNow.AddMinutes(-5)))
            return Task.FromResult(HealthCheckResult.Degraded(
                "Recovery worker has not completed a successful scan recently."));
        return Task.FromResult(HealthCheckResult.Healthy("Recovery worker is operating."));
    }
}