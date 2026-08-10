namespace Xenia.Application.TenantContext;

/// <summary>
/// Represents the resolved tenant context for a single Xenia request or operation.
///
/// The context is resolved from cryptographically signed platform credentials
/// (JWT claims) — never from arbitrary caller-supplied values.
///
/// Tenant-scoped operations MUST check <see cref="IsResolved"/> before proceeding.
/// </summary>
public interface IXeniaTenantContext
{
    /// <summary>
    /// True when the tenant context has been successfully resolved for this request.
    /// Tenant-scoped operations must reject requests when this is false.
    /// </summary>
    bool IsResolved { get; }

    /// <summary>
    /// The resolved tenant identifier. Throws if context is not resolved.
    /// Use <see cref="IsResolved"/> before accessing this property.
    /// </summary>
    Guid TenantId { get; }

    /// <summary>
    /// Optional human-readable tenant code (e.g. "ACME").
    /// May be null even when <see cref="IsResolved"/> is true.
    /// </summary>
    string? TenantCode { get; }

    /// <summary>
    /// Authenticated actor (user) ID. Null for service-to-service requests
    /// that do not carry a user identity.
    /// </summary>
    Guid? ActorId { get; }

    /// <summary>
    /// Correlation ID from the originating request. Used for distributed tracing
    /// and audit event correlation.
    /// </summary>
    string? CorrelationId { get; }
}
