using Microsoft.AspNetCore.Http;

namespace Billing.Api.LegalSynq;

/// <summary>
/// LS-INT-01 — dual-mode tenant context resolver for Tenant Billing.
///
/// Implementations resolve the canonical <see cref="Guid"/> tenant identifier
/// for the current HTTP request using the configured priority hierarchy:
/// <list type="number">
///   <item>LegalSynq JWT <c>tenant_id</c> claim</item>
///   <item>Internal-service JWT + <c>X-Tenant-Id</c> header</item>
///   <item><c>X-Tenant-Id</c> header fallback</item>
/// </list>
///
/// This preserves standalone compatibility, staged migration, and
/// operational rollback by never removing the header-based path.
/// </summary>
public interface ITenantIdentityContextResolver
{
    Task<TenantResolutionResult> ResolveAsync(HttpContext context, CancellationToken ct = default);
}
