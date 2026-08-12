using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Xenia.Application.TenantContext;

namespace Xenia.Infrastructure.TenantContext;

/// <summary>
/// Resolves <see cref="IXeniaTenantContext"/> from a JWT <c>tenant_id</c> claim.
///
/// The claim is minted and signed by the platform Identity service. Xenia
/// trusts the claim because the JWT signature is verified by ASP.NET Core's
/// JWT Bearer middleware before this resolver runs.
///
/// This resolver does NOT trust arbitrary caller-supplied tenant identifiers.
/// Only claims from cryptographically verified tokens are accepted.
/// </summary>
internal sealed class JwtTenantContextResolver : ITenantContextResolver
{
    private readonly ILogger<JwtTenantContextResolver> _logger;

    public JwtTenantContextResolver(ILogger<JwtTenantContextResolver> logger)
        => _logger = logger;

    public Task<IXeniaTenantContext?> ResolveAsync(HttpContext httpContext, CancellationToken ct = default)
    {
        var user = httpContext.User;

        if (user?.Identity?.IsAuthenticated != true)
        {
            return Task.FromResult<IXeniaTenantContext?>(null);
        }

        var tenantIdClaim = user.FindFirst("tenant_id")?.Value;

        if (string.IsNullOrWhiteSpace(tenantIdClaim) ||
            !Guid.TryParse(tenantIdClaim, out var tenantId) ||
            tenantId == Guid.Empty)
        {
            _logger.LogDebug(
                "Xenia: JWT authenticated but no valid tenant_id claim found. " +
                "This is expected for platform-admin requests without tenant scope.");
            return Task.FromResult<IXeniaTenantContext?>(null);
        }

        var tenantCode = user.FindFirst("tenant_code")?.Value;
        var correlationId = httpContext.Request.Headers["X-Correlation-Id"].FirstOrDefault();

        Guid? actorId = Guid.TryParse(user.FindFirst("sub")?.Value, out var uid)
            ? uid
            : null;

        IXeniaTenantContext ctx = new ResolvedTenantContext(tenantId, tenantCode, actorId, correlationId);
        return Task.FromResult<IXeniaTenantContext?>(ctx);
    }

    private sealed record ResolvedTenantContext(
        Guid TenantId,
        string? TenantCode,
        Guid? ActorId,
        string? CorrelationId) : IXeniaTenantContext
    {
        public bool IsResolved => true;
    }
}
