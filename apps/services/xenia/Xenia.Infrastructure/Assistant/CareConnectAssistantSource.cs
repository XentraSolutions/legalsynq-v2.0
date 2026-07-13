using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xenia.Application.Assistant;

namespace Xenia.Infrastructure.Assistant;

internal sealed class CareConnectAssistantSource : ICareConnectAssistantSource
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
        var detail = await SendAsync<ReferralWire>($"/api/referrals/{referralId}", ct);
        if (!detail.Succeeded || detail.Value is null)
        {
            return new CareConnectReferralLookupOutcome(
                detail.Succeeded,
                detail.Status,
                detail.SafeError,
                null);
        }

        var history = await SendAsync<List<ReferralHistoryWire>>($"/api/referrals/{referralId}/history", ct);
        var maxHistoryItems = Math.Max(1, _options.Value.CareConnect.MaxHistoryItems);

        var normalizedHistory = history.Value?
            .OrderByDescending(item => item.ChangedAtUtc)
            .Take(maxHistoryItems)
            .Select(item => new CareConnectReferralHistoryLookupItem(
                item.OldStatus,
                item.NewStatus,
                item.ChangedAtUtc,
                NormalizeNotes(item.Notes)))
            .ToList()
            ?? [];

        var referral = new CareConnectReferralLookupResult(
            detail.Value.Id,
            detail.Value.Status,
            detail.Value.Urgency,
            detail.Value.ProviderName,
            BuildClientDisplayName(detail.Value.ClientFirstName, detail.Value.ClientLastName),
            NullIfWhiteSpace(detail.Value.RequestedService),
            NullIfWhiteSpace(detail.Value.TreatmentTypeName),
            NullIfWhiteSpace(detail.Value.CaseNumber),
            NullIfWhiteSpace(detail.Value.ReferringOrganizationName),
            NullIfWhiteSpace(detail.Value.ReferrerName),
            detail.Value.CreatedAtUtc,
            detail.Value.UpdatedAtUtc,
            normalizedHistory);

        return new CareConnectReferralLookupOutcome(
            true,
            history.Succeeded ? "completed" : "completed_with_partial_history",
            history.Succeeded ? null : history.SafeError,
            referral);
    }

    private async Task<HttpLookupResult<T>> SendAsync<T>(string path, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        ApplyCallerHeaders(request);

        try
        {
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return HttpLookupResult<T>.Fail(
                    "not_found",
                    "CareConnect referral not found or not accessible.");
            }

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                return HttpLookupResult<T>.Fail(
                    "forbidden",
                    "You are not authorized to access this CareConnect referral.");
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "CareConnect assistant lookup failed. path={Path} status={StatusCode}",
                    path,
                    (int)response.StatusCode);

                return HttpLookupResult<T>.Fail(
                    "service_unavailable",
                    "CareConnect referral lookup is currently unavailable.");
            }

            var value = await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
            return value is null
                ? HttpLookupResult<T>.Fail("empty", "CareConnect returned an empty response.")
                : HttpLookupResult<T>.Success(value);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return HttpLookupResult<T>.Fail(
                "timeout",
                "CareConnect referral lookup timed out.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "CareConnect assistant lookup transport failure. path={Path}", path);
            return HttpLookupResult<T>.Fail(
                "service_unavailable",
                "CareConnect referral lookup is currently unavailable.");
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

    private static string BuildClientDisplayName(string firstName, string lastName)
    {
        var combined = string.Join(' ', new[] { firstName?.Trim(), lastName?.Trim() }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
        return string.IsNullOrWhiteSpace(combined) ? "Unnamed client" : combined;
    }

    private static string? NormalizeNotes(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes)) return null;
        var trimmed = notes.Trim();
        return trimmed.Length <= 160 ? trimmed : trimmed[..160];
    }

    private static string? NullIfWhiteSpace(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record HttpLookupResult<T>(
        bool Succeeded,
        string Status,
        string? SafeError,
        T? Value)
    {
        public static HttpLookupResult<T> Success(T value)
            => new(true, "completed", null, value);

        public static HttpLookupResult<T> Fail(string status, string safeError)
            => new(false, status, safeError, default);
    }

    private sealed class ReferralWire
    {
        public Guid Id { get; init; }
        public string Status { get; init; } = string.Empty;
        public string Urgency { get; init; } = string.Empty;
        public string ProviderName { get; init; } = string.Empty;
        public string ClientFirstName { get; init; } = string.Empty;
        public string ClientLastName { get; init; } = string.Empty;
        public string? RequestedService { get; init; }
        public string? TreatmentTypeName { get; init; }
        public string? CaseNumber { get; init; }
        public string? ReferringOrganizationName { get; init; }
        public string? ReferrerName { get; init; }
        public DateTime CreatedAtUtc { get; init; }
        public DateTime UpdatedAtUtc { get; init; }
    }

    private sealed class ReferralHistoryWire
    {
        public string OldStatus { get; init; } = string.Empty;
        public string NewStatus { get; init; } = string.Empty;
        public DateTime ChangedAtUtc { get; init; }
        public string? Notes { get; init; }
    }
}
