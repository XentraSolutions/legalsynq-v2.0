using System.Net.Http.Json;
using System.Text.Json.Serialization;
using CareConnect.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CareConnect.Infrastructure.Services;

/// <summary>
/// Live HTTP implementation of IOrganizationRelationshipResolver.
///
/// Queries the Identity service admin endpoint to find an active
/// OrganizationRelationship between two organizations:
///
///   GET {BaseUrl}/api/admin/organization-relationships
///         ?sourceOrgId={referringOrgId}&amp;activeOnly=true&amp;pageSize=200
///
/// Resolution logic:
///   1. Send the request with the configured timeout.
///   2. Deserialize the paged response.
///   3. Return the first item whose targetOrganizationId matches receivingOrganizationId.
///   4. On any failure (timeout, 4xx/5xx, network error, parse error) → return null.
///      Referral creation is never blocked by relationship resolution.
///
/// Configured via IdentityServiceOptions (appsettings: "IdentityService").
/// When BaseUrl is null/empty, the resolver skips the HTTP call and returns null immediately.
/// </summary>
public sealed class HttpOrganizationRelationshipResolver : IOrganizationRelationshipResolver
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IdentityServiceOptions _options;
    private readonly ILogger<HttpOrganizationRelationshipResolver> _logger;

    public HttpOrganizationRelationshipResolver(
        IHttpClientFactory httpClientFactory,
        IOptions<IdentityServiceOptions> options,
        ILogger<HttpOrganizationRelationshipResolver> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Guid?> FindActiveRelationshipAsync(
        Guid referringOrganizationId,
        Guid receivingOrganizationId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            _logger.LogDebug(
                "IdentityService:BaseUrl is not configured — skipping relationship resolution.");
            return null;
        }

        try
        {
            using var client = _httpClientFactory.CreateClient("IdentityService");
            client.BaseAddress = new Uri(_options.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);

            var url = $"api/admin/organization-relationships" +
                      $"?sourceOrgId={referringOrganizationId:D}" +
                      $"&activeOnly=true" +
                      $"&pageSize=200";

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

            var response = await client.GetAsync(url, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Identity relationship lookup returned {StatusCode} for source={ReferringOrgId}. " +
                    "Proceeding with null relationship.",
                    (int)response.StatusCode, referringOrganizationId);
                return null;
            }

            var body = await response.Content.ReadFromJsonAsync<OrgRelationshipPagedResponse>(
                cancellationToken: cts.Token);

            if (body?.Items is null || body.Items.Count == 0)
                return null;

            var match = body.Items.FirstOrDefault(item =>
                item.IsActive &&
                item.TargetOrganizationId == receivingOrganizationId);

            if (match is null)
            {
                _logger.LogDebug(
                    "No active relationship found between source={ReferringOrgId} and target={ReceivingOrgId}.",
                    referringOrganizationId, receivingOrganizationId);
            }

            return match?.Id;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Identity relationship lookup timed out for source={ReferringOrgId}. " +
                "Proceeding with null relationship.",
                referringOrganizationId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Identity relationship lookup failed for source={ReferringOrgId}. " +
                "Proceeding with null relationship.",
                referringOrganizationId);
            return null;
        }
    }

    // ── Private response models (scoped to this class — not shared DTOs) ──────

    private sealed class OrgRelationshipPagedResponse
    {
        [JsonPropertyName("items")]
        public List<OrgRelationshipItem>? Items { get; set; }

        [JsonPropertyName("totalCount")]
        public int TotalCount { get; set; }
    }

    private sealed class OrgRelationshipItem
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        [JsonPropertyName("sourceOrganizationId")]
        public Guid SourceOrganizationId { get; set; }

        [JsonPropertyName("targetOrganizationId")]
        public Guid TargetOrganizationId { get; set; }

        [JsonPropertyName("isActive")]
        public bool IsActive { get; set; }
    }
}
