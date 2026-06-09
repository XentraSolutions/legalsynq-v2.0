using Microsoft.Extensions.Options;
using TenantBilling.Domain.Services;

namespace TenantBilling.Api.Hosting;

/// <summary>
/// Periodically sweeps every tenant's invoices for past-due Issued /
/// PartiallyPaid records and flips them into Overdue via
/// <see cref="IInvoiceService.MarkEligibleOverdueAsync"/>. Disabled by
/// default (see <see cref="InvoiceLifecycleOptions.OverdueJobEnabled"/>) —
/// when off the loop short-circuits and the service idles forever, which
/// is the desired posture for tests and local dev.
/// </summary>
public sealed class InvoiceOverdueHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IOptionsMonitor<InvoiceLifecycleOptions> _options;
    private readonly ILogger<InvoiceOverdueHostedService> _logger;

    public InvoiceOverdueHostedService(
        IServiceScopeFactory scopes,
        IOptionsMonitor<InvoiceLifecycleOptions> options,
        ILogger<InvoiceOverdueHostedService> logger)
    {
        _scopes = scopes;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Read once at startup so the disabled path is observable in logs.
        // We re-read inside the loop too so an operator can flip the flag
        // via configuration reload without a restart.
        var initial = _options.CurrentValue;
        if (!initial.OverdueJobEnabled)
        {
            _logger.LogInformation(
                "InvoiceOverdueHostedService disabled (InvoiceLifecycle:OverdueJobEnabled=false); not scheduling sweeps.");
            // Park forever (until shutdown). We don't poll the flag — by
            // design, enabling the job requires a process restart so the
            // operational footprint of "is the sweeper on?" is unambiguous.
            try { await Task.Delay(Timeout.Infinite, stoppingToken); }
            catch (OperationCanceledException) { /* shutdown */ }
            return;
        }

        _logger.LogInformation(
            "InvoiceOverdueHostedService enabled: interval={IntervalMinutes}min, batch={BatchSize}.",
            initial.OverdueJobIntervalMinutes, initial.OverdueBatchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown; let the loop exit.
                break;
            }
            catch (Exception ex)
            {
                // Top-level swallow: we never want a single tick's failure
                // to take the whole hosted service down. The service stays
                // alive and re-tries on the next tick.
                _logger.LogError(ex, "Overdue sweep tick failed; will retry next interval.");
            }

            var current = _options.CurrentValue;
            // Defensive lower-bound: a misconfigured zero or negative
            // interval would otherwise spin the loop continuously.
            var interval = TimeSpan.FromMinutes(Math.Max(1, current.OverdueJobIntervalMinutes));
            try { await Task.Delay(interval, stoppingToken); }
            catch (OperationCanceledException) { /* shutdown */ break; }
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        var current = _options.CurrentValue;
        var batchSize = Math.Max(1, current.OverdueBatchSize);

        // Each tick gets its own DI scope so the scoped DbContext is
        // properly disposed and we don't leak EF tracking state across
        // ticks (which would slowly bloat memory).
        using var scope = _scopes.CreateScope();
        var invoices = scope.ServiceProvider.GetRequiredService<IInvoiceService>();

        var nowUtc = DateTime.UtcNow;
        var result = await invoices.MarkEligibleOverdueAsync(
            tenantId: null,   // cross-tenant sweep
            nowUtc: nowUtc,
            take: batchSize,
            ct: ct);

        if (result.UpdatedCount == 0 && result.FailedCount == 0)
        {
            _logger.LogDebug("Overdue sweep tick complete: nothing eligible.");
        }
        else
        {
            _logger.LogInformation(
                "Overdue sweep tick complete: updated={Updated}, failed={Failed}.",
                result.UpdatedCount, result.FailedCount);
        }
    }
}
