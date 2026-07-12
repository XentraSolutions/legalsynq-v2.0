namespace Xenia.Domain.Events;

/// <summary>
/// Platform-neutral event envelope for all Xenia internal events.
///
/// This envelope provides standard metadata required for traceability,
/// ordering, and correlation across modules and adapters. The payload
/// carries the domain-specific event data.
///
/// Design intent: the envelope is independent of any specific message broker.
/// The <see cref="IEventPublisher"/> contract allows swapping the transport
/// (in-memory, SQS, RabbitMQ, etc.) without changing event producers.
/// </summary>
public sealed record XeniaEventEnvelope<TPayload>
{
    /// <summary>Unique identifier for this event occurrence (UUIDv7 — time-ordered).</summary>
    public required Guid EventId { get; init; }

    /// <summary>Fully-qualified event type name. Example: <c>xenia.module.enabled</c>.</summary>
    public required string EventType { get; init; }

    /// <summary>Schema version of the payload. Increment when breaking changes occur.</summary>
    public required int EventVersion { get; init; }

    /// <summary>UTC timestamp when the event occurred.</summary>
    public required DateTime OccurredAt { get; init; }

    /// <summary>Tenant that this event is scoped to. Null for platform-level events.</summary>
    public Guid? TenantId { get; init; }

    /// <summary>Authenticated actor that triggered this event. Null for system events.</summary>
    public Guid? ActorId { get; init; }

    /// <summary>
    /// Correlation ID from the originating HTTP request or job.
    /// Propagated across service calls for distributed tracing.
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>ID of the event that caused this event (event chain tracing).</summary>
    public Guid? CausationId { get; init; }

    /// <summary>Domain-specific event payload.</summary>
    public required TPayload Payload { get; init; }

    /// <summary>Optional key-value metadata for routing, filtering, or observability.</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; }
        = new Dictionary<string, string>();
}

/// <summary>
/// Non-generic marker interface for event envelopes.
/// Allows untyped handling in dispatcher infrastructure.
/// </summary>
public interface IXeniaEventEnvelope
{
    Guid EventId { get; }
    string EventType { get; }
    int EventVersion { get; }
    DateTime OccurredAt { get; }
    Guid? TenantId { get; }
    Guid? ActorId { get; }
    string? CorrelationId { get; }
    Guid? CausationId { get; }
}
