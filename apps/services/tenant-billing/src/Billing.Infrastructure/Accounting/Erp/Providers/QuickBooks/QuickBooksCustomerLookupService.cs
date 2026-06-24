using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Billing.Domain.Accounting.Erp.Remediation;

namespace Billing.Infrastructure.Accounting.Erp.Providers.QuickBooks;

/// <summary>
/// MS-BILL-ERP-005 — server-side adapter for the governed
/// QuickBooks Online customer search. Implements
/// <see cref="IQuickBooksCustomerLookup"/> by composing
/// <see cref="IQuickBooksTokenProvider"/> with a typed
/// <see cref="HttpClient"/> targeted at the QBO REST query and
/// customer-by-id endpoints.
///
/// <para>
/// Strategic stance: <b>read-only, server-side, bounded</b>. The
/// adapter NEVER creates a QBO customer, NEVER mutates a QBO row,
/// NEVER fuzzy-matches on the result set (the operator confirms
/// the explicit QBO id), and NEVER caps the result set above the
/// QBO `MAXRESULTS` ceiling of 25.
/// </para>
///
/// <para>
/// Failure mapping mirrors
/// <see cref="QuickBooksAccountingExportProvider"/>:
/// </para>
/// <list type="bullet">
///   <item>!IsConfigured → <see cref="QuickBooksCustomerLookupOutcome.ConfigurationRequired"/>.</item>
///   <item>token refresh failure / 401 / 403 →
///   <see cref="QuickBooksCustomerLookupOutcome.ProviderUnavailable"/>.</item>
///   <item>429 / 5xx / transport timeout →
///   <see cref="QuickBooksCustomerLookupOutcome.ProviderUnavailable"/>.</item>
///   <item>404 (GetById only) → null hit, no exception.</item>
///   <item>400 / unexpected status →
///   <see cref="QuickBooksCustomerLookupOutcome.Failed"/> with the
///   capped status text as the reason.</item>
/// </list>
///
/// <para>
/// Logging discipline: NEVER logs the access token, refresh token,
/// client secret, realm id, or full request/response bodies. Logs
/// only the operation, status code, and hit count.
/// </para>
/// </summary>
public sealed class QuickBooksCustomerLookupService : IQuickBooksCustomerLookup
{
    private const int FailureReasonCapChars = 500;
    private const int MaxResults = 25;

    private readonly HttpClient _http;
    private readonly IOptionsMonitor<QuickBooksOptions> _optionsMonitor;
    private readonly IQuickBooksTokenProvider _tokenProvider;
    private readonly ILogger<QuickBooksCustomerLookupService> _log;

    public QuickBooksCustomerLookupService(
        HttpClient http,
        IOptionsMonitor<QuickBooksOptions> optionsMonitor,
        IQuickBooksTokenProvider tokenProvider,
        ILogger<QuickBooksCustomerLookupService> log)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _optionsMonitor = optionsMonitor ?? throw new ArgumentNullException(nameof(optionsMonitor));
        _tokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));
        _log = log ?? throw new ArgumentNullException(nameof(log));

        var snapshot = _optionsMonitor.CurrentValue;
        if (snapshot.TimeoutSeconds > 0)
        {
            _http.Timeout = TimeSpan.FromSeconds(snapshot.TimeoutSeconds);
        }
    }

    public bool IsConfigured => _optionsMonitor.CurrentValue.HasRequired();

    public async Task<QuickBooksCustomerSearchResult> SearchByDisplayNameAsync(
        string query,
        CancellationToken ct = default)
    {
        var options = _optionsMonitor.CurrentValue;
        if (!options.HasRequired())
        {
            return QuickBooksCustomerSearchResult.ConfigurationRequired();
        }
        var trimmed = (query ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            return QuickBooksCustomerSearchResult.Ok(Array.Empty<QuickBooksCustomerSearchHit>());
        }

        string accessToken;
        try
        {
            accessToken = await _tokenProvider.GetAccessTokenAsync(ct).ConfigureAwait(false);
        }
        catch (QuickBooksTokenException tex)
        {
            _log.LogWarning("QBO customer-search: token failure ({Reason})", tex.Reason);
            return QuickBooksCustomerLookupTokenOutcome(tex);
        }

        // Single-quote escape per QBO query language: literal single
        // quotes inside the LIKE value must be escaped as '\''. We
        // strip control characters and cap length so the operator
        // cannot smuggle a multi-clause query.
        var safe = SanitizeSearchTerm(trimmed);
        var qboQuery =
            $"select Id, DisplayName, Active, PrimaryEmailAddr from Customer " +
            $"where DisplayName LIKE '%{safe}%' MAXRESULTS {MaxResults}";

        var url = $"{options.ResolveApiBaseUrl().TrimEnd('/')}" +
                  $"/v3/company/{Uri.EscapeDataString(options.RealmId)}/query" +
                  $"?minorversion=70&query={Uri.EscapeDataString(qboQuery)}";

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        HttpResponseMessage resp;
        try
        {
            resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return QuickBooksCustomerSearchResult.ProviderUnavailable(
                "QuickBooks customer search timed out.");
        }
        catch (HttpRequestException hex)
        {
            _log.LogWarning(hex, "QBO customer-search: transport failure");
            return QuickBooksCustomerSearchResult.ProviderUnavailable(
                "QuickBooks transport error during customer search.");
        }

        using (resp)
        {
            if ((int)resp.StatusCode == 401 || (int)resp.StatusCode == 403)
            {
                return QuickBooksCustomerSearchResult.ProviderUnavailable(
                    "QuickBooks rejected the access token during customer search.");
            }
            if ((int)resp.StatusCode == 429 || (int)resp.StatusCode >= 500)
            {
                return QuickBooksCustomerSearchResult.ProviderUnavailable(
                    $"QuickBooks customer search is currently unavailable (status {(int)resp.StatusCode}).");
            }
            if (!resp.IsSuccessStatusCode)
            {
                var body = await SafeReadBodyAsync(resp, ct).ConfigureAwait(false);
                return QuickBooksCustomerSearchResult.Failed(
                    Cap($"QuickBooks customer search failed (status {(int)resp.StatusCode}): {body}"));
            }

            try
            {
                await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
                var hits = ParseQueryResponse(doc);
                _log.LogInformation("QBO customer-search returned {Count} hits", hits.Count);
                return QuickBooksCustomerSearchResult.Ok(hits);
            }
            catch (JsonException jex)
            {
                _log.LogWarning(jex, "QBO customer-search: malformed response");
                return QuickBooksCustomerSearchResult.Failed(
                    "QuickBooks returned a malformed customer-search response.");
            }
        }
    }

    public async Task<QuickBooksCustomerSearchHit?> GetByIdAsync(
        string quickBooksCustomerId,
        CancellationToken ct = default)
    {
        var options = _optionsMonitor.CurrentValue;
        if (!options.HasRequired())
        {
            throw new QuickBooksCustomerLookupException(
                QuickBooksCustomerLookupOutcome.ConfigurationRequired,
                "QuickBooks provider configuration is incomplete.");
        }
        var id = (quickBooksCustomerId ?? string.Empty).Trim();
        if (id.Length == 0)
        {
            throw new QuickBooksCustomerLookupException(
                QuickBooksCustomerLookupOutcome.Failed,
                "QuickBooks customer id is required.");
        }

        string accessToken;
        try
        {
            accessToken = await _tokenProvider.GetAccessTokenAsync(ct).ConfigureAwait(false);
        }
        catch (QuickBooksTokenException tex)
        {
            _log.LogWarning("QBO customer-by-id: token failure ({Reason})", tex.Reason);
            var outcome = TokenOutcomeFor(tex);
            throw new QuickBooksCustomerLookupException(outcome,
                outcome == QuickBooksCustomerLookupOutcome.ConfigurationRequired
                    ? "QuickBooks provider configuration is incomplete."
                    : "QuickBooks rejected the access token.");
        }

        var url = $"{options.ResolveApiBaseUrl().TrimEnd('/')}" +
                  $"/v3/company/{Uri.EscapeDataString(options.RealmId)}/customer/" +
                  $"{Uri.EscapeDataString(id)}?minorversion=70";

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        HttpResponseMessage resp;
        try
        {
            resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new QuickBooksCustomerLookupException(
                QuickBooksCustomerLookupOutcome.ProviderUnavailable,
                "QuickBooks customer lookup timed out.");
        }
        catch (HttpRequestException hex)
        {
            _log.LogWarning(hex, "QBO customer-by-id: transport failure");
            throw new QuickBooksCustomerLookupException(
                QuickBooksCustomerLookupOutcome.ProviderUnavailable,
                "QuickBooks transport error during customer lookup.");
        }

        using (resp)
        {
            if (resp.StatusCode == HttpStatusCode.NotFound) return null;
            if ((int)resp.StatusCode == 401 || (int)resp.StatusCode == 403)
            {
                throw new QuickBooksCustomerLookupException(
                    QuickBooksCustomerLookupOutcome.ProviderUnavailable,
                    "QuickBooks rejected the access token during customer lookup.");
            }
            if ((int)resp.StatusCode == 429 || (int)resp.StatusCode >= 500)
            {
                throw new QuickBooksCustomerLookupException(
                    QuickBooksCustomerLookupOutcome.ProviderUnavailable,
                    $"QuickBooks customer lookup is currently unavailable (status {(int)resp.StatusCode}).");
            }
            if (!resp.IsSuccessStatusCode)
            {
                throw new QuickBooksCustomerLookupException(
                    QuickBooksCustomerLookupOutcome.Failed,
                    $"QuickBooks customer lookup failed (status {(int)resp.StatusCode}).");
            }

            try
            {
                await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
                if (!doc.RootElement.TryGetProperty("Customer", out var c)) return null;
                return ParseCustomer(c);
            }
            catch (JsonException jex)
            {
                _log.LogWarning(jex, "QBO customer-by-id: malformed response");
                throw new QuickBooksCustomerLookupException(
                    QuickBooksCustomerLookupOutcome.Failed,
                    "QuickBooks returned a malformed customer-lookup response.");
            }
        }
    }

    private static QuickBooksCustomerSearchResult QuickBooksCustomerLookupTokenOutcome(
        QuickBooksTokenException tex)
        => string.Equals(tex.Reason, "NotConfigured", StringComparison.Ordinal)
            ? QuickBooksCustomerSearchResult.ConfigurationRequired()
            : QuickBooksCustomerSearchResult.ProviderUnavailable(
                "QuickBooks rejected the access token during customer search.");

    private static QuickBooksCustomerLookupOutcome TokenOutcomeFor(QuickBooksTokenException tex)
        => string.Equals(tex.Reason, "NotConfigured", StringComparison.Ordinal)
            ? QuickBooksCustomerLookupOutcome.ConfigurationRequired
            : QuickBooksCustomerLookupOutcome.ProviderUnavailable;

    private static IReadOnlyList<QuickBooksCustomerSearchHit> ParseQueryResponse(JsonDocument doc)
    {
        if (!doc.RootElement.TryGetProperty("QueryResponse", out var qr)
            || !qr.TryGetProperty("Customer", out var arr)
            || arr.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<QuickBooksCustomerSearchHit>();
        }
        var list = new List<QuickBooksCustomerSearchHit>(arr.GetArrayLength());
        foreach (var c in arr.EnumerateArray())
        {
            list.Add(ParseCustomer(c));
        }
        // Deterministic ordering by DisplayName then id so the
        // operator sees stable result lists across reloads.
        list.Sort((a, b) =>
        {
            var n = string.CompareOrdinal(a.DisplayName, b.DisplayName);
            return n != 0 ? n : string.CompareOrdinal(a.QuickBooksCustomerId, b.QuickBooksCustomerId);
        });
        if (list.Count > MaxResults)
        {
            list.RemoveRange(MaxResults, list.Count - MaxResults);
        }
        return list;
    }

    private static QuickBooksCustomerSearchHit ParseCustomer(JsonElement c)
    {
        var id = c.TryGetProperty("Id", out var idEl) ? (idEl.GetString() ?? string.Empty) : string.Empty;
        var name = c.TryGetProperty("DisplayName", out var nEl) ? (nEl.GetString() ?? string.Empty) : string.Empty;
        var active = c.TryGetProperty("Active", out var aEl)
            && aEl.ValueKind == JsonValueKind.True;
        string? email = null;
        if (c.TryGetProperty("PrimaryEmailAddr", out var emEl)
            && emEl.ValueKind == JsonValueKind.Object
            && emEl.TryGetProperty("Address", out var addrEl)
            && addrEl.ValueKind == JsonValueKind.String)
        {
            email = addrEl.GetString();
        }
        return new QuickBooksCustomerSearchHit(
            QuickBooksCustomerId: id,
            DisplayName: name,
            Active: active,
            PrimaryEmail: email);
    }

    private static string SanitizeSearchTerm(string s)
    {
        // Strip control characters and cap to 80 chars; escape
        // single quotes per the QBO query-language convention.
        var span = s.AsSpan();
        var sb = new System.Text.StringBuilder(span.Length);
        foreach (var ch in span)
        {
            if (char.IsControl(ch)) continue;
            if (ch == '\'') { sb.Append("\\'"); continue; }
            if (ch == '%' || ch == '\\') continue;
            sb.Append(ch);
            if (sb.Length >= 80) break;
        }
        return sb.ToString();
    }

    private static async Task<string> SafeReadBodyAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        try
        {
            var s = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return s ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string Cap(string s)
        => s.Length <= FailureReasonCapChars ? s : s.Substring(0, FailureReasonCapChars);
}
