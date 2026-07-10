using Xenia.Domain.Events;

namespace Xenia.Application.Events;

/// <summary>
/// Handles a specific Xenia event payload type.
///
/// Handlers are registered in DI and discovered by the <see cref="IEventPublisher"/>
/// at publish time. A single event may have multiple handlers.
///
/// Handlers must be idempotent where possible.
/// Handlers must not throw — failures should be caught and logged.
/// </summary>
public interface IEventHandler<TPayload>
{
    Task HandleAsync(XeniaEventEnvelope<TPayload> envelope, CancellationToken ct = default);
}
