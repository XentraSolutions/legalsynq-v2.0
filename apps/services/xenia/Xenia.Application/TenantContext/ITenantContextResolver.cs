using Microsoft.AspNetCore.Http;

namespace Xenia.Application.TenantContext;

/// <summary>
/// Resolves a <see cref="IXeniaTenantContext"/> from an HTTP request.
///
/// Production implementation: reads the <c>tenant_id</c> claim from a signed JWT.
/// Development implementation: may return a configured test tenant (not active in production).
///
/// The resolver must not trust caller-supplied tenant identifiers that are not
/// authenticated by the platform identity infrastructure.
/// </summary>
public interface ITenantContextResolver
{
    /// <summary>
    /// Attempts to resolve the tenant context for the given request.
    /// Returns null when the request carries no valid tenant identity
    /// (unauthenticated requests or platform-level requests without tenant scope).
    /// </summary>
    Task<IXeniaTenantContext?> ResolveAsync(HttpContext httpContext, CancellationToken ct = default);
}
