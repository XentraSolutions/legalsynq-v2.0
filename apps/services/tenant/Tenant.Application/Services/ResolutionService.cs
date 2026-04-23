using Tenant.Application.DTOs;
using Tenant.Application.Interfaces;
using Tenant.Domain;

namespace Tenant.Application.Services;

public class ResolutionService : IResolutionService
{
    private readonly IDomainRepository  _domains;
    private readonly ITenantRepository  _tenants;
    private readonly IBrandingRepository _brandings;

    public ResolutionService(
        IDomainRepository  domains,
        ITenantRepository  tenants,
        IBrandingRepository brandings)
    {
        _domains   = domains;
        _tenants   = tenants;
        _brandings = brandings;
    }

    // ── by-host ───────────────────────────────────────────────────────────────

    public async Task<TenantResolutionResponse?> ResolveByHostAsync(
        string            host,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(host)) return null;

        var normalized = TenantDomain.NormalizeHost(host);
        if (!TenantDomain.IsValidHost(normalized)) return null;

        var domain = await _domains.GetActiveByHostAsync(normalized, ct);
        if (domain is null) return null;

        var tenant = await _tenants.GetByIdAsync(domain.TenantId, ct);
        if (tenant is null) return null;

        var branding = await _brandings.GetByTenantIdAsync(tenant.Id, ct);

        return new TenantResolutionResponse(
            tenant.Id,
            tenant.Code,
            tenant.DisplayName,
            tenant.Status.ToString(),
            MatchedBy: "Host",
            MatchedHost: domain.Host,
            PrimaryColor: branding?.PrimaryColor,
            LogoDocumentId: branding?.LogoDocumentId ?? tenant.LogoDocumentId);
    }

    // ── by-subdomain ──────────────────────────────────────────────────────────

    public async Task<TenantResolutionResponse?> ResolveBySubdomainAsync(
        string            subdomain,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(subdomain)) return null;

        var normalized = subdomain.Trim().ToLowerInvariant();

        // 1. Check TenantDomain — prefer IsPrimary=true, then any active Subdomain.
        //    Match: host equals the label exactly OR host begins with "{label}.".
        var domain = await _domains.GetActiveSubdomainByLabelAsync(normalized, ct);

        if (domain is not null)
        {
            var tenant = await _tenants.GetByIdAsync(domain.TenantId, ct);
            if (tenant is not null)
            {
                var branding = await _brandings.GetByTenantIdAsync(tenant.Id, ct);
                return new TenantResolutionResponse(
                    tenant.Id,
                    tenant.Code,
                    tenant.DisplayName,
                    tenant.Status.ToString(),
                    MatchedBy: "Subdomain",
                    MatchedHost: domain.Host,
                    PrimaryColor: branding?.PrimaryColor,
                    LogoDocumentId: branding?.LogoDocumentId ?? tenant.LogoDocumentId);
            }
        }

        // 2. Migration fallback — check Tenant.Subdomain for backward compatibility.
        var fallbackTenant = await _tenants.GetBySubdomainAsync(normalized, ct);
        if (fallbackTenant is null) return null;

        var fallbackBranding = await _brandings.GetByTenantIdAsync(fallbackTenant.Id, ct);
        return new TenantResolutionResponse(
            fallbackTenant.Id,
            fallbackTenant.Code,
            fallbackTenant.DisplayName,
            fallbackTenant.Status.ToString(),
            MatchedBy: "Subdomain",
            MatchedHost: null,
            PrimaryColor: fallbackBranding?.PrimaryColor,
            LogoDocumentId: fallbackBranding?.LogoDocumentId ?? fallbackTenant.LogoDocumentId);
    }

    // ── by-code ───────────────────────────────────────────────────────────────

    public async Task<TenantResolutionResponse?> ResolveByCodeAsync(
        string            code,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;

        var normalized = code.Trim().ToLowerInvariant();
        var tenant     = await _tenants.GetByCodeAsync(normalized, ct);
        if (tenant is null) return null;

        // Resolve the primary active host for informational purposes.
        var primaryDomain = await _domains.GetActivePrimarySubdomainByTenantAsync(tenant.Id, ct);
        var branding      = await _brandings.GetByTenantIdAsync(tenant.Id, ct);

        return new TenantResolutionResponse(
            tenant.Id,
            tenant.Code,
            tenant.DisplayName,
            tenant.Status.ToString(),
            MatchedBy: "Code",
            MatchedHost: primaryDomain?.Host,
            PrimaryColor: branding?.PrimaryColor,
            LogoDocumentId: branding?.LogoDocumentId ?? tenant.LogoDocumentId);
    }
}
