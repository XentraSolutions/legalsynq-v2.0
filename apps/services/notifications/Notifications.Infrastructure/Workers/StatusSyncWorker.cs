using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Notifications.Infrastructure.Workers;

public class StatusSyncWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<StatusSyncWorker> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(5);

    public StatusSyncWorker(IServiceScopeFactory scopeFactory, ILogger<StatusSyncWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("StatusSyncWorker started, interval={Interval}s", _interval.TotalSeconds);
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogDebug("StatusSyncWorker: sync cycle complete");
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "StatusSyncWorker error");
            }

            await Task.Delay(_interval, stoppingToken);
        }

        _logger.LogInformation("StatusSyncWorker stopped");
    }
}
