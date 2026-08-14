using Intake.Application.Operations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Intake.Infrastructure.Operations;

public sealed class IntakeRecoveryWorker(
    IServiceScopeFactory scopeFactory,
    IntakeRecoveryOptions options,
    ILogger<IntakeRecoveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled)
        {
            logger.LogInformation("Intake recovery worker is disabled.");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(
            Math.Clamp(options.ScanIntervalSeconds, 5, 3600)));
        do
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider
                    .GetRequiredService<IIntakeRecoveryService>()
                    .RunScanAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception)
            {
                logger.LogError("Intake recovery worker iteration failed safely.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}