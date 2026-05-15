namespace Billing.Api.LegalSynq;

/// <summary>
/// LS-INT-01 — dual-mode tenant context resolution options for Tenant Billing.
/// Bound from the <c>LegalSynq:TenantContext</c> configuration section.
///
/// Resolution hierarchy when <see cref="Enabled"/> is true:
/// <list type="number">
///   <item>LegalSynq JWT <c>tenant_id</c> claim (when <see cref="PreferJwtTenant"/> = true)</item>
///   <item>Internal-service JWT marker + <c>X-Tenant-Id</c> header (service-to-service)</item>
///   <item><c>X-Tenant-Id</c> header fallback (when <see cref="AllowHeaderFallback"/> = true)</item>
/// </list>
///
/// When <see cref="Enabled"/> = false (default), the original
/// <c>X-Internal-Token</c> + <c>X-Tenant-Id</c> middleware runs
/// unchanged — zero behavior change on deploy.
/// </summary>
public sealed class LegalSynqTenantContextOptions
{
    public const string SectionName = "LegalSynq:TenantContext";

    /// <summary>Master switch — defaults to false (standalone/unchanged behavior).</summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// When true and a valid JWT contains a <c>tenant_id</c> claim, use it
    /// as the canonical tenant identifier. Default true.
    /// </summary>
    public bool PreferJwtTenant { get; set; } = true;

    /// <summary>
    /// When true, fall back to the <c>X-Tenant-Id</c> header when no JWT
    /// tenant claim is present. Preserves standalone + staged-migration
    /// compatibility. Default true.
    /// </summary>
    public bool AllowHeaderFallback { get; set; } = true;

    /// <summary>
    /// When true, a request carrying a valid internal-service JWT (role =
    /// <c>InternalService</c>) is allowed to supply tenant context via the
    /// <c>X-Tenant-Id</c> header (service-to-service pattern). Default true.
    /// </summary>
    public bool AllowInternalTokenFallback { get; set; } = true;
}
