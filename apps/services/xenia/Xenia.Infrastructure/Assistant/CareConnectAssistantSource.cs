using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xenia.Application.Assistant;

namespace Xenia.Infrastructure.Assistant;

internal sealed class CareConnectAssistantSource : ProductAssistantToolApiSource, ICareConnectAssistantSource
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IOptions<XeniaAssistantOptions> _options;
    private readonly ILogger<CareConnectAssistantSource> _logger;

    public CareConnectAssistantSource(
        HttpClient http,
        IHttpContextAccessor httpContextAccessor,
        IOptions<XeniaAssistantOptions> options,
        ILogger<CareConnectAssistantSource> logger)
    {
        _http = http;
        _httpContextAccessor = httpContextAccessor;
        _options = options;
        _logger = logger;
    }

    public async Task<CareConnectReferralLookupOutcome> LookupReferralAsync(
        Guid referralId,
        CancellationToken ct = default)
    {
        var response = await SendAsync<CareConnectReferralLookupOutcome>(
            $"{BuildAssistantToolPath($"referrals/{referralId}")}{BuildQueryString(new Dictionary<string, object?>
            {
                ["historyTop"] = Math.Max(1, _options.Value.CareConnect.MaxHistoryItems),
            })}",
            "CareConnect referral",
            ct);

        return response.Succeeded && response.Value is not null
            ? response.Value
            : new CareConnectReferralLookupOutcome(false, response.Status, response.SafeError, null);
    }

    public async Task<CareConnectReferralHistoryLookupOutcome> LookupReferralHistoryAsync(
        Guid referralId,
        int top,
        CancellationToken ct = default)
    {
        var response = await SendAsync<CareConnectReferralHistoryLookupOutcome>(
            $"{BuildAssistantToolPath($"referrals/{referralId}/history")}{BuildQueryString(new Dictionary<string, object?>
            {
                ["top"] = Math.Clamp(top, 1, 50),
            })}",
            "CareConnect referral history",
            ct);

        return response.Succeeded && response.Value is not null
            ? response.Value
            : new CareConnectReferralHistoryLookupOutcome(false, response.Status, response.SafeError, null);
    }

    public async Task<CareConnectReferralSearchOutcome> SearchReferralsAsync(
        CareConnectReferralSearchRequest request,
        CancellationToken ct = default)
    {
        var response = await SendAsync<CareConnectReferralSearchOutcome>(
            $"{BuildAssistantToolPath("referrals/search")}{BuildQueryString(new Dictionary<string, object?>
            {
                ["search"] = request.SearchText,
                ["clientName"] = request.ClientName,
                ["caseNumber"] = request.CaseNumber,
                ["providerName"] = request.ProviderName,
                ["referrerName"] = request.ReferrerName,
                ["status"] = request.Status,
                ["createdFrom"] = request.CreatedFromUtc,
                ["createdTo"] = request.CreatedToUtc,
                ["top"] = Math.Clamp(request.Top, 1, 25),
            })}",
            "CareConnect referral search",
            ct);

        return response.Succeeded && response.Value is not null
            ? response.Value
            : new CareConnectReferralSearchOutcome(false, response.Status, response.SafeError, 0, []);
    }

    public async Task<CareConnectProviderSearchOutcome> SearchProvidersAsync(
        CareConnectProviderSearchRequest request,
        CancellationToken ct = default)
    {
        var response = await SendAsync<CareConnectProviderSearchOutcome>(
            $"{BuildAssistantToolPath("providers/search")}{BuildQueryString(new Dictionary<string, object?>
            {
                ["name"] = request.Name,
                ["city"] = request.City,
                ["state"] = request.State,
                ["acceptingReferrals"] = request.AcceptingReferrals,
                ["top"] = Math.Clamp(request.Top, 1, 25),
            })}",
            "CareConnect provider search",
            ct);

        return response.Succeeded && response.Value is not null
            ? response.Value
            : new CareConnectProviderSearchOutcome(false, response.Status, response.SafeError, 0, []);
    }

    public async Task<CareConnectReferrerSearchOutcome> SearchReferrersAsync(
        CareConnectReferrerSearchRequest request,
        CancellationToken ct = default)
    {
        var response = await SendAsync<CareConnectReferrerSearchOutcome>(
            $"{BuildAssistantToolPath("referrers/search")}{BuildQueryString(new Dictionary<string, object?>
            {
                ["search"] = request.SearchText,
                ["referrerName"] = request.ReferrerName,
                ["status"] = request.Status,
                ["top"] = Math.Clamp(request.Top, 1, 15),
            })}",
            "CareConnect referrer search",
            ct);

        return response.Succeeded && response.Value is not null
            ? response.Value
            : new CareConnectReferrerSearchOutcome(false, response.Status, response.SafeError, 0, []);
    }

    public async Task<CareConnectReferralQueueSummaryOutcome> GetReferralQueueSummaryAsync(
        CareConnectReferralQueueSummaryRequest request,
        CancellationToken ct = default)
    {
        var response = await SendAsync<CareConnectReferralQueueSummaryOutcome>(
            $"{BuildAssistantToolPath("referrals/queue-summary")}{BuildQueryString(new Dictionary<string, object?>
            {
                ["search"] = request.SearchText,
                ["providerName"] = request.ProviderName,
                ["referrerName"] = request.ReferrerName,
                ["recentTop"] = Math.Clamp(request.RecentTop, 1, 10),
            })}",
            "CareConnect referral queue summary",
            ct);

        return response.Succeeded && response.Value is not null
            ? response.Value
            : new CareConnectReferralQueueSummaryOutcome(false, response.Status, response.SafeError, 0, [], []);
    }

    private async Task<HttpLookupResult<T>> SendAsync<T>(
        string path,
        string resourceLabel,
        CancellationToken ct)
    {
        EnsureAssistantToolPath(path);

        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        ApplyCallerHeaders(request);

        try
        {
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return HttpLookupResult<T>.Fail(
                    "not_found",
                    $"{resourceLabel} was not found or is not accessible.");
            }

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                return HttpLookupResult<T>.Fail(
                    "forbidden",
                    $"You are not authorized to access {resourceLabel.ToLowerInvariant()}.");
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "CareConnect assistant tool request failed. path={Path} status={StatusCode}",
                    path,
                    (int)response.StatusCode);

                return HttpLookupResult<T>.Fail(
                    "service_unavailable",
                    $"{resourceLabel} is currently unavailable.");
            }

            var value = await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
            return value is null
                ? HttpLookupResult<T>.Fail("empty", $"{resourceLabel} returned an empty response.")
                : HttpLookupResult<T>.Success(value);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return HttpLookupResult<T>.Fail(
                "timeout",
                $"{resourceLabel} timed out.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "CareConnect assistant tool transport failure. path={Path}", path);
            return HttpLookupResult<T>.Fail(
                "service_unavailable",
                $"{resourceLabel} is currently unavailable.");
        }
    }

    private void ApplyCallerHeaders(HttpRequestMessage request)
    {
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var httpContext = _httpContextAccessor.HttpContext;
        var auth = httpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(auth) &&
            auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                auth["Bearer ".Length..].Trim());
        }

        var correlationId = httpContext?.Request.Headers["X-Correlation-Id"].ToString();
        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            request.Headers.TryAddWithoutValidation("X-Correlation-Id", correlationId);
        }
    }

    private static string BuildQueryString(IReadOnlyDictionary<string, object?> parameters)
    {
        var parts = parameters
            .Where(pair => pair.Value is not null && !string.IsNullOrWhiteSpace(pair.Value.ToString()))
            .Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(FormatQueryValue(pair.Value!))}")
            .ToList();

        return parts.Count == 0 ? string.Empty : $"?{string.Join("&", parts)}";
    }

    private static string FormatQueryValue(object value)
        => value switch
        {
            DateTime dt => dt.ToUniversalTime().ToString("O"),
            bool b => b ? "true" : "false",
            _ => value.ToString() ?? string.Empty,
        };

    private sealed record HttpLookupResult<T>(
        bool Succeeded,
        string Status,
        string? SafeError,
        T? Value)
    {
        public static HttpLookupResult<T> Success(T value) => new(true, "completed", null, value);

        public static HttpLookupResult<T> Fail(string status, string safeError)
            => new(false, status, safeError, default);
    }
}
