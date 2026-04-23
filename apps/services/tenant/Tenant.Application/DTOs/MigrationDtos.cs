namespace Tenant.Application.DTOs;

// ── Migration utility DTOs ────────────────────────────────────────────────────

/// <summary>
/// Summary of a dry-run reconciliation between Identity tenant data and Tenant service data.
/// Block 4 — foundation only; no writes performed.
/// </summary>
public record MigrationDryRunReport(
    DateTime  GeneratedAtUtc,
    bool      IdentityAccessible,
    string?   IdentityAccessError,
    int       IdentityTenantCount,
    int       TenantServiceCount,
    int       MissingInTenantService,
    int       CodeMismatches,
    int       NameMismatches,
    int       StatusMismatches,
    int       SubdomainGaps,
    int       LogoGaps,
    IReadOnlyList<MigrationTenantDiff> Differences);

/// <summary>Per-tenant difference record from the reconciliation.</summary>
public record MigrationTenantDiff(
    string   IdentityTenantId,
    string   IdentityCode,
    string   IdentityName,
    string   IdentityStatus,
    string?  IdentitySubdomain,
    bool     IdentityHasLogo,
    string?  TenantServiceId,
    string?  TenantServiceCode,
    string?  TenantServiceName,
    string?  TenantServiceStatus,
    bool     IsMissing,
    bool     HasCodeMismatch,
    bool     HasNameMismatch,
    bool     HasStatusMismatch,
    bool     HasSubdomainGap,
    bool     HasLogoGap);
