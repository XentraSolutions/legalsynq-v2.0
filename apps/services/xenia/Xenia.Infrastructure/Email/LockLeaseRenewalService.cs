using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xenia.Application.Email.Ingestion;
using Xenia.Infrastructure.Persistence;

namespace Xenia.Infrastructure.Email;

/// <summary>
/// Background service that periodically renews held sync lock leases.
///
/// Runs every LeaseRenewalIntervalSeconds and extends all non-expired leases by
/// LeaseDurationSeconds. Records renewal failures on the lock entity so the alert
/// rule engine can emit stale-lock alerts when consecutive failures exceed the threshold.
///
/// Architecture:
/// - Uses a dedicated IServiceScope per cycle (avoids holding a scoped DbContext across ticks)
/// - Skips locks already past expiry (they are eligible for takeover by other workers)
/// - Does not cancel running sync operations — a lock expiry only means another worker
///   may take over; the original worker must validate its fencing token before committing state
/// - Failure to renew is logged and recorded on the lock entity; it does NOT stop the
///   background service from running again on the next interval
///
/// Security: this service does not read or write message bodies, raw cursors, or credentials.
/// </summary>
internal sealed class LockLeaseRenewalService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<XeniaIngestionOptions> _options;
    private readonly ILogger<LockLeaseRenewalService> _logger;

    private const int DefaultRenewalIntervalSeconds = 30;
    private const int DefaultLeaseDurationSeconds   = 120;
    private const int RenewalFailureThreshold       = 3;

    public LockLeaseRenewalService(
        IServiceScopeFactory scopeFactory,
        IOptions<XeniaIngestionOptions> options,
        ILogger<LockLeaseRenewalService> logger)
    {
        _scopeFactory = scopeFactory;
        _options      = options;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Lock lease renewal service started.");

        var renewalInterval = TimeSpan.FromSeconds(DefaultRenewalIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(renewalInterval, stoppingToken);
                await RenewExpiredLeaseWindowAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in lock lease renewal cycle.");
            }
        }

        _logger.LogInformation("Lock lease renewal service stopped.");
    }

    private async Task RenewExpiredLeaseWindowAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<XeniaDbContext>();

        var now    = DateTime.UtcNow;
        var window = now.AddSeconds(-DefaultRenewalIntervalSeconds * 2);

        // Load locks expiring within the next LeaseDuration window (held, not yet expired)
        var locks = await db.EmailSourceSyncLocks
            .Where(l => l.ExpiresAt > now &&
                        l.RenewedAt < now.AddSeconds(-(DefaultRenewalIntervalSeconds - 5)))
            .ToListAsync(ct);

        if (locks.Count == 0) return;

        foreach (var lk in locks)
        {
            try
            {
                var newExpiry = now.AddSeconds(DefaultLeaseDurationSeconds);
                lk.Renew(lk.LeaseOwnerId, newExpiry);
            }
            catch (InvalidOperationException ex)
            {
                var exceeded = lk.RecordRenewalFailure(RenewalFailureThreshold);
                _logger.LogWarning(ex,
                    "Lock renewal failed for source={SourceId} owner={Owner} failures={Count}",
                    lk.EmailSourceId, lk.LeaseOwnerId, lk.RenewalFailureCount);

                if (exceeded)
                {
                    _logger.LogError(
                        "Lock lease renewal failure threshold exceeded for source={SourceId}. " +
                        "Alert should be raised by rule engine.", lk.EmailSourceId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error renewing lock for source={SourceId}", lk.EmailSourceId);
            }
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist lock renewals.");
        }
    }
}
