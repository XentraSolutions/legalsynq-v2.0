using Commerce.Application.Integration.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Commerce.Infrastructure.Integration.TenantBilling.Outbox;

/// <summary>
/// TB-INT-04 — durable outbox poller. Wakes every
/// <c>Commerce:TenantBilling:OutboxPollSeconds</c> and asks the
/// processor to drain a batch. When the outbox is disabled the
/// worker simply sleeps; it never exits early so toggling the
/// config is sufficient to enable processing without a redeploy.
///
/// <para>Never throws unhandled exceptions out of
/// <see cref="ExecuteAsync"/>: every loop-body exception is caught
/// and logged so a single bad batch cannot crash the host
/// process.</para>
/// </summary>
internal sealed class TenantBillingEntitlementOutboxWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IOptionsMonitor<TenantBillingClientOptions> _options;
    private readonly ILogger<TenantBillingEntitlementOutboxWorker> _log;

    public TenantBillingEntitlementOutboxWorker(
        IServiceScopeFactory scopes,
        IOptionsMonitor<TenantBillingClientOptions> options,
        ILogger<TenantBillingEntitlementOutboxWorker> log)
    {
        _scopes = scopes;
        _options = options;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var initial = _options.CurrentValue.Normalised();
        _log.LogInformation(
            "TenantBilling outbox worker started. OutboxEnabled={OutboxEnabled} PollSeconds={PollSeconds} BatchSize={BatchSize} MaxAttempts={MaxAttempts} RetryBaseDelaySeconds={RetryBaseDelaySeconds}",
            initial.OutboxEnabled, initial.OutboxPollSeconds, initial.OutboxBatchSize,
            initial.OutboxMaxAttempts, initial.OutboxRetryBaseDelaySeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            var opts = _options.CurrentValue.Normalised();
            try
            {
                if (opts.OutboxEnabled)
                {
                    await RunOneBatchAsync(opts.OutboxBatchSize, stoppingToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "TenantBilling outbox worker batch threw; will retry next tick.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(opts.OutboxPollSeconds), stoppingToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _log.LogInformation("TenantBilling outbox worker stopped.");
    }

    private async Task RunOneBatchAsync(int batchSize, CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var processor = scope.ServiceProvider
            .GetRequiredService<ITenantBillingEntitlementOutboxProcessor>();
        var result = await processor.ProcessDueAsync(batchSize, ct).ConfigureAwait(false);
        if (result.Considered == 0 && result.Recovered == 0) return;

        _log.LogDebug(
            "Outbox batch summary: Considered={Considered} Recovered={Recovered} Published={Published} Retried={Retried} Abandoned={Abandoned} Skipped={Skipped}",
            result.Considered, result.Recovered, result.Published, result.Retried, result.Abandoned, result.Skipped);
    }
}
