// LSCC-010: HTTP client that calls the Identity service to create/resolve
// a minimal PROVIDER Organization for the auto-provision flow.
//
// Failure policy: ALL failures (network, 4xx, 5xx, parse) return null.
// The caller (AutoProvisionService) interprets null as "fall back to LSCC-009".
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using CareConnect.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CareConnect.Infrastructure.Services;

public sealed class HttpIdentityOrganizationService : IIdentityOrganizationService
{
    private readonly IHttpClientFactory                          _httpClientFactory;
    private readonly IdentityServiceOptions                      _options;
    private readonly ILogger<HttpIdentityOrganizationService>   _logger;
    private readonly bool                                        _isEnabled;

    public HttpIdentityOrganizationService(
        IHttpClientFactory                        httpClientFactory,
        IOptions<IdentityServiceOptions>          options,
        ILogger<HttpIdentityOrganizationService>  logger)
    {
        _httpClientFactory = httpClientFactory;
        _options           = options.Value;
        _logger            = logger;
        _isEnabled         = !string.IsNullOrWhiteSpace(_options.BaseUrl);

        if (!_isEnabled)
        {
            _logger.LogWarning(
                "LSCC-010 IdentityService:BaseUrl not configured. " +
                "Auto-provisioning org creation is disabled — " +
                "all auto-provision calls will fall back to LSCC-009.");
        }
    }

    public async Task<Guid?> EnsureProviderOrganizationAsync(
        Guid              tenantId,
        Guid              providerCcId,
        string            providerName,
        CancellationToken ct = default)
    {
        if (!_isEnabled)
        {
            _logger.LogDebug(
                "LSCC-010 Identity org creation skipped (BaseUrl not configured) for provider {ProviderId}.",
                providerCcId);
            return null;
        }

        try
        {
            using var client = _httpClientFactory.CreateClient("IdentityService");
            client.BaseAddress = new Uri(_options.BaseUrl!.TrimEnd('/') + "/");
            client.Timeout     = TimeSpan.FromSeconds(_options.TimeoutSeconds);

            if (!string.IsNullOrWhiteSpace(_options.AuthHeaderName) &&
                !string.IsNullOrWhiteSpace(_options.AuthHeaderValue))
            {
                client.DefaultRequestHeaders.TryAddWithoutValidation(
                    _options.AuthHeaderName, _options.AuthHeaderValue);
            }

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

            var body = new
            {
                tenantId     = tenantId,
                providerCcId = providerCcId,
                providerName = providerName,
            };

            using var response = await client.PostAsJsonAsync(
                "api/admin/organizations", body, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "LSCC-010 Identity org creation returned HTTP {Status} for provider {ProviderId}. Fallback triggered.",
                    (int)response.StatusCode, providerCcId);
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<CreateProviderOrgResponse>(
                cancellationToken: cts.Token);

            if (result is null || result.Id == Guid.Empty)
            {
                _logger.LogWarning(
                    "LSCC-010 Identity org creation returned null or empty Id for provider {ProviderId}.",
                    providerCcId);
                return null;
            }

            _logger.LogInformation(
                "LSCC-010 Identity org {OrgId} {IsNew} for provider {ProviderId}.",
                result.Id,
                result.IsNew ? "created" : "already exists",
                providerCcId);

            return result.Id;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(
                "LSCC-010 Identity org creation timed out for provider {ProviderId}. Fallback triggered.",
                providerCcId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "LSCC-010 Identity org creation failed for provider {ProviderId}. Fallback triggered.",
                providerCcId);
            return null;
        }
    }

    // ── CC2-INT-B04: Token → Identity Bridge — user invitation ───────────────

    /// <inheritdoc />
    public async Task<ProvisionProviderUserResult?> InviteProviderUserAsync(
        Guid              orgId,
        string            email,
        string            firstName,
        string?           lastName,
        CancellationToken ct = default)
    {
        if (!_isEnabled)
        {
            _logger.LogDebug(
                "CC2-INT-B04 Identity user invitation skipped (BaseUrl not configured) for org {OrgId}.",
                orgId);
            return null;
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            _logger.LogDebug(
                "CC2-INT-B04 Identity user invitation skipped (no email) for org {OrgId}.", orgId);
            return null;
        }

        try
        {
            using var client = _httpClientFactory.CreateClient("IdentityService");
            client.BaseAddress = new Uri(_options.BaseUrl!.TrimEnd('/') + "/");
            client.Timeout     = TimeSpan.FromSeconds(_options.TimeoutSeconds);

            if (!string.IsNullOrWhiteSpace(_options.AuthHeaderName) &&
                !string.IsNullOrWhiteSpace(_options.AuthHeaderValue))
            {
                client.DefaultRequestHeaders.TryAddWithoutValidation(
                    _options.AuthHeaderName, _options.AuthHeaderValue);
            }

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

            var body = new
            {
                email     = email,
                firstName = firstName,
                lastName  = lastName,
            };

            using var response = await client.PostAsJsonAsync(
                $"api/admin/organizations/{orgId}/provision-user", body, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "CC2-INT-B04 Identity user provision returned HTTP {Status} for org {OrgId}. " +
                    "Invitation not sent — provider org link is still valid.",
                    (int)response.StatusCode, orgId);
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<ProvisionProviderUserResponse>(
                cancellationToken: cts.Token);

            if (result is null || result.UserId == Guid.Empty)
            {
                _logger.LogWarning(
                    "CC2-INT-B04 Identity user provision returned null/empty userId for org {OrgId}.",
                    orgId);
                return null;
            }

            _logger.LogInformation(
                "CC2-INT-B04 Identity user {UserId} {IsNew} for org {OrgId}. InvitationSent={InvitationSent}.",
                result.UserId,
                result.IsNew ? "created" : "already existed",
                orgId,
                result.InvitationSent);

            return new ProvisionProviderUserResult
            {
                UserId         = result.UserId,
                InvitationId   = result.InvitationId,
                IsNew          = result.IsNew,
                InvitationSent = result.InvitationSent,
            };
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(
                "CC2-INT-B04 Identity user provision timed out for org {OrgId}. Invitation not sent.", orgId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "CC2-INT-B04 Identity user provision failed for org {OrgId}. Invitation not sent.", orgId);
            return null;
        }
    }

    // ── Private response models ───────────────────────────────────────────────

    private sealed class CreateProviderOrgResponse
    {
        [JsonPropertyName("id")]
        public Guid   Id    { get; set; }

        [JsonPropertyName("name")]
        public string Name  { get; set; } = string.Empty;

        [JsonPropertyName("isNew")]
        public bool   IsNew { get; set; }
    }

    private sealed class ProvisionProviderUserResponse
    {
        [JsonPropertyName("userId")]
        public Guid  UserId         { get; set; }

        [JsonPropertyName("invitationId")]
        public Guid? InvitationId   { get; set; }

        [JsonPropertyName("isNew")]
        public bool  IsNew          { get; set; }

        [JsonPropertyName("invitationSent")]
        public bool  InvitationSent { get; set; }
    }
}
