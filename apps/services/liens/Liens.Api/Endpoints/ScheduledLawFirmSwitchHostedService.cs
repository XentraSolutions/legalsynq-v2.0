using Liens.Api.Serialization;
using Liens.Infrastructure.Persistence;

namespace Liens.Api.Endpoints;

internal sealed class ScheduledLawFirmSwitchHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<ScheduledLawFirmSwitchHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ApplyDueSwitchesAsync(stoppingToken);

        using var timer = new PeriodicTimer(PollInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await ApplyDueSwitchesAsync(stoppingToken);
    }

    private async Task ApplyDueSwitchesAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            var pacificToday = DateOnly.FromDateTime(PacificTimeHelper.Convert(DateTime.UtcNow).Date);
            var applied = await LawFirmChangeHistory.ApplyDueScheduledSwitchesAsync(db, pacificToday, ct);
            if (applied > 0)
                logger.LogInformation("Applied {Count} scheduled law firm switches.", applied);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Normal host shutdown.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unable to apply scheduled law firm switches; the next poll will retry.");
        }
    }
}
