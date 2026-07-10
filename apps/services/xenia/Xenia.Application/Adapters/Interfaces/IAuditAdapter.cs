namespace Xenia.Application.Adapters.Interfaces;

/// <summary>
/// Platform-neutral contract for recording structured audit events.
///
/// Audit events must NEVER be silently discarded. When this adapter is unavailable,
/// implementations must fall back to durable local logging with a clear marker prefix.
///
/// Each audit event includes: tenant, actor, action, resource, result, correlation ID, timestamp.
/// </summary>
public interface IAuditAdapter
{
    /// <summary>Whether this adapter is configured for the current environment.</summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Records a structured audit event.
    /// Implementations must not throw — failures must be handled and logged locally.
    /// </summary>
    Task RecordEventAsync(XeniaAuditEvent auditEvent, CancellationToken ct = default);
}

/// <summary>
/// A structured audit event produced by Xenia operations.
/// Contains all context required for compliance and security investigation.
/// </summary>
public sealed record XeniaAuditEvent
{
    public required string Action { get; init; }
    public required string ResourceType { get; init; }
    public required string? ResourceId { get; init; }
    public required string Result { get; init; }
    public required Guid? TenantId { get; init; }
    public required Guid? ActorId { get; init; }
    public required string? CorrelationId { get; init; }
    public required DateTime OccurredAt { get; init; }
    public string? Detail { get; init; }
    public string SourceSystem => "xenia";
}
