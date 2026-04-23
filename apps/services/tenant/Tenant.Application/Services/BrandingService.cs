using System.Text.RegularExpressions;
using BuildingBlocks.Exceptions;
using Tenant.Application.DTOs;
using Tenant.Application.Interfaces;
using Tenant.Domain;

namespace Tenant.Application.Services;

public partial class BrandingService : IBrandingService
{
    private readonly IBrandingRepository _brandingRepo;
    private readonly ITenantRepository   _tenantRepo;

    public BrandingService(IBrandingRepository brandingRepo, ITenantRepository tenantRepo)
    {
        _brandingRepo = brandingRepo;
        _tenantRepo   = tenantRepo;
    }

    /// <summary>
    /// Returns branding for the tenant, creating an empty record if none exists yet.
    /// </summary>
    public async Task<BrandingResponse> GetByTenantIdAsync(Guid tenantId, CancellationToken ct = default)
    {
        _ = await _tenantRepo.GetByIdAsync(tenantId, ct)
            ?? throw new NotFoundException($"Tenant '{tenantId}' was not found.");

        var branding = await _brandingRepo.GetByTenantIdAsync(tenantId, ct)
                       ?? await CreateEmptyAsync(tenantId, ct);

        return ToResponse(branding);
    }

    /// <summary>
    /// Creates or updates branding for the tenant (upsert semantics).
    /// </summary>
    public async Task<BrandingResponse> UpsertAsync(Guid tenantId, UpdateBrandingRequest request, CancellationToken ct = default)
    {
        _ = await _tenantRepo.GetByIdAsync(tenantId, ct)
            ?? throw new NotFoundException($"Tenant '{tenantId}' was not found.");

        var errors = new Dictionary<string, string[]>();

        ValidateOptionalHexColor(request.PrimaryColor,    "primaryColor",    errors);
        ValidateOptionalHexColor(request.SecondaryColor,  "secondaryColor",  errors);
        ValidateOptionalHexColor(request.AccentColor,     "accentColor",     errors);
        ValidateOptionalHexColor(request.TextColor,       "textColor",       errors);
        ValidateOptionalHexColor(request.BackgroundColor, "backgroundColor", errors);
        ValidateOptionalEmail(request.SupportEmailOverride, "supportEmailOverride", errors);
        ValidateOptionalUrl(request.WebsiteUrlOverride,     "websiteUrlOverride",   errors);

        if (errors.Count > 0)
            throw new ValidationException("One or more validation errors occurred.", errors);

        var branding = await _brandingRepo.GetByTenantIdAsync(tenantId, ct);

        if (branding is null)
        {
            branding = TenantBranding.Create(tenantId);
            branding.Update(
                request.BrandName,
                request.LogoDocumentId,
                request.LogoWhiteDocumentId,
                request.FaviconDocumentId,
                request.PrimaryColor,
                request.SecondaryColor,
                request.AccentColor,
                request.TextColor,
                request.BackgroundColor,
                request.WebsiteUrlOverride,
                request.SupportEmailOverride,
                request.SupportPhoneOverride);
            await _brandingRepo.AddAsync(branding, ct);
        }
        else
        {
            branding.Update(
                request.BrandName,
                request.LogoDocumentId,
                request.LogoWhiteDocumentId,
                request.FaviconDocumentId,
                request.PrimaryColor,
                request.SecondaryColor,
                request.AccentColor,
                request.TextColor,
                request.BackgroundColor,
                request.WebsiteUrlOverride,
                request.SupportEmailOverride,
                request.SupportPhoneOverride);
            await _brandingRepo.UpdateAsync(branding, ct);
        }

        return ToResponse(branding);
    }

    public async Task<PublicBrandingResponse?> GetPublicByCodeAsync(string code, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        var tenant = await _tenantRepo.GetByCodeAsync(code.ToLowerInvariant(), ct);
        if (tenant is null || tenant.Status == TenantStatus.Inactive) return null;

        var branding = await _brandingRepo.GetByTenantIdAsync(tenant.Id, ct);
        return ToPublicResponse(tenant, branding);
    }

    public async Task<PublicBrandingResponse?> GetPublicBySubdomainAsync(string subdomain, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subdomain);
        var tenant = await _tenantRepo.GetBySubdomainAsync(subdomain.ToLowerInvariant(), ct);
        if (tenant is null || tenant.Status == TenantStatus.Inactive) return null;

        var branding = await _brandingRepo.GetByTenantIdAsync(tenant.Id, ct);
        return ToPublicResponse(tenant, branding);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<TenantBranding> CreateEmptyAsync(Guid tenantId, CancellationToken ct)
    {
        var branding = TenantBranding.Create(tenantId);
        await _brandingRepo.AddAsync(branding, ct);
        return branding;
    }

    private static void ValidateOptionalHexColor(string? value, string field, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        if (!HexColorRegex().IsMatch(value))
            errors[field] = [$"'{value}' is not a valid hex color (expected #RGB or #RRGGBB)."];
    }

    private static void ValidateOptionalEmail(string? value, string field, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        try { _ = new System.Net.Mail.MailAddress(value); }
        catch { errors[field] = [$"'{value}' is not a valid email address."]; }
    }

    private static void ValidateOptionalUrl(string? value, string field, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            errors[field] = [$"'{value}' is not a valid http/https URL."];
    }

    [GeneratedRegex(@"^#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{3})$")]
    private static partial Regex HexColorRegex();

    // ── Mapping ───────────────────────────────────────────────────────────────

    private static BrandingResponse ToResponse(TenantBranding b) => new(
        b.Id,
        b.TenantId,
        b.BrandName,
        b.LogoDocumentId,
        b.LogoWhiteDocumentId,
        b.FaviconDocumentId,
        b.PrimaryColor,
        b.SecondaryColor,
        b.AccentColor,
        b.TextColor,
        b.BackgroundColor,
        b.WebsiteUrlOverride,
        b.SupportEmailOverride,
        b.SupportPhoneOverride,
        b.CreatedAtUtc,
        b.UpdatedAtUtc);

    private static PublicBrandingResponse ToPublicResponse(Domain.Tenant t, TenantBranding? b) => new(
        t.Id,
        t.Code,
        t.DisplayName,
        b?.BrandName,
        b?.LogoDocumentId   ?? t.LogoDocumentId,
        b?.LogoWhiteDocumentId ?? t.LogoWhiteDocumentId,
        b?.FaviconDocumentId,
        b?.PrimaryColor,
        b?.SecondaryColor,
        b?.AccentColor,
        b?.TextColor,
        b?.BackgroundColor,
        b?.WebsiteUrlOverride ?? t.WebsiteUrl,
        b?.SupportEmailOverride ?? t.SupportEmail);
}
