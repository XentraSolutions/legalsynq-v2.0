using Contracts.Commerce;

namespace BuildingBlocks.Commerce;

/// <summary>
/// Noop implementation of <see cref="ICommerceLifecycleNotifier"/> that
/// accepts all events without performing any I/O.
///
/// <para>
/// Always registered as the default. A real outbound HTTP or message-bus
/// implementation will replace this in a later phase once the Commerce
/// lifecycle ingest endpoint is stabilised.
/// </para>
/// </summary>
internal sealed class NoopCommerceLifecycleNotifier : ICommerceLifecycleNotifier
{
    public Task NotifyAsync(CommerceLifecycleEvent ev, CancellationToken ct = default)
        => Task.CompletedTask;
}
