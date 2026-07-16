using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xenia.Application.Assistant;

namespace Xenia.Infrastructure.Assistant;

internal sealed class SynqLienAssistantSource : ProductAssistantToolApiSource, ISynqLienAssistantSource
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<SynqLienAssistantSource> _logger;

    public SynqLienAssistantSource(
        HttpClient http,
        IHttpContextAccessor httpContextAccessor,
        ILogger<SynqLienAssistantSource> logger)
    {
        _http = http;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<SynqLienLienLookupOutcome> LookupLienAsync(
        SynqLienLienLookupRequest request,
        CancellationToken ct = default)
    {
        var path = request.LienId.HasValue
            ? BuildAssistantToolPath($"liens/{request.LienId.Value}")
            : !string.IsNullOrWhiteSpace(request.LienNumber)
                ? BuildAssistantToolPath($"liens/by-number/{Uri.EscapeDataString(request.LienNumber.Trim())}")
                : string.Empty;

        if (string.IsNullOrWhiteSpace(path))
            return new SynqLienLienLookupOutcome(false, "invalid_input", "The SynqLien lien id or lien number is required.", null);

        var response = await SendAsync<SynqLienLienLookupOutcome>(path, "SynqLien lien", ct);
        return response.Succeeded && response.Value is not null
            ? response.Value
            : new SynqLienLienLookupOutcome(false, response.Status, response.SafeError, null);
    }

    public async Task<SynqLienLienSearchOutcome> SearchLiensAsync(
        SynqLienLienSearchRequest request,
        CancellationToken ct = default)
    {
        var response = await SendAsync<SynqLienLienSearchOutcome>(
            $"{BuildAssistantToolPath("liens/search")}{BuildQueryString(new Dictionary<string, object?>
            {
                ["search"] = request.SearchText,
                ["subjectName"] = request.SubjectName,
                ["caseNumber"] = request.CaseNumber,
                ["status"] = request.Status,
                ["statusGroup"] = request.StatusGroup,
                ["lienType"] = request.LienType,
                ["createdFrom"] = request.CreatedFromUtc,
                ["createdTo"] = request.CreatedToUtc,
                ["top"] = Math.Clamp(request.Top, 1, 25),
            })}",
            "SynqLien lien search",
            ct);

        return response.Succeeded && response.Value is not null
            ? response.Value
            : new SynqLienLienSearchOutcome(false, response.Status, response.SafeError, 0, []);
    }

    public async Task<SynqLienQueueSummaryOutcome> GetLienQueueSummaryAsync(
        SynqLienQueueSummaryRequest request,
        CancellationToken ct = default)
    {
        var response = await SendAsync<SynqLienQueueSummaryOutcome>(
            $"{BuildAssistantToolPath("liens/queue-summary")}{BuildQueryString(new Dictionary<string, object?>
            {
                ["search"] = request.SearchText,
                ["subjectName"] = request.SubjectName,
                ["caseNumber"] = request.CaseNumber,
                ["status"] = request.Status,
                ["statusGroup"] = request.StatusGroup,
                ["lienType"] = request.LienType,
                ["days"] = request.Days,
                ["createdFrom"] = request.CreatedFromUtc,
                ["createdTo"] = request.CreatedToUtc,
                ["recentTop"] = Math.Clamp(request.RecentTop, 1, 10),
            })}",
            "SynqLien lien queue summary",
            ct);

        return response.Succeeded && response.Value is not null
            ? response.Value
            : new SynqLienQueueSummaryOutcome(false, response.Status, response.SafeError, 0, 0, 0, 0, 0, 0, null, null, null, null, [], []);
    }

    public async Task<SynqLienCaseLookupOutcome> LookupCaseAsync(
        SynqLienCaseLookupRequest request,
        CancellationToken ct = default)
    {
        var path = request.CaseId.HasValue
            ? BuildAssistantToolPath($"cases/{request.CaseId.Value}")
            : !string.IsNullOrWhiteSpace(request.CaseNumber)
                ? BuildAssistantToolPath($"cases/by-number/{Uri.EscapeDataString(request.CaseNumber.Trim())}")
                : string.Empty;

        if (string.IsNullOrWhiteSpace(path))
            return new SynqLienCaseLookupOutcome(false, "invalid_input", "The SynqLien case id or case number is required.", null);

        var query = BuildQueryString(new Dictionary<string, object?>
        {
            ["liensTop"] = Math.Clamp(request.LiensTop, 1, 25),
        });

        var response = await SendAsync<SynqLienCaseLookupOutcome>(
            $"{path}{query}",
            "SynqLien case",
            ct);

        return response.Succeeded && response.Value is not null
            ? response.Value
            : new SynqLienCaseLookupOutcome(false, response.Status, response.SafeError, null);
    }

    public async Task<SynqLienCaseSearchOutcome> SearchCasesAsync(
        SynqLienCaseSearchRequest request,
        CancellationToken ct = default)
    {
        var response = await SendAsync<SynqLienCaseSearchOutcome>(
            $"{BuildAssistantToolPath("cases/search")}{BuildQueryString(new Dictionary<string, object?>
            {
                ["search"] = request.SearchText,
                ["clientName"] = request.ClientName,
                ["caseNumber"] = request.CaseNumber,
                ["status"] = request.Status,
                ["top"] = Math.Clamp(request.Top, 1, 25),
            })}",
            "SynqLien case search",
            ct);

        return response.Succeeded && response.Value is not null
            ? response.Value
            : new SynqLienCaseSearchOutcome(false, response.Status, response.SafeError, 0, []);
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
                    "SynqLien assistant tool request failed. path={Path} status={StatusCode}",
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
            _logger.LogWarning(ex, "SynqLien assistant tool transport failure. path={Path}", path);
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
