using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Billing.Infrastructure.Accounting.Erp.Providers.QuickBooks;

/// <summary>
/// Named HttpClient identifier used by
/// <see cref="QuickBooksTokenProvider"/> when minting a transport
/// client from <see cref="IHttpClientFactory"/>. Pinned so the
/// timeout / handler-pool configuration stays consistent across
/// every refresh call.
/// </summary>
public static class QuickBooksHttpClients
{
    public const string TokenClient = "billing.quickbooks.token";
    public const string ExportClient = "billing.quickbooks.export";
}

/// <summary>
/// MS-BILL-ERP-002 — Singleton, in-process implementation of
/// <see cref="IQuickBooksTokenProvider"/>. Caches the latest
/// access token in memory until 60 seconds before its
/// <c>expires_in</c> boundary, then refreshes against
/// <c>https://oauth.platform.intuit.com/oauth2/v1/tokens/bearer</c>
/// using the configured client id, client secret, and refresh
/// token.
///
/// <para>
/// Singleton lifetime is mandatory: the in-memory token cache and
/// the refresh-serialisation <see cref="SemaphoreSlim"/> only
/// produce the documented "second caller awaits the first"
/// behaviour when every export request resolves the same
/// instance. Transport is built per refresh from
/// <see cref="IHttpClientFactory"/> using the
/// <see cref="QuickBooksHttpClients.TokenClient"/> named client,
/// so handler pooling / lifetime is still managed by the
/// framework.
/// </para>
///
/// <para>
/// Determinism / safety contract:
/// </para>
/// <list type="bullet">
///   <item>Refresh token is read from
///   <see cref="IOptionsMonitor{QuickBooksOptions}"/> per refresh
///   so an operator-driven rotation takes effect on the next
///   refresh boundary without a process restart.</item>
///   <item>Concurrent callers share a single in-flight refresh via
///   <see cref="SemaphoreSlim"/>; the second caller awaits the
///   first instead of issuing a duplicate refresh.</item>
///   <item>The access token, refresh token, and client secret are
///   NEVER logged. Only structured presence/outcome markers are
///   emitted (status code, mapped reason).</item>
///   <item>Every failure path collapses to a typed
///   <see cref="QuickBooksTokenException"/> with a NON-PII
///   <c>Reason</c> string so the calling provider can map it
///   to <c>ProviderUnavailable</c> with a stable failure tag.</item>
/// </list>
/// </summary>
public sealed class QuickBooksTokenProvider : IQuickBooksTokenProvider, IDisposable
{
    /// <summary>
    /// Refresh proactively this many seconds before the documented
    /// <c>expires_in</c> boundary so an in-flight HTTP call cannot
    /// race the expiry.
    /// </summary>
    private const int ExpirySafetyWindowSeconds = 60;

    private readonly IHttpClientFactory _httpFactory;
    private readonly IOptionsMonitor<QuickBooksOptions> _optionsMonitor;
    private readonly ILogger<QuickBooksTokenProvider> _log;
    private readonly TimeProvider _time;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private string? _cachedAccessToken;
    private DateTimeOffset _cachedExpiresAt = DateTimeOffset.MinValue;

    public QuickBooksTokenProvider(
        IHttpClientFactory httpFactory,
        IOptionsMonitor<QuickBooksOptions> optionsMonitor,
        ILogger<QuickBooksTokenProvider> log,
        TimeProvider time)
    {
        _httpFactory = httpFactory ?? throw new ArgumentNullException(nameof(httpFactory));
        _optionsMonitor = optionsMonitor ?? throw new ArgumentNullException(nameof(optionsMonitor));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _time = time ?? TimeProvider.System;
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken ct = default)
    {
        var options = _optionsMonitor.CurrentValue;

        if (!options.HasRequired())
        {
            // Caller (provider) translates this into ProviderUnavailable.
            throw new QuickBooksTokenException(
                "NotConfigured",
                "QuickBooks provider configuration is incomplete.");
        }

        // Fast-path: cached token still inside its safety window.
        var now = _time.GetUtcNow();
        if (!string.IsNullOrEmpty(_cachedAccessToken) && now < _cachedExpiresAt)
        {
            return _cachedAccessToken!;
        }

        await _refreshLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Re-check inside the lock — a concurrent caller may
            // have refreshed while we were waiting.
            now = _time.GetUtcNow();
            if (!string.IsNullOrEmpty(_cachedAccessToken) && now < _cachedExpiresAt)
            {
                return _cachedAccessToken!;
            }

            await RefreshAccessTokenAsync(options, ct).ConfigureAwait(false);
            return _cachedAccessToken!;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private async Task RefreshAccessTokenAsync(QuickBooksOptions options, CancellationToken ct)
    {
        // application/x-www-form-urlencoded body per Intuit OAuth2 docs.
        var body = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = options.RefreshToken,
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, options.ResolveTokenEndpoint())
        {
            Content = body,
        };

        // HTTP Basic auth: base64(clientId:clientSecret).
        var basic = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{options.ClientId}:{options.ClientSecret}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        // Mint a transport per refresh from the factory. Handler
        // pooling lives in the factory; per-request timeout is set
        // here from current options so a rotation is honored without
        // restarting the process. Refresh frequency is bounded by
        // the operator-driven export cadence — we are not in a hot
        // loop.
        using var http = _httpFactory.CreateClient(QuickBooksHttpClients.TokenClient);
        if (options.TimeoutSeconds > 0)
        {
            http.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        }

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, ct).ConfigureAwait(false);
        }
        catch (TaskCanceledException tex) when (!ct.IsCancellationRequested)
        {
            _log.LogWarning(tex, "QuickBooks token refresh timed out.");
            throw new QuickBooksTokenException(
                "TokenEndpointTransport",
                "QuickBooks token endpoint timed out.",
                tex);
        }
        catch (HttpRequestException hex)
        {
            _log.LogWarning(hex, "QuickBooks token refresh transport error.");
            throw new QuickBooksTokenException(
                "TokenEndpointTransport",
                "QuickBooks token endpoint unreachable.",
                hex);
        }

        using (response)
        {
            var statusCode = (int)response.StatusCode;
            if (!response.IsSuccessStatusCode)
            {
                _log.LogWarning(
                    "QuickBooks token refresh rejected. statusCode={StatusCode}",
                    statusCode);
                throw new QuickBooksTokenException(
                    "RefreshRejected",
                    $"QuickBooks token refresh rejected ({statusCode}).");
            }

            string bodyText;
            try
            {
                bodyText = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                throw new QuickBooksTokenException(
                    "MalformedTokenResponse",
                    "QuickBooks token endpoint body unreadable.",
                    ex);
            }

            string accessToken;
            int expiresInSeconds;
            try
            {
                using var doc = JsonDocument.Parse(bodyText);
                if (doc.RootElement.ValueKind != JsonValueKind.Object
                    || !doc.RootElement.TryGetProperty("access_token", out var atProp)
                    || atProp.ValueKind != JsonValueKind.String)
                {
                    throw new QuickBooksTokenException(
                        "MalformedTokenResponse",
                        "QuickBooks token response missing access_token.");
                }
                accessToken = atProp.GetString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(accessToken))
                {
                    throw new QuickBooksTokenException(
                        "MalformedTokenResponse",
                        "QuickBooks token response had empty access_token.");
                }

                // expires_in is documented as integer seconds; default
                // to a conservative 1 hour if absent / unparseable so
                // the next call still refreshes proactively.
                expiresInSeconds = 3600;
                if (doc.RootElement.TryGetProperty("expires_in", out var eiProp))
                {
                    if (eiProp.ValueKind == JsonValueKind.Number && eiProp.TryGetInt32(out var n))
                    {
                        expiresInSeconds = n;
                    }
                    else if (eiProp.ValueKind == JsonValueKind.String
                             && int.TryParse(eiProp.GetString(), out var n2))
                    {
                        expiresInSeconds = n2;
                    }
                }
            }
            catch (JsonException jex)
            {
                throw new QuickBooksTokenException(
                    "MalformedTokenResponse",
                    "QuickBooks token response was not valid JSON.",
                    jex);
            }

            _cachedAccessToken = accessToken;
            _cachedExpiresAt = _time.GetUtcNow()
                .AddSeconds(Math.Max(0, expiresInSeconds - ExpirySafetyWindowSeconds));

            _log.LogInformation(
                "QuickBooks access token refreshed. expiresInSeconds={ExpiresInSeconds}",
                expiresInSeconds);
        }
    }

    public void Dispose()
    {
        _refreshLock.Dispose();
    }
}
