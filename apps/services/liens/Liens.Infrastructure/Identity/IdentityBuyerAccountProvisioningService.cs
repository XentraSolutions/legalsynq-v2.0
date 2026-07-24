using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Liens.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Liens.Infrastructure.Identity;

public sealed class IdentityBuyerAccountProvisioningService : IPublicBuyerAccountProvisioningService
{
    private const int ServiceUnavailableStatusCode = 503;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IdentityServiceOptions _options;
    private readonly ILogger<IdentityBuyerAccountProvisioningService> _logger;
    private readonly bool _isEnabled;

    public IdentityBuyerAccountProvisioningService(
        IHttpClientFactory httpClientFactory,
        IOptions<IdentityServiceOptions> options,
        ILogger<IdentityBuyerAccountProvisioningService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
        _isEnabled = !string.IsNullOrWhiteSpace(_options.BaseUrl);

        if (!_isEnabled)
        {
            _logger.LogWarning(
                "SynqLien public buyer account activation disabled because IdentityService:BaseUrl is not configured.");
        }
    }

    public async Task<PublicBuyerAccountProvisioningResult> ProvisionBuyerAccountAsync(
        PublicBuyerAccountProvisioningRequest request,
        CancellationToken ct = default)
    {
        if (!_isEnabled)
        {
            return PublicBuyerAccountProvisioningResult.Failed(
                "identity-unavailable",
                "Account activation is temporarily unavailable.",
                ServiceUnavailableStatusCode);
        }

        try
        {
            using var client = BuildIdentityClient();
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

            var identityOrg = await EnsureBuyerOrganizationAsync(client, request, timeoutCts.Token);
            if (!identityOrg.Success)
            {
                return PublicBuyerAccountProvisioningResult.Failed(
                    identityOrg.ErrorCode ?? "identity-organization-error",
                    identityOrg.ErrorMessage ?? "Buyer organization could not be prepared for account activation.",
                    identityOrg.StatusCode);
            }

            var body = new
            {
                tenantId = request.TenantId,
                email = request.Email,
                password = request.Password,
                firstName = request.FirstName,
                lastName = request.LastName,
                phone = request.Phone,
            };

            using var response = await client.PostAsJsonAsync(
                $"api/admin/organizations/{identityOrg.OrganizationId}/synqlien-buyer-self-register",
                body,
                timeoutCts.Token);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await ReadErrorResponseAsync(response, timeoutCts.Token);
                var message = errorBody?.Error?.Trim();
                var code = errorBody?.Code?.Trim();

                if (response.StatusCode == HttpStatusCode.Conflict)
                {
                    return PublicBuyerAccountProvisioningResult.Failed(
                        string.IsNullOrWhiteSpace(code) ? "account-conflict" : code.ToLowerInvariant(),
                        string.IsNullOrWhiteSpace(message)
                            ? "An account with this email already exists. Use the existing account password to activate access."
                            : message,
                        (int)response.StatusCode);
                }

                _logger.LogWarning(
                    "Identity SynqLien buyer self-register returned HTTP {Status} for org {OrgId}.",
                    (int)response.StatusCode,
                    request.BuyerOrgId);

                return PublicBuyerAccountProvisioningResult.Failed(
                    "identity-error",
                    string.IsNullOrWhiteSpace(message)
                        ? "Account activation could not be completed."
                        : message,
                    (int)response.StatusCode);
            }

            var result = await response.Content.ReadFromJsonAsync<SelfRegisterResponse>(
                cancellationToken: timeoutCts.Token);

            if (result is null || result.UserId == Guid.Empty)
            {
                _logger.LogWarning(
                    "Identity SynqLien buyer self-register returned an empty user id for org {OrgId}.",
                    request.BuyerOrgId);
                return PublicBuyerAccountProvisioningResult.Failed(
                    "identity-error",
                    "Account activation could not be completed.",
                    ServiceUnavailableStatusCode);
            }

            return PublicBuyerAccountProvisioningResult.Created(result.UserId, result.IsNew);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Identity SynqLien buyer self-register timed out for org {OrgId}.",
                request.BuyerOrgId);
            return PublicBuyerAccountProvisioningResult.Failed(
                "identity-timeout",
                "Account activation timed out. Please try again.",
                ServiceUnavailableStatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Identity SynqLien buyer self-register failed for org {OrgId}.",
                request.BuyerOrgId);
            return PublicBuyerAccountProvisioningResult.Failed(
                "identity-error",
                "Account activation could not be completed.",
                ServiceUnavailableStatusCode);
        }
    }

    private async Task<EnsureBuyerOrganizationResult> EnsureBuyerOrganizationAsync(
        HttpClient client,
        PublicBuyerAccountProvisioningRequest request,
        CancellationToken ct)
    {
        var body = new
        {
            tenantId = request.TenantId,
            sourceBuyerOrgId = request.BuyerOrgId,
            buyerCompanyName = request.BuyerCompanyName,
            contactEmail = request.Email,
        };

        using var response = await client.PostAsJsonAsync(
            "api/admin/organizations/synqlien-buyer",
            body,
            ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await ReadErrorResponseAsync(response, ct);
            var message = errorBody?.Error?.Trim();
            var code = errorBody?.Code?.Trim();

            _logger.LogWarning(
                "Identity SynqLien buyer organization ensure returned HTTP {Status} for source org {SourceOrgId}.",
                (int)response.StatusCode,
                request.BuyerOrgId);

            return EnsureBuyerOrganizationResult.Failed(
                string.IsNullOrWhiteSpace(code) ? "identity-organization-error" : code.ToLowerInvariant(),
                string.IsNullOrWhiteSpace(message)
                    ? "Buyer organization could not be prepared for account activation."
                    : message,
                (int)response.StatusCode);
        }

        var result = await response.Content.ReadFromJsonAsync<EnsureBuyerOrganizationResponse>(
            cancellationToken: ct);

        if (result is null || result.Id == Guid.Empty)
        {
            _logger.LogWarning(
                "Identity SynqLien buyer organization ensure returned an empty id for source org {SourceOrgId}.",
                request.BuyerOrgId);
            return EnsureBuyerOrganizationResult.Failed(
                "identity-organization-error",
                "Buyer organization could not be prepared for account activation.",
                ServiceUnavailableStatusCode);
        }

        return EnsureBuyerOrganizationResult.Created(result.Id);
    }

    private HttpClient BuildIdentityClient()
    {
        var client = _httpClientFactory.CreateClient("IdentityService");
        client.BaseAddress = new Uri(_options.BaseUrl!.TrimEnd('/') + "/");
        client.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);

        if (!string.IsNullOrWhiteSpace(_options.ProvisioningToken))
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation(
                "X-Provisioning-Token",
                _options.ProvisioningToken);
        }
        else if (!string.IsNullOrWhiteSpace(_options.AuthHeaderName) &&
                 !string.IsNullOrWhiteSpace(_options.AuthHeaderValue))
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation(
                _options.AuthHeaderName,
                _options.AuthHeaderValue);
        }

        return client;
    }

    private static async Task<ErrorResponse?> ReadErrorResponseAsync(
        HttpResponseMessage response,
        CancellationToken ct)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<ErrorResponse>(
                cancellationToken: ct);
        }
        catch
        {
            return null;
        }
    }

    private sealed class SelfRegisterResponse
    {
        [JsonPropertyName("userId")]
        public Guid UserId { get; set; }

        [JsonPropertyName("isNew")]
        public bool IsNew { get; set; }
    }

    private sealed class EnsureBuyerOrganizationResponse
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }
    }

    private sealed class ErrorResponse
    {
        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("code")]
        public string? Code { get; set; }
    }

    private sealed record EnsureBuyerOrganizationResult(
        bool Success,
        Guid? OrganizationId,
        string? ErrorCode,
        string? ErrorMessage,
        int? StatusCode)
    {
        public static EnsureBuyerOrganizationResult Created(Guid organizationId)
            => new(true, organizationId, null, null, null);

        public static EnsureBuyerOrganizationResult Failed(
            string errorCode,
            string errorMessage,
            int statusCode)
            => new(false, null, errorCode, errorMessage, statusCode);
    }
}
