using Commerce.Application.Integration.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Commerce.Infrastructure.Integration.TenantBilling;

/// <summary>
/// TB-INT-03 — background worker that drains
/// <see cref="ITenantBillingEntitlementPublishQueue"/> and dispatches
/// each item to <see cref="ITenantBillingEntitlementPublisher"/> in a
/// fresh DI scope. Always registered (even when auto-publish is
/// disabled) — when the queue refuses writes the worker simply waits
/// on an empty channel and consumes no resources.
///
/// <para>Never throws unhandled exceptions out of
/// <see cref="ExecuteAsync"/>: every per-item error is caught,
/// logged, and recorded as a failed-autopublish counter so a single
/// bad work item cannot crash the host process or stop the loop.</para>
/// </summary>
internal sealed class TenantBillingEntitlementPublishWorker : BackgroundService
{
    private readonly ITenantBillingEntitlementPublishQueue _queue;
    private readonly IServiceScopeFactory _scopes;
    private readonly TenantBillingPublisherMetrics _metrics;
    private readonly ILogger<TenantBillingEntitlementPublishWorker> _log;

    public TenantBillingEntitlementPublishWorker(
        ITenantBillingEntitlementPublishQueue queue,
        IServiceScopeFactory scopes,
        TenantBillingPublisherMetrics metrics,
        ILogger<TenantBillingEntitlementPublishWorker> log)
    {
        _queue = queue;
        _scopes = scopes;
        _metrics = metrics;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation(
            "TenantBilling auto-publish worker started. AutoPublishEnabled={AutoPublishEnabled} Capacity={Capacity}",
            _queue.AutoPublishEnabled, _queue.Capacity);

        try
        {
            await foreach (var item in _queue.ReadAllAsync(stoppingToken)
                .ConfigureAwait(false))
            {
                await ProcessOneAsync(item, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // graceful shutdown
        }
        catch (Exception ex)
        {
            _log.LogError(ex,
                "TenantBilling auto-publish worker stopped unexpectedly.");
        }

        _log.LogInformation("TenantBilling auto-publish worker stopped.");
    }

    private async Task ProcessOneAsync(
        TenantBillingEntitlementPublishWorkItem item,
        CancellationToken ct)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            var publisher = scope.ServiceProvider
                .GetRequiredService<ITenantBillingEntitlementPublisher>();

            _log.LogDebug(
                "Auto-publish started: BA={BillingAccountId} Trigger={TriggerSource} EnqueuedAtUtc={EnqueuedAtUtc} CorrelationId={CorrelationId} QueueDepth={QueueDepth}",
                item.BillingAccountId, item.TriggerSource,
                item.EnqueuedAtUtc, item.CorrelationId, _queue.Depth);

            var result = await publisher
                .PublishForBillingAccountAsync(item.BillingAccountId, ct)
                .ConfigureAwait(false);

            var outcome = result.Outcome.ToString().ToLowerInvariant();
            _metrics.RecordAutoPublishProcessed(item.TriggerSource, outcome, result.Reason);

            switch (result.Outcome)
            {
                case PublishEntitlementOutcome.Published:
                    _log.LogInformation(
                        "Auto-publish completed: BA={BillingAccountId} Trigger={TriggerSource} TenantId={TenantId} HttpStatus={HttpStatus} Attempts={Attempts} PublishedAtUtc={PublishedAtUtc}",
                        item.BillingAccountId, item.TriggerSource,
                        result.TenantId, result.HttpStatus, result.Attempts,
                        DateTime.UtcNow);
                    break;
                case PublishEntitlementOutcome.Skipped:
                    _log.LogInformation(
                        "Auto-publish skipped: BA={BillingAccountId} Trigger={TriggerSource} Reason={Reason}",
                        item.BillingAccountId, item.TriggerSource, result.Reason);
                    break;
                case PublishEntitlementOutcome.Failed:
                    _metrics.RecordAutoPublishFailed(item.TriggerSource, result.Reason);
                    _log.LogWarning(
                        "Auto-publish failed: BA={BillingAccountId} Trigger={TriggerSource} TenantId={TenantId} HttpStatus={HttpStatus} Reason={Reason} Attempts={Attempts}",
                        item.BillingAccountId, item.TriggerSource,
                        result.TenantId, result.HttpStatus, result.Reason, result.Attempts);
                    break;
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _metrics.RecordAutoPublishFailed(item.TriggerSource, "exception");
            _log.LogError(ex,
                "Auto-publish threw unhandled exception: BA={BillingAccountId} Trigger={TriggerSource}",
                item.BillingAccountId, item.TriggerSource);
        }
    }
}
