using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xenia.Application.Email.Ingestion;
using Xenia.Domain.Email;

namespace Xenia.Infrastructure.Email;

/// <summary>
/// Background worker that periodically syncs all enabled email sources.
///
/// Disabled by default (<see cref="XeniaIngestionOptions.WorkerEnabled"/> = false).
/// Manual sync via API is always available regardless of this setting.
///
/// The worker respects per-source backoff (NextEligibleSyncAt) and concurrency limits.
/// </summary>
internal sealed class EmailIngestionWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly XeniaIngestionOptions _opts;
    private readonly ILogger<EmailIngestionWorker> _logger;

    public EmailIngestionWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<XeniaIngestionOptions> opts,
        ILogger<EmailIngestionWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _opts         = opts.Value;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_opts.WorkerEnabled)
        {
            _logger.LogInformation("EmailIngestionWorker is disabled (WorkerEnabled=false). Background sync will not run.");
            return;
        }

        _logger.LogInformation(
            "EmailIngestionWorker starting. Interval={Interval} Concurrency={Concurrency}",
            _opts.WorkerInterval, _opts.WorkerConcurrency);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessDueSources(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "EmailIngestionWorker cycle failed.");
            }

            try { await Task.Delay(_opts.WorkerInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }

        _logger.LogInformation("EmailIngestionWorker stopped.");
    }

    private async Task ProcessDueSources(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Persistence.XeniaDbContext>();

        var now = DateTime.UtcNow;
        var dueSources = await db.EmailSyncStates
            .AsNoTracking()
            .Where(s => s.NextEligibleSyncAt == null || s.NextEligibleSyncAt <= now)
            .Take(_opts.WorkerConcurrency * 2)
            .Select(s => new { s.TenantId, s.EmailSourceId })
            .ToListAsync(ct);

        using var semaphore = new SemaphoreSlim(_opts.WorkerConcurrency, _opts.WorkerConcurrency);

        var tasks = dueSources.Select(async s =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                using var innerScope = _scopeFactory.CreateScope();
                var syncService = innerScope.ServiceProvider.GetRequiredService<IEmailSyncService>();
                await syncService.ExecuteSyncAsync(
                    s.TenantId, s.EmailSourceId,
                    IngestionRunTriggerType.Scheduled,
                    actorId: null, correlationId: null, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Worker sync failed for sourceId={SourceId}", s.EmailSourceId);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
    }
}
