using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BuildingBlocks.Commerce;

/// <summary>
/// HTTP implementation of <see cref="ICommerceEntitlementClient"/> that calls
/// Commerce's host-integration endpoints to resolve entitlement snapshots.
///
/// <para>
/// Endpoints called:
/// <list type="bullet">
///   <item><description>
///     <c>GET /api/commerce/integration/host-tenants/{key}/{id}/entitlement-snapshot</c>
///     — used by <see cref="GetByHostTenantAsync"/>.
///   </description></item>
///   <item><description>
///     <c>GET /api/commerce/integration/billing-accounts/{id}/entitlement-snapshot</c>
///     — used by <see cref="GetByBillingAccountAsync"/>.
///   </description></item>
/// </list>
/// </para>
///
/// <para>
/// This class intentionally contains private mirror DTOs that replicate the
/// Commerce response shape without importing <c>Commerce.Contracts</c>. This
/// keeps consuming services decoupled from Commerce's internal contract versions.
/// </para>
///
/// <para>
/// Never throws — all exceptions are caught and surfaced as
/// <see cref="CommerceEntitlementResult.Error"/>.
/// </para>
/// </summary>
internal sealed class HttpCommerceEntitlementClient : ICommerceEntitlementClient
{
    private readonly HttpClient _http;
    private readonly CommerceIntegrationOptions _options;
    private readonly ILogger<HttpCommerceEntitlementClient> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition      = JsonIgnoreCondition.WhenWritingNull,
        Converters                  = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public HttpCommerceEntitlementClient(
        HttpClient                             http,
        IOptions<CommerceIntegrationOptions>   options,
        ILogger<HttpCommerceEntitlementClient> logger)
    {
        _http    = http;
        _options = options.Value;
        _logger  = logger;
    }

    /// <inheritdoc />
    public Task<CommerceEntitlementResult> GetByHostTenantAsync(
        string            hostPlatformKey,
        string            externalTenantId,
        CancellationToken ct = default)
    {
        var url = $"api/commerce/integration/host-tenants/" +
                  $"{Uri.EscapeDataString(hostPlatformKey)}/" +
                  $"{Uri.EscapeDataString(externalTenantId)}/entitlement-snapshot";
        return FetchSnapshotAsync(url, ct);
    }

    /// <inheritdoc />
    public Task<CommerceEntitlementResult> GetByBillingAccountAsync(
        Guid              billingAccountId,
        CancellationToken ct = default)
    {
        var url = $"api/commerce/integration/billing-accounts/{billingAccountId:D}/entitlement-snapshot";
        return FetchSnapshotAsync(url, ct);
    }

    // ── internal ──────────────────────────────────────────────────────────────

    private async Task<CommerceEntitlementResult> FetchSnapshotAsync(
        string            url,
        CancellationToken ct)
    {
        try
        {
            using var response = await _http.GetAsync(url, ct).ConfigureAwait(false);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogDebug(
                    "Commerce entitlement snapshot not found at {Url} — tenant not registered in Commerce.",
                    url);
                return CommerceEntitlementResult.Unavailable(
                    "Tenant is not registered in Commerce (no billing account mapping).");
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Commerce entitlement call to {Url} returned {StatusCode}.",
                    url, (int)response.StatusCode);
                return CommerceEntitlementResult.Error(
                    $"Commerce returned HTTP {(int)response.StatusCode}.");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            var snapshot = await JsonSerializer
                .DeserializeAsync<CommerceSnapshotResponse>(stream, _jsonOptions, ct)
                .ConfigureAwait(false);

            if (snapshot is null)
            {
                _logger.LogWarning("Commerce returned empty/null snapshot body from {Url}.", url);
                return CommerceEntitlementResult.Error("Commerce returned an empty snapshot body.");
            }

            return MapSnapshot(snapshot);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Commerce entitlement HTTP request failed for {Url}.", url);
            return CommerceEntitlementResult.Error($"HTTP connection error: {ex.Message}");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning("Commerce entitlement request timed out for {Url}.", url);
            return CommerceEntitlementResult.Error("Commerce entitlement request timed out.");
        }
        catch (OperationCanceledException)
        {
            return CommerceEntitlementResult.Error("Request was cancelled.");
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize Commerce snapshot response from {Url}.", url);
            return CommerceEntitlementResult.Error($"JSON deserialize error: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching Commerce entitlement from {Url}.", url);
            return CommerceEntitlementResult.Error($"Unexpected error: {ex.GetType().Name}");
        }
    }

    private static CommerceEntitlementResult MapSnapshot(CommerceSnapshotResponse s)
    {
        var productKeys = s.Products?
            .Where(p => !string.IsNullOrWhiteSpace(p.ProductKey))
            .Select(p => p.ProductKey!)
            .ToList() ?? new List<string>();

        var plans = s.Plans?
            .Where(pl => !string.IsNullOrWhiteSpace(pl.PlanKey))
            .Select(pl => new CommerceEntitlementPlan(
                pl.PlanKey!,
                pl.PlanName ?? pl.PlanKey!,
                pl.ProductKey))
            .ToList() ?? new List<CommerceEntitlementPlan>();

        return new CommerceEntitlementResult(
            IsAvailable:            true,
            AccessRecommendation:   s.AccessRecommendation ?? CommerceAccessRecommendationValues.Unknown,
            AccountStandingStatus:  s.AccountStandingStatus ?? "Unknown",
            AccountStandingReason:  s.AccountStandingReason,
            ProductKeys:            productKeys,
            Plans:                  plans,
            SnapshotGeneratedAtUtc: s.GeneratedAtUtc,
            BillingAccountId:       s.BillingAccountId?.ToString(),
            ExternalTenantId:       s.ExternalTenantId);
    }

    // ── Private mirror DTOs ────────────────────────────────────────────────────
    // These replicate the Commerce.Contracts.Integration shape without a direct
    // project dependency. Kept in sync by contract — update when Commerce's
    // integration endpoint response shape changes.

    private sealed class CommerceSnapshotResponse
    {
        public Guid?                    BillingAccountId        { get; init; }
        public string?                  AccountNumber           { get; init; }
        public string?                  DisplayName             { get; init; }
        public string?                  HostPlatformKey         { get; init; }
        public string?                  ExternalTenantId        { get; init; }
        public string?                  AccountStandingStatus   { get; init; }
        public string?                  AccountStandingReason   { get; init; }
        public string?                  AccessRecommendation    { get; init; }
        public SnapshotProductRef[]?    Products                { get; init; }
        public SnapshotPlanRef[]?       Plans                   { get; init; }
        public DateTimeOffset?          GeneratedAtUtc          { get; init; }
    }

    private sealed class SnapshotProductRef
    {
        public Guid?   ProductId   { get; init; }
        public string? ProductKey  { get; init; }
        public string? ProductName { get; init; }
    }

    private sealed class SnapshotPlanRef
    {
        public Guid?   PlanId      { get; init; }
        public string? PlanKey     { get; init; }
        public string? PlanName    { get; init; }
        public Guid?   ProductId   { get; init; }
        public string? ProductKey  { get; init; }
    }
}
