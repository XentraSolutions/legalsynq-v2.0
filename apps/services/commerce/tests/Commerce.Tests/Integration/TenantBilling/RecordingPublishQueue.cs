using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Commerce.Application.Integration.Abstractions;

namespace Commerce.Tests.Integration.TenantBilling;

/// <summary>
/// TB-INT-03 — test double for
/// <see cref="ITenantBillingEntitlementPublishQueue"/> that records
/// every enqueue call so trigger-site tests can assert on labels and
/// ordering without running the worker.
/// </summary>
internal sealed class RecordingPublishQueue : ITenantBillingEntitlementPublishQueue
{
    private readonly EnqueueResult _resultToReturn;

    public RecordingPublishQueue(
        bool autoPublishEnabled = true,
        int capacity = 1000,
        EnqueueResult resultToReturn = EnqueueResult.Accepted)
    {
        AutoPublishEnabled = autoPublishEnabled;
        Capacity = capacity;
        _resultToReturn = resultToReturn;
    }

    public ConcurrentQueue<TenantBillingEntitlementPublishWorkItem> Enqueued { get; } = new();
    public bool AutoPublishEnabled { get; }
    public int Capacity { get; }
    public int Depth => Enqueued.Count;

    public EnqueueResult Enqueue(TenantBillingEntitlementPublishWorkItem item)
    {
        if (item.BillingAccountId == Guid.Empty
            || string.IsNullOrWhiteSpace(item.TriggerSource))
        {
            return EnqueueResult.Invalid;
        }
        if (!AutoPublishEnabled) return EnqueueResult.SkippedDisabled;
        if (_resultToReturn == EnqueueResult.Accepted)
        {
            Enqueued.Enqueue(item);
        }
        return _resultToReturn;
    }

    public async IAsyncEnumerable<TenantBillingEntitlementPublishWorkItem> ReadAllAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Snapshot drain — sufficient for tests that don't use the worker.
        foreach (var i in Enqueued)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return i;
        }
        await Task.CompletedTask;
    }
}
