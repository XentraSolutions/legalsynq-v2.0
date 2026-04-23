using System.Net.Mail;
using System.Text.RegularExpressions;
using BuildingBlocks.Exceptions;
using Tenant.Application.DTOs;
using Tenant.Application.Interfaces;
using Tenant.Domain;

namespace Tenant.Application.Services;

public class TenantService : ITenantService
{
    private readonly ITenantRepository _repository;

    public TenantService(ITenantRepository repository) => _repository = repository;

    public async Task<TenantResponse?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var tenant = await _repository.GetByIdAsync(id, ct);
        return tenant is null ? null : ToResponse(tenant);
    }

    public async Task<TenantResponse?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        var tenant = await _repository.GetByCodeAsync(code.ToLowerInvariant(), ct);
        return tenant is null ? null : ToResponse(tenant);
    }

    public async Task<(List<TenantResponse> Items, int Total)> ListAsync(
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        if (page < 1)       page     = 1;
        if (pageSize < 1)   pageSize = 20;
        if (pageSize > 200) pageSize = 200;

        var (items, total) = await _repository.ListAsync(page, pageSize, ct);
        return (items.Select(ToResponse).ToList(), total);
    }

    public async Task<TenantResponse> CreateAsync(CreateTenantRequest request, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Code,        nameof(request.Code));
        ArgumentException.ThrowIfNullOrWhiteSpace(request.DisplayName, nameof(request.DisplayName));

        var errors = new Dictionary<string, string[]>();

        var code = request.Code.Trim().ToLowerInvariant();

        if (await _repository.ExistsByCodeAsync(code, ct))
            throw new ConflictException($"A tenant with code '{code}' already exists.");

        if (request.Subdomain is not null)
        {
            var sub = request.Subdomain.Trim().ToLowerInvariant();
            if (await _repository.ExistsBySubdomainAsync(sub, null, ct))
                throw new ConflictException($"The subdomain '{sub}' is already taken.");
        }

        ValidateOptionalEmail(request.SupportEmail, "supportEmail", errors);
        ValidateOptionalUrl(request.WebsiteUrl,     "websiteUrl",   errors);
        ValidateOptionalCountryCode(request.CountryCode, "countryCode", errors);

        if (errors.Count > 0)
            throw new ValidationException("One or more validation errors occurred.", errors);

        var tenant = Domain.Tenant.Create(
            code,
            request.DisplayName,
            request.LegalName,
            request.Subdomain,
            request.Description,
            request.WebsiteUrl,
            request.TimeZone,
            request.Locale,
            request.SupportEmail,
            request.SupportPhone,
            request.AddressLine1,
            request.AddressLine2,
            request.City,
            request.StateOrProvince,
            request.PostalCode,
            request.CountryCode);

        await _repository.AddAsync(tenant, ct);
        return ToResponse(tenant);
    }

    public async Task<TenantResponse> UpdateAsync(Guid id, UpdateTenantRequest request, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.DisplayName, nameof(request.DisplayName));

        var tenant = await _repository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException($"Tenant '{id}' was not found.");

        var errors = new Dictionary<string, string[]>();

        if (request.Subdomain is not null)
        {
            var sub = request.Subdomain.Trim().ToLowerInvariant();
            if (await _repository.ExistsBySubdomainAsync(sub, id, ct))
                throw new ConflictException($"The subdomain '{sub}' is already taken.");
        }

        ValidateOptionalEmail(request.SupportEmail, "supportEmail", errors);
        ValidateOptionalUrl(request.WebsiteUrl,     "websiteUrl",   errors);
        ValidateOptionalCountryCode(request.CountryCode, "countryCode", errors);

        if (errors.Count > 0)
            throw new ValidationException("One or more validation errors occurred.", errors);

        tenant.UpdateProfile(
            request.DisplayName,
            request.LegalName,
            request.Description,
            request.WebsiteUrl,
            request.TimeZone,
            request.Locale,
            request.SupportEmail,
            request.SupportPhone);

        tenant.UpdateAddress(
            request.AddressLine1,
            request.AddressLine2,
            request.City,
            request.StateOrProvince,
            request.PostalCode,
            request.CountryCode);

        if (request.Subdomain is not null)
            tenant.SetSubdomain(request.Subdomain);

        if (request.Status is not null)
        {
            if (!Enum.TryParse<TenantStatus>(request.Status, ignoreCase: true, out var status))
                throw new ValidationException($"Invalid status '{request.Status}'.",
                    new Dictionary<string, string[]> { ["status"] = [$"'{request.Status}' is not a valid status value."] });
            tenant.SetStatus(status);
        }

        if (request.LogoDocumentId is not null)
            tenant.SetLogo(request.LogoDocumentId);

        if (request.LogoWhiteDocumentId is not null)
            tenant.SetLogoWhite(request.LogoWhiteDocumentId);

        await _repository.UpdateAsync(tenant, ct);
        return ToResponse(tenant);
    }

    public async Task DeactivateAsync(Guid id, CancellationToken ct = default)
    {
        var tenant = await _repository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException($"Tenant '{id}' was not found.");

        tenant.SetStatus(TenantStatus.Inactive);
        await _repository.UpdateAsync(tenant, ct);
    }

    // ── Validation helpers ────────────────────────────────────────────────────

    private static void ValidateOptionalEmail(string? value, string field, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        try { _ = new MailAddress(value); }
        catch { errors[field] = [$"'{value}' is not a valid email address."]; }
    }

    private static void ValidateOptionalUrl(string? value, string field, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            errors[field] = [$"'{value}' is not a valid http/https URL."];
    }

    private static void ValidateOptionalCountryCode(string? value, string field, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        if (value.Trim().Length != 2)
            errors[field] = ["Country code must be a 2-character ISO 3166-1 alpha-2 value."];
    }

    // ── Mapping ───────────────────────────────────────────────────────────────

    internal static TenantResponse ToResponse(Domain.Tenant t) => new(
        t.Id,
        t.Code,
        t.DisplayName,
        t.LegalName,
        t.Description,
        t.Status.ToString(),
        t.Subdomain,
        t.LogoDocumentId,
        t.LogoWhiteDocumentId,
        t.WebsiteUrl,
        t.TimeZone,
        t.Locale,
        t.SupportEmail,
        t.SupportPhone,
        t.AddressLine1,
        t.AddressLine2,
        t.City,
        t.StateOrProvince,
        t.PostalCode,
        t.CountryCode,
        t.CreatedAtUtc,
        t.UpdatedAtUtc);
}
