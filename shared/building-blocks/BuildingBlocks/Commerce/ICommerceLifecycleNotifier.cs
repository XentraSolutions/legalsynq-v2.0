using Contracts.Commerce;

namespace BuildingBlocks.Commerce;

/// <summary>
/// Sends Commerce-relevant tenant lifecycle events from host services
/// (Identity, Tenant) to Commerce or to a message bus that Commerce
/// subscribes to.
///
/// <para>
/// This is the reverse direction of Commerce's existing
/// <c>IProvisioningHookPublisher</c> (Commerce → Host). This interface
/// covers Host → Commerce notification: "something significant happened
/// in the host that Commerce should know about."
/// </para>
///
/// <para>
/// The default implementation registered by
/// <see cref="CommerceIntegrationServiceCollectionExtensions.AddCommerceIntegration"/>
/// is <c>NoopCommerceLifecycleNotifier</c>, which accepts all events without
/// performing any I/O. A real outbound HTTP or message-bus implementation
/// is wired in a later phase once the Commerce ingest endpoint is defined.
/// </para>
///
/// <para>
/// <b>Never block host business operations on notification delivery.</b>
/// Implementations MUST catch and log delivery errors without re-throwing.
/// </para>
/// </summary>
public interface ICommerceLifecycleNotifier
{
    /// <summary>
    /// Notify Commerce of a lifecycle transition. The event type is
    /// determined by <see cref="CommerceLifecycleEvent.EventType"/>;
    /// use <see cref="CommerceEventTypes"/> constants.
    /// </summary>
    /// <param name="ev">Lifecycle event envelope.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Completes when the notification has been accepted (not necessarily delivered).</returns>
    Task NotifyAsync(CommerceLifecycleEvent ev, CancellationToken ct = default);
}
