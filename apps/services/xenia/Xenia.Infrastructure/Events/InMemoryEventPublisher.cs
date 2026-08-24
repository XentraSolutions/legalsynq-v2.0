using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xenia.Application.Events;
using Xenia.Domain.Events;

namespace Xenia.Infrastructure.Events;

/// <summary>
/// In-memory event publisher for development and test environments.
///
/// Resolves handlers from DI for the current scope and invokes them synchronously.
/// Individual handler failures are caught and logged — the publisher never throws.
///
/// IMPORTANT: This implementation does not survive process restarts and does not
/// guarantee delivery. Replace with a durable broker adapter (SQS, RabbitMQ) for
/// production modules that require reliable event delivery.
/// </summary>
internal sealed class InMemoryEventPublisher : IEventPublisher
{
    private readonly IServiceProvider _services;
    private readonly ILogger<InMemoryEventPublisher> _logger;

    public InMemoryEventPublisher(IServiceProvider services, ILogger<InMemoryEventPublisher> logger)
    {
        _services = services;
        _logger = logger;
    }

    public async Task PublishAsync<TPayload>(
        XeniaEventEnvelope<TPayload> envelope,
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "Xenia: publishing event {EventType} ({EventId}) for tenant {TenantId}",
            envelope.EventType, envelope.EventId, envelope.TenantId);

        var handlers = _services.GetServices<IEventHandler<TPayload>>();

        foreach (var handler in handlers)
        {
            try
            {
                await handler.HandleAsync(envelope, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Xenia: handler {Handler} failed processing event {EventType} ({EventId}). " +
                    "Event publishing continues with remaining handlers.",
                    handler.GetType().Name, envelope.EventType, envelope.EventId);
            }
        }
    }
}
