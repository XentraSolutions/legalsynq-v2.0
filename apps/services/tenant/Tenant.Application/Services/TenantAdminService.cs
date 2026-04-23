using BuildingBlocks.Exceptions;
using Tenant.Application.DTOs;
using Tenant.Application.Interfaces;
using Tenant.Domain;

namespace Tenant.Application.Services;

/// <summary>
/// TENANT-B11 — Admin-focused aggregation service.
///
/// Combines data from Tenant repositories (tenant, branding, entitlements,
/// domains, capabilities, settings) plus an optional read-through to Identity
/// for compat fields (sessionTimeoutMinutes).
///
/// Returns response shapes that are directly compatible with the control-center
/// <c>mapTenantSummary</c> / <c>mapTenantDetail</c> mappers, so the Control
/// Center can switch reads from Identity to Tenant without changing its mappers.
/// </summary>
public class TenantAdminService : ITenantAdminService
{
    private readonly ITenantRepository      _tenantRepo;
    private readonly IBrandingRepository    _brandingRepo;
    private readonly IEntitlementRepository _entitlementRepo;
    private readonly IDomainRepository      _domainRepo;
    private readonly ICapabilityRepository  _capabilityRepo;
    private readonly ISettingRepository     _settingRepo;
    private readonly IIdentityCompatAdapter _identityCompat;

    public TenantAdminService(
        ITenantRepository      tenantRepo,
        IBrandingRepository    brandingRepo,
        IEntitlementRepository entitlementRepo,
        IDomainRepository      domainRepo,
        ICapabilityRepository  capabilityRepo,
        ISettingRepository     settingRepo,
        IIdentityCompatAdapter identityCompat)
    {
        _tenantRepo      = tenantRepo;
        _brandingRepo    = brandingRepo;
        _entitlementRepo = entitlementRepo;
        _domainRepo      = domainRepo;
        _capabilityRepo  = capabilityRepo;
        _settingRepo     = settingRepo;
        _identityCompat  = identityCompat;
    }

    public async Task<(List<TenantAdminSummaryResponse> Items, int Total)> ListAdminAsync(
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        if (page < 1)       page     = 1;
        if (pageSize < 1)   pageSize = 20;
        if (pageSize > 200) pageSize = 200;

        var (tenants, total) = await _tenantRepo.ListAsync(page, pageSize, ct);

        var items = tenants.Select(ToSummary).ToList();
        return (items, total);
    }

    public async Task<TenantAdminDetailResponse?> GetAdminDetailAsync(Guid id, CancellationToken ct = default)
    {
        var tenant = await _tenantRepo.GetByIdAsync(id, ct);
        if (tenant is null) return null;

        // Run all enrichment queries in parallel for efficiency.
        var brandingTask      = _brandingRepo.GetByTenantIdAsync(id, ct);
        var entitlementsTask  = _entitlementRepo.ListByTenantAsync(id, ct);
        var domainsTask       = _domainRepo.ListByTenantAsync(id, ct);
        var capabilitiesTask  = _capabilityRepo.ListByTenantAsync(id, ct);
        var settingsTask      = _settingRepo.ListByTenantAsync(id, ct);
        var sessionTask       = _identityCompat.GetSessionTimeoutMinutesAsync(id, ct);

        await Task.WhenAll(brandingTask, entitlementsTask, domainsTask,
                           capabilitiesTask, settingsTask, sessionTask);

        var branding      = brandingTask.Result;
        var entitlements  = entitlementsTask.Result;
        var domains       = domainsTask.Result;
        var capabilities  = capabilitiesTask.Result;
        var settings      = settingsTask.Result;
        var sessionTimeout = sessionTask.Result;

        // Resolve logo: TenantBranding is authoritative (B10); Tenant entity is fallback.
        var logoDocumentId      = branding?.LogoDocumentId      ?? tenant.LogoDocumentId;
        var logoWhiteDocumentId = branding?.LogoWhiteDocumentId ?? tenant.LogoWhiteDocumentId;

        var entitlementItems = entitlements
            .Select(e => new AdminEntitlementItem(
                ProductCode:  e.ProductKey,
                ProductName:  e.ProductDisplayName ?? e.ProductKey,
                Enabled:      e.IsEnabled,
                Status:       e.IsEnabled ? "Active" : "Disabled",
                EnabledAtUtc: e.EffectiveFromUtc))
            .ToList<AdminEntitlementItem>();

        // Settings summary: extract common keys.
        var defaultProductSetting = settings.FirstOrDefault(s => s.SettingKey == "default_product");
        var localeSetting         = settings.FirstOrDefault(s => s.SettingKey == "locale");
        var timeZoneSetting       = settings.FirstOrDefault(s => s.SettingKey == "timezone");

        var settingsSummary = new TenantAdminSettingsSummary(
            DefaultProduct: defaultProductSetting?.SettingValue ?? tenant.Locale,
            Locale:         localeSetting?.SettingValue         ?? tenant.Locale,
            TimeZone:       timeZoneSetting?.SettingValue       ?? tenant.TimeZone);

        var brandingSummary = branding is null ? null : new TenantAdminBrandingSummary(
            BrandName:          branding.BrandName,
            PrimaryColor:       branding.PrimaryColor,
            LogoDocumentId:     branding.LogoDocumentId,
            LogoWhiteDocumentId: branding.LogoWhiteDocumentId);

        var compatSource = sessionTimeout.HasValue ? "IdentityCompat" : "Unavailable";

        return new TenantAdminDetailResponse(
            Id:                  tenant.Id,
            Code:                tenant.Code,
            DisplayName:         tenant.DisplayName,
            Status:              tenant.Status.ToString(),
            IsActive:            tenant.Status == TenantStatus.Active,
            Type:                "LawFirm",
            PrimaryContactName:  "",
            UserCount:           0,
            OrgCount:            0,
            ActiveUserCount:     0,
            LinkedOrgCount:      0,
            Email:               tenant.SupportEmail,
            Subdomain:           tenant.Subdomain,
            CreatedAtUtc:        tenant.CreatedAtUtc,
            UpdatedAtUtc:        tenant.UpdatedAtUtc,
            LogoDocumentId:      logoDocumentId,
            LogoWhiteDocumentId: logoWhiteDocumentId,
            SessionTimeoutMinutes: sessionTimeout,
            IdentityCompatSource: compatSource,
            ProductEntitlements:  entitlementItems,
            DomainCount:         domains.Count,
            CapabilityCount:     capabilities.Count,
            SettingsSummary:     settingsSummary,
            BrandingSummary:     brandingSummary);
    }

    public async Task<TenantAdminSummaryResponse> UpdateStatusAsync(
        Guid id,
        string status,
        CancellationToken ct = default)
    {
        var tenant = await _tenantRepo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException($"Tenant '{id}' was not found.");

        if (!Enum.TryParse<TenantStatus>(status, ignoreCase: true, out var parsed))
            throw new ValidationException($"Invalid status '{status}'.",
                new Dictionary<string, string[]>
                {
                    ["status"] = [$"'{status}' is not a valid tenant status (Active, Inactive, Suspended)."]
                });

        tenant.SetStatus(parsed);
        await _tenantRepo.UpdateAsync(tenant, ct);

        return ToSummary(tenant);
    }

    // ── Mapping ───────────────────────────────────────────────────────────────

    private static TenantAdminSummaryResponse ToSummary(Domain.Tenant t) =>
        new(
            Id:                 t.Id,
            Code:               t.Code,
            DisplayName:        t.DisplayName,
            Status:             t.Status.ToString(),
            IsActive:           t.Status == TenantStatus.Active,
            Type:               "LawFirm",
            PrimaryContactName: "",
            UserCount:          0,
            OrgCount:           0,
            Subdomain:          t.Subdomain,
            CreatedAtUtc:       t.CreatedAtUtc);
}
