using System.Threading.Channels;
using Commerce.Application.Integration.Abstractions;
using Microsoft.Extensions.Options;

namespace Commerce.Infrastructure.Integration.TenantBilling;

/// <summary>
/// TB-INT-03 — bounded in-process implementation of
/// <see cref="ITenantBillingEntitlementPublishQueue"/> backed by a
/// <see cref="Channel{T}"/>. Capacity is taken from
/// <see cref="TenantBillingClientOptions.AutoPublishQueueCapacity"/>
/// (clamped). When the queue is full <see cref="Enqueue"/> returns
/// <see cref="EnqueueResult.DroppedQueueFull"/> immediately rather than
/// blocking the trigger site (we use
/// <see cref="BoundedChannelFullMode.Wait"/> + a non-blocking
/// <see cref="ChannelWriter{T}.TryWrite"/> so the awaiting/writer
/// semantics never delay a Commerce commit).
///
/// <para>Singleton; single-reader (the hosted worker), multi-writer
/// (trigger sites in scoped services).</para>
/// </summary>
internal sealed class BoundedTenantBillingEntitlementPublishQueue
    : ITenantBillingEntitlementPublishQueue
{
    private readonly Channel<TenantBillingEntitlementPublishWorkItem> _channel;
    private readonly TenantBillingClientOptions _opts;

    public BoundedTenantBillingEntitlementPublishQueue(
        IOptions<TenantBillingClientOptions> options)
    {
        _opts = (options ?? throw new ArgumentNullException(nameof(options)))
            .Value.Normalised();
        Capacity = _opts.AutoPublishQueueCapacity;
        _channel = Channel.CreateBounded<TenantBillingEntitlementPublishWorkItem>(
            new BoundedChannelOptions(Capacity)
            {
                // We want TryWrite to return false when full instead of
                // silently dropping, so the trigger site can record an
                // accurate "dropped-queue-full" metric.
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false,
            });
    }

    public int Capacity { get; }

    public int Depth => _channel.Reader.Count;

    public bool AutoPublishEnabled => _opts.AutoPublishEnabled;

    public EnqueueResult Enqueue(TenantBillingEntitlementPublishWorkItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.BillingAccountId == Guid.Empty
            || string.IsNullOrWhiteSpace(item.TriggerSource))
        {
            return EnqueueResult.Invalid;
        }

        if (!AutoPublishEnabled)
        {
            return EnqueueResult.SkippedDisabled;
        }

        return _channel.Writer.TryWrite(item)
            ? EnqueueResult.Accepted
            : EnqueueResult.DroppedQueueFull;
    }

    public IAsyncEnumerable<TenantBillingEntitlementPublishWorkItem> ReadAllAsync(
        CancellationToken cancellationToken)
        => _channel.Reader.ReadAllAsync(cancellationToken);

    /// <summary>
    /// Test/host hook to complete the channel so consumers exit their
    /// async-foreach. Not exposed on the public interface.
    /// </summary>
    internal void Complete() => _channel.Writer.TryComplete();
}
