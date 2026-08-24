using Xenia.Domain.Events;

namespace Xenia.Application.Events;

/// <summary>
/// Publishes Xenia domain events to registered handlers.
///
/// The development implementation uses in-memory delivery.
/// Production modules that require reliable delivery should use a
/// durable broker adapter (e.g. SQS, RabbitMQ) registered for this interface.
/// </summary>
public interface IEventPublisher
{
    /// <summary>
    /// Publishes an event envelope to all registered handlers for the event type.
    /// Implementations must not throw — individual handler failures must be caught and logged.
    /// </summary>
    Task PublishAsync<TPayload>(
        XeniaEventEnvelope<TPayload> envelope,
        CancellationToken ct = default);
}
