using System.Net.Mail;
using System.Text.RegularExpressions;
using BuildingBlocks.Commerce;
using BuildingBlocks.Exceptions;
using Contracts.Commerce;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tenant.Application.Configuration;
using Tenant.Application.DTOs;
using Tenant.Application.Interfaces;
using Tenant.Domain;

namespace Tenant.Application.Services;

/// <summary>
/// LS-COMMERCE-ECO-02: Commerce lifecycle notifications wired for tenant
/// provisioning, creation, and deactivation.  Notifications are noop-first
/// and never block the primary tenant lifecycle operation.
/// </summary>
public class TenantService : ITenantService
{
    private readonly ITenantRepository           _repository;
    private readonly ISettingRepository          _settings;
    private readonly ICommerceLifecycleNotifier  _commerceNotifier;
    private readonly ILogger<TenantService>      _logger;
    private readonly string                      _platformBaseDomain;

    private const string HostPlatformKey        = "legalsynq";
    private const string DefaultTimezone        = TenantDefaults.Timezone;
    private const string TimezoneSettingKey     = TenantDefaults.TimezoneSettingKey;
    private const string LegacyTimezoneSettingKey = TenantDefaults.LegacyTimezoneSettingKey;

    public TenantService(
        ITenantRepository          repository,
        ISettingRepository         settings,
        IOptions<PlatformRoutingOptions> routingOptions,
        ICommerceLifecycleNotifier commerceNotifier,
        ILogger<TenantService>     logger)
    {
        _repository         = repository;
        _settings           = settings;
        _platformBaseDomain = NormalizeBaseDomain(routingOptions.Value.BaseDomain);
        _commerceNotifier   = commerceNotifier;
        _logger             = logger;
    }

    // ── BLK-TS-01: Tenant code format rules ──────────────────────────────────

    /// <summary>
    /// Valid code: lowercase alphanumeric + hyphens, no leading/trailing hyphens, 2–50 chars.
    /// Examples: "acme", "liens-company", "abc123"
    /// </summary>
    private static readonly Regex CodeFormatRegex = new(
        @"^[a-z0-9]([a-z0-9\-]*[a-z0-9])?$",
        RegexOptions.Compiled);

    private const int CodeMinLength = 2;
    private const int CodeMaxLength = 50;

    private static bool IsValidCodeFormat(string normalizedCode, out string error)
    {
        if (normalizedCode.Length < CodeMinLength)
        {
            error = $"Code must be at least {CodeMinLength} characters.";
            return false;
        }
        if (normalizedCode.Length > CodeMaxLength)
        {
            error = $"Code must be at most {CodeMaxLength} characters.";
            return false;
        }
        if (!CodeFormatRegex.IsMatch(normalizedCode))
        {
            error = "Code must contain only lowercase letters, digits, and hyphens, and must not start or end with a hyphen.";
            return false;
        }
        error = string.Empty;
        return true;
    }

    // ── BLK-TS-01: Check code availability ───────────────────────────────────

    public async Task<CheckCodeResponse> CheckCodeAsync(string code, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code))
            return new CheckCodeResponse(false, string.Empty, "Code cannot be empty.");

        var normalized = code.Trim().ToLowerInvariant();

        if (!IsValidCodeFormat(normalized, out var formatError))
            return new CheckCodeResponse(false, normalized, formatError);

        if (await _repository.ExistsByCodeAsync(normalized, ct))
            return new CheckCodeResponse(false, normalized, $"The code '{normalized}' is already taken.");

        return new CheckCodeResponse(true, normalized);
    }

    // ── BLK-TS-01: Minimal provision ─────────────────────────────────────────

    public async Task<ProvisionResponse> ProvisionAsync(ProvisionRequest request, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TenantName, nameof(request.TenantName));
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TenantCode, nameof(request.TenantCode));

        var code = request.TenantCode.Trim().ToLowerInvariant();

        if (!IsValidCodeFormat(code, out var formatError))
            throw new ValidationException("Invalid tenant code.",
                new Dictionary<string, string[]> { ["tenantCode"] = [formatError] });

        if (await _repository.ExistsByCodeAsync(code, ct))
            throw new ConflictException($"A tenant with code '{code}' already exists.");

        var subdomain = code;
        if (await _repository.ExistsBySubdomainAsync(subdomain, null, ct))
            throw new ConflictException($"The subdomain '{subdomain}' is already taken.");

        var tenant = Domain.Tenant.Create(
            code:         code,
            displayName:  request.TenantName.Trim(),
            subdomain:    subdomain,
            timeZone:     DefaultTimezone,
            workspaceUrl: ComposeWorkspaceUrl(subdomain));

        if (request.OwnerUserId.HasValue)
            tenant.SetOwner(request.OwnerUserId.Value);

        await _repository.AddAsync(tenant, ct);

        // ── LS-COMMERCE-ECO-02: Notify Commerce of provisioned tenant ─────────
        await TryNotifyCommerceAsync(new CommerceLifecycleEvent(
            EventType:        CommerceEventTypes.TenantCreated,
            HostPlatformKey:  HostPlatformKey,
            ExternalTenantId: tenant.Id.ToString(),
            OccurredAtUtc:    DateTimeOffset.UtcNow,
            Metadata:         new Dictionary<string, string>
            {
                ["tenantCode"] = tenant.Code,
                ["source"]     = "provision"
            }), ct);

        return new ProvisionResponse(tenant.Id, tenant.Code, tenant.Subdomain ?? subdomain);
    }

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

        if (!IsValidCodeFormat(code, out var codeFormatError))
            throw new ValidationException("Invalid tenant code.",
                new Dictionary<string, string[]> { ["code"] = [codeFormatError] });

        if (await _repository.ExistsByCodeAsync(code, ct))
            throw new ConflictException($"A tenant with code '{code}' already exists.");

        string? normalizedSubdomain = null;

        if (request.Subdomain is not null)
        {
            normalizedSubdomain = request.Subdomain.Trim().ToLowerInvariant();
            if (await _repository.ExistsBySubdomainAsync(normalizedSubdomain, null, ct))
                throw new ConflictException($"The subdomain '{normalizedSubdomain}' is already taken.");
        }
        else
        {
            normalizedSubdomain = code;
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
            normalizedSubdomain,
            request.Description,
            request.WebsiteUrl,
            request.TimeZone ?? DefaultTimezone,
            request.Locale,
            request.SupportEmail,
            request.SupportPhone,
            request.AddressLine1,
            request.AddressLine2,
            request.City,
            request.StateOrProvince,
            request.PostalCode,
            request.CountryCode,
            workspaceUrl: normalizedSubdomain is null ? null : ComposeWorkspaceUrl(normalizedSubdomain));

        await _repository.AddAsync(tenant, ct);

        // ── LS-COMMERCE-ECO-02: Notify Commerce of new tenant creation ─────────
        await TryNotifyCommerceAsync(new CommerceLifecycleEvent(
            EventType:        CommerceEventTypes.TenantCreated,
            HostPlatformKey:  HostPlatformKey,
            ExternalTenantId: tenant.Id.ToString(),
            OccurredAtUtc:    DateTimeOffset.UtcNow,
            Metadata:         new Dictionary<string, string>
            {
                ["tenantCode"] = tenant.Code,
                ["source"]     = "create"
            }), ct);

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
        {
            var normalizedSubdomain = request.Subdomain.Trim().ToLowerInvariant();
            tenant.SetSubdomain(normalizedSubdomain);
            tenant.SetWorkspaceUrl(ComposeWorkspaceUrl(normalizedSubdomain));
        }

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

        // ── LS-COMMERCE-ECO-02: Notify Commerce of tenant deactivation ─────────
        // Inactive is the closest domain status to suspended; no Closed status exists.
        await TryNotifyCommerceAsync(new CommerceLifecycleEvent(
            EventType:        CommerceEventTypes.TenantSuspended,
            HostPlatformKey:  HostPlatformKey,
            ExternalTenantId: tenant.Id.ToString(),
            OccurredAtUtc:    DateTimeOffset.UtcNow,
            Metadata:         new Dictionary<string, string>
            {
                ["tenantCode"] = tenant.Code,
                ["newStatus"]  = TenantStatus.Inactive.ToString()
            }), ct);
    }

    public async Task<string> GetTimezoneAsync(Guid tenantId, CancellationToken ct = default)
    {
        var tenant = await _repository.GetByIdAsync(tenantId, ct)
            ?? throw new NotFoundException($"Tenant '{tenantId}' was not found.");

        if (!string.IsNullOrWhiteSpace(tenant.TimeZone))
            return tenant.TimeZone;

        var setting = await _settings.GetByKeyAsync(tenantId, TimezoneSettingKey, productKey: null, ct)
                   ?? await _settings.GetByKeyAsync(tenantId, LegacyTimezoneSettingKey, productKey: null, ct);

        return string.IsNullOrWhiteSpace(setting?.SettingValue)
            ? DefaultTimezone
            : setting.SettingValue;
    }

    /// <summary>
    /// TENANT-B07 — Idempotent upsert from an Identity dual-write sync event.
    ///
    /// If the tenant already exists in the Tenant service (matched by Id), it is
    /// updated with the incoming payload fields. If it does not exist, a minimal
    /// record is created from the payload so the Tenant service can serve it as
    /// a runtime read source without requiring a full migration run first.
    ///
    /// Fields not present in the sync payload (profile metadata, address, etc.)
    /// are left unchanged on update or left null on create — they will be populated
    /// by the migration execute endpoint or a subsequent operator update.
    /// </summary>
    public async Task<string> UpdateTimezoneAsync(Guid tenantId, string timezone, CancellationToken ct = default)
    {
        var tenant = await _repository.GetByIdAsync(tenantId, ct)
            ?? throw new NotFoundException($"Tenant '{tenantId}' was not found.");

        try { TimeZoneInfo.FindSystemTimeZoneById(timezone); }
        catch (TimeZoneNotFoundException)
        {
            throw new ValidationException(
                $"'{timezone}' is not a recognized IANA or Windows timezone identifier.",
                new Dictionary<string, string[]> { ["timezone"] = [$"'{timezone}' is not a valid timezone."] });
        }

        tenant.UpdateProfile(
            tenant.DisplayName,
            tenant.LegalName,
            tenant.Description,
            tenant.WebsiteUrl,
            timezone,
            tenant.Locale,
            tenant.SupportEmail,
            tenant.SupportPhone);

        await _repository.UpdateAsync(tenant, ct);
        await UpsertTimezoneSettingAsync(tenantId, timezone, ct);
        return timezone;
    }

    private async Task UpsertTimezoneSettingAsync(Guid tenantId, string timezone, CancellationToken ct)
    {
        var canonicalSetting = await _settings.GetByKeyAsync(tenantId, TimezoneSettingKey, productKey: null, ct);
        if (canonicalSetting is null)
        {
            canonicalSetting = TenantSetting.Create(
                tenantId,
                TimezoneSettingKey,
                timezone,
                SettingValueType.String);
            await _settings.AddAsync(canonicalSetting, ct);
        }
        else
        {
            canonicalSetting.UpdateValue(timezone, SettingValueType.String);
            await _settings.UpdateAsync(canonicalSetting, ct);
        }

        var legacySetting = await _settings.GetByKeyAsync(tenantId, LegacyTimezoneSettingKey, productKey: null, ct);
        if (legacySetting is not null)
        {
            legacySetting.UpdateValue(timezone, SettingValueType.String);
            await _settings.UpdateAsync(legacySetting, ct);
        }
    }

    public async Task UpsertFromSyncAsync(TenantSyncRequest request, CancellationToken ct = default)
    {
        var existing = await _repository.GetByIdAsync(request.TenantId, ct);

        if (existing is null)
        {
            // Create a minimal record so Tenant service can serve runtime reads.
            var code = request.Code.Trim().ToLowerInvariant();

            // If the code is already taken by a *different* tenant, skip creation
            // (this should not happen in practice but guards against race conditions).
            var byCode = await _repository.GetByCodeAsync(code, ct);
            if (byCode is not null && byCode.Id != request.TenantId)
                return;

            if (byCode is null)
            {
                var created = Domain.Tenant.Rehydrate(
                    id:                 request.TenantId,
                    code:               code,
                    displayName:        request.DisplayName,
                    status:             ParseStatus(request.Status),
                    subdomain:          request.Subdomain,
                    logoDocumentId:     request.LogoDocumentId,
                    logoWhiteDocumentId: request.LogoWhiteDocumentId,
                    createdAtUtc:       request.SourceCreatedAtUtc,
                    updatedAtUtc:       request.SourceUpdatedAtUtc);

                await _repository.AddAsync(created, ct);
            }
        }
        else
        {
            // Update the fields the sync event carries.
            existing.UpdateProfile(
                existing.DisplayName != request.DisplayName ? request.DisplayName : existing.DisplayName,
                existing.LegalName,
                existing.Description,
                existing.WebsiteUrl,
                existing.TimeZone,
                existing.Locale,
                existing.SupportEmail,
                existing.SupportPhone);

            if (request.Subdomain is not null)
                existing.SetSubdomain(request.Subdomain);

            existing.SetStatus(ParseStatus(request.Status));

            if (request.LogoDocumentId.HasValue)
                existing.SetLogo(request.LogoDocumentId);

            if (request.LogoWhiteDocumentId.HasValue)
                existing.SetLogoWhite(request.LogoWhiteDocumentId);

            await _repository.UpdateAsync(existing, ct);
        }
    }

    private static TenantStatus ParseStatus(string? status) =>
        Enum.TryParse<TenantStatus>(status, ignoreCase: true, out var s) ? s : TenantStatus.Active;

    // ── LS-COMMERCE-ECO-02: Safe Commerce notification helper ─────────────────

    /// <summary>
    /// Sends a Commerce lifecycle event without blocking or throwing into the
    /// caller.  The <see cref="ICommerceLifecycleNotifier"/> contract already
    /// requires implementations to swallow delivery errors; this wrapper adds a
    /// second safety net at the call-site level.
    /// </summary>
    private async Task TryNotifyCommerceAsync(CommerceLifecycleEvent ev, CancellationToken ct)
    {
        try
        {
            await _commerceNotifier.NotifyAsync(ev, ct);
            _logger.LogDebug(
                "Commerce lifecycle notification dispatched: EventType={EventType}, TenantId={TenantId}",
                ev.EventType, ev.ExternalTenantId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Commerce lifecycle notification failed (non-blocking): EventType={EventType}, TenantId={TenantId}",
                ev.EventType, ev.ExternalTenantId);
        }
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
        t.UpdatedAtUtc,
        // BLK-TS-02 — provisioning state
        ProvisioningStatus:    t.ProvisioningStatus.ToString(),
        ProvisionedAtUtc:      t.ProvisionedAtUtc,
        LastProvisioningError: t.LastProvisioningError,
        WorkspaceUrl:          t.WorkspaceUrl,
        CreatedByUserId:       t.CreatedByUserId);

    private string ComposeWorkspaceUrl(string subdomain) =>
        $"{subdomain}.{_platformBaseDomain}";

    private static string NormalizeBaseDomain(string value) =>
        value.Trim()
            .ToLowerInvariant()
            .Replace("https://", string.Empty, StringComparison.Ordinal)
            .Replace("http://", string.Empty, StringComparison.Ordinal)
            .Trim('/')
            .Trim('.');
}
