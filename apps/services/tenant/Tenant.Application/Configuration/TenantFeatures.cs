namespace Tenant.Application.Configuration;

/// <summary>
/// Phased read-source feature flags for the Tenant service.
/// All flags default to the safest Identity-first mode.
/// Configure via appsettings.json Features section or environment variables
/// (Features__TenantReadSource=HybridFallback, etc.).
/// </summary>
public class TenantFeatures
{
    public const string SectionName = "Features";

    /// <summary>
    /// Overall default read-source for all tenant-related runtime reads.
    /// Per-consumer overrides below take precedence when explicitly set.
    /// </summary>
    public TenantReadSource TenantReadSource { get; set; } = TenantReadSource.Identity;

    /// <summary>
    /// Read-source for public branding bootstrap (login page, anonymous branding).
    /// When set, overrides TenantReadSource for branding lookups.
    /// </summary>
    public TenantReadSource TenantBrandingReadSource { get; set; } = TenantReadSource.Identity;

    /// <summary>
    /// Read-source for tenant resolution by host/subdomain/code.
    /// When set, overrides TenantReadSource for resolution lookups.
    /// </summary>
    public TenantReadSource TenantResolutionReadSource { get; set; } = TenantReadSource.Identity;

    /// <summary>
    /// Activates dual-write via ITenantSyncAdapter.
    /// Default: false — dual-write is disabled until Block 7 validation.
    /// </summary>
    public bool TenantDualWriteEnabled { get; set; } = false;

    /// <summary>
    /// TENANT-B07 — When true, a Tenant sync failure from Identity aborts the originating
    /// Identity operation (returns 502 to the caller). When false (default), sync failures
    /// are logged and the Identity operation continues normally.
    /// Only enable in controlled environments where Tenant service health is confirmed.
    /// </summary>
    public bool TenantDualWriteStrictMode { get; set; } = false;

    // ── TENANT-B08: Caching / Performance Hardening ───────────────────────────

    /// <summary>
    /// TENANT-B08 — Enable in-process IMemoryCache on public branding and
    /// resolution read paths. Safe to disable for debugging or rollback.
    /// Default: true.
    /// </summary>
    public bool TenantReadCachingEnabled { get; set; } = true;

    /// <summary>
    /// TENANT-B08 — Time-to-live in seconds for cached branding and resolution
    /// results. Applies to all cached public read paths.
    /// Default: 60 seconds.
    /// </summary>
    public int TenantReadCacheTtlSeconds { get; set; } = 60;
}

/// <summary>
/// Controls which service is consulted for tenant data reads.
/// </summary>
public enum TenantReadSource
{
    /// <summary>Legacy Identity service path. Safe default — no behavior change.</summary>
    Identity,

    /// <summary>Read exclusively from the Tenant service.</summary>
    Tenant,

    /// <summary>
    /// Try the Tenant service first; fall back to Identity on failure,
    /// 404, or incomplete required fields.
    /// </summary>
    HybridFallback,
}
