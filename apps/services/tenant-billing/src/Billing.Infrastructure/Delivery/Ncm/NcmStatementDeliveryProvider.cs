using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Billing.Domain.Statements.Delivery;

namespace Billing.Infrastructure.Delivery.Ncm;

/// <summary>
/// MS-BILL-INT-002 — Real NCM-backed implementation of
/// <see cref="IStatementDeliveryProvider"/>. Posts to the existing
/// platform notification endpoint (<c>/api/ntc/SendEmail</c>) using
/// a pre-issued bearer credential and an NCM template code; NCM
/// owns the template body, the subject line, the SendGrid wiring,
/// and the per-tenant from-address policy. Billing supplies only
/// the recipient + a small substitution dictionary so this provider
/// stays a thin transport adapter, not a parallel mail subsystem.
///
/// <para>
/// Configuration is bound through
/// <see cref="IOptionsMonitor{NcmDeliveryOptions}"/> and
/// re-evaluated per request, so a credential rotation (or any
/// other Billing:Delivery:Ncm change) is honored without a process
/// restart. Half-configured state collapses to the deterministic
/// <see cref="StatementDeliveryStatus.ProviderUnavailable"/>
/// branch instead of silently sending to the wrong place.
/// </para>
///
/// <para>
/// Determinism contract:
/// </para>
/// <list type="bullet">
///   <item>2xx → <see cref="StatementDeliveryStatus.Sent"/>; provider
///   delivery id is parsed from the response body when present and
///   falls back to the correlation id otherwise.</item>
///   <item>400 → <see cref="StatementDeliveryStatus.InvalidRecipient"/>
///   when the response body suggests an address-shape problem,
///   otherwise <see cref="StatementDeliveryStatus.Failed"/>.</item>
///   <item>401 / 403 →
///   <see cref="StatementDeliveryStatus.ProviderUnavailable"/>
///   (operator must rotate the credential; same UI surface as
///   "no provider configured" so the operator gets one banner).</item>
///   <item>408 / network timeout / cancellation →
///   <see cref="StatementDeliveryStatus.RetryableFailure"/>.</item>
///   <item>429 →
///   <see cref="StatementDeliveryStatus.RetryableFailure"/>.</item>
///   <item>5xx →
///   <see cref="StatementDeliveryStatus.RetryableFailure"/>.</item>
///   <item>Any other unhandled exception →
///   <see cref="StatementDeliveryStatus.Failed"/> with the exception
///   type name as the reason (NEVER the message — exceptions can
///   carry payload bytes).</item>
/// </list>
///
/// <para>
/// Logging discipline (mirrors the NoOp provider):
/// </para>
/// <list type="bullet">
///   <item>NEVER logs the rendered HTML body.</item>
///   <item>NEVER logs the recipient email verbatim.</item>
///   <item>NEVER logs the API key.</item>
///   <item>Logs the correlation id, the status code, and the
///   mapped <see cref="StatementDeliveryStatus"/> only.</item>
/// </list>
/// </summary>
public sealed class NcmStatementDeliveryProvider : IStatementDeliveryProvider
{
    /// <summary>
    /// Stable provider name persisted on the delivery row. Pinned
    /// to <c>"ncm-http"</c> so historical audit can distinguish
    /// rows produced by this provider from rows produced by a
    /// future SMTP/SendGrid provider.
    /// </summary>
    public const string Name = "ncm-http";

    private const string SendEmailPath = "api/ntc/SendEmail";

    private readonly HttpClient _http;
    private readonly IOptionsMonitor<NcmDeliveryOptions> _optionsMonitor;
    private readonly ILogger<NcmStatementDeliveryProvider> _log;

    public NcmStatementDeliveryProvider(
        HttpClient http,
        IOptionsMonitor<NcmDeliveryOptions> optionsMonitor,
        ILogger<NcmStatementDeliveryProvider> log)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _optionsMonitor = optionsMonitor ?? throw new ArgumentNullException(nameof(optionsMonitor));
        _log = log ?? throw new ArgumentNullException(nameof(log));

        // Per-request transport timeout is pinned at construction
        // (HttpClient.Timeout is immutable after the first send).
        // BaseUrl/ApiKey/TemplateCode are intentionally NOT cached
        // here — they are read from IOptionsMonitor.CurrentValue on
        // every SendAsync so a config rotation is honored without
        // a process restart. The absolute request URI is built per
        // request, so HttpClient.BaseAddress is left unset.
        var snapshot = _optionsMonitor.CurrentValue;
        if (snapshot.TimeoutSeconds > 0)
        {
            _http.Timeout = TimeSpan.FromSeconds(snapshot.TimeoutSeconds);
        }
    }

    public string ProviderName => Name;

    /// <summary>
    /// True only when every required field is present in the
    /// CURRENT bound options. Re-evaluated per call via
    /// <see cref="IOptionsMonitor{T}.CurrentValue"/>, so a
    /// configuration swap mid-process degrades to the
    /// deterministic <c>ProviderUnavailable</c> branch instead of
    /// crashing or silently sending under a stale snapshot.
    /// </summary>
    public bool IsConfigured => _optionsMonitor.CurrentValue.HasRequired();

    public async Task<StatementDeliveryResult> SendAsync(
        StatementDeliveryRequest request,
        CancellationToken ct = default)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));

        // Snapshot the current options ONCE per request so a mid-
        // request rotation cannot produce a half-using-old, half-
        // using-new request. The next request gets the new values.
        var options = _optionsMonitor.CurrentValue;

        if (!options.HasRequired())
        {
            // Same deterministic outcome the NoOp default produces
            // so a half-configured Ncm deployment never silently
            // sends to the wrong place.
            return StatementDeliveryResult.ProviderNotConfigured(
                provider: Name,
                correlationId: request.CorrelationId);
        }

        // Build the absolute request URI from CURRENT options so a
        // BaseUrl rotation is honored per request (HttpClient.
        // BaseAddress would have been a startup-time pin).
        Uri requestUri;
        try
        {
            var baseUrl = options.BaseUrl.EndsWith('/')
                ? options.BaseUrl
                : options.BaseUrl + "/";
            requestUri = new Uri(new Uri(baseUrl, UriKind.Absolute), SendEmailPath);
        }
        catch (UriFormatException)
        {
            // Malformed BaseUrl is operator-actionable config; surface
            // the same banner as "no provider configured" rather than
            // throwing through the controller.
            _log.LogWarning(
                "NCM delivery skipped: malformed BaseUrl. tenantId={TenantId} statementId={StatementId} correlationId={CorrelationId}",
                request.TenantId, request.StatementId, request.CorrelationId);
            return StatementDeliveryResult.ProviderNotConfigured(
                provider: Name,
                correlationId: request.CorrelationId);
        }

        // Build the NCM request body. NCM's SendEmail handler reads
        // a Dictionary<string,string>; "TemplateCode" and "Email"
        // are the only required keys, every other key is forwarded
        // to the template engine as a substitution token. Billing
        // supplies StatementNumber + RecipientName for the body
        // copy, and CorrelationId for cross-service tracing.
        var body = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["TemplateCode"] = options.TemplateCode,
            ["Email"] = request.RecipientEmail,
            ["StatementNumber"] = request.StatementNumber,
            ["RecipientName"] = request.RecipientName,
            ["CorrelationId"] = request.CorrelationId,
        };
        if (!string.IsNullOrWhiteSpace(options.FromEmail))
        {
            body["FromEmail"] = options.FromEmail!;
        }
        if (!string.IsNullOrWhiteSpace(options.FromName))
        {
            body["FromName"] = options.FromName!;
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = JsonContent.Create(body),
        };
        // Authorization built from CURRENT options so a credential
        // rotation is honored without a process restart.
        httpRequest.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", options.ApiKey);
        // Propagate the correlation id through the platform
        // notification pipeline so NCM logs and Billing's persisted
        // delivery row can be tied together end-to-end.
        httpRequest.Headers.TryAddWithoutValidation("X-Correlation-Id", request.CorrelationId);

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(httpRequest, ct).ConfigureAwait(false);
        }
        catch (TaskCanceledException tex) when (!ct.IsCancellationRequested)
        {
            // HttpClient surfaces a transport timeout as
            // TaskCanceledException with no inner cancellation
            // request. Treat as retryable; the operator can re-click
            // Send. Real cancellation (caller bailed) bubbles up.
            _log.LogWarning(tex,
                "NCM delivery timed out. tenantId={TenantId} statementId={StatementId} correlationId={CorrelationId}",
                request.TenantId, request.StatementId, request.CorrelationId);
            return StatementDeliveryResult.RetryableFailure(
                provider: Name,
                correlationId: request.CorrelationId,
                reason: "Timeout");
        }
        catch (HttpRequestException hex)
        {
            _log.LogWarning(hex,
                "NCM delivery transport error. tenantId={TenantId} statementId={StatementId} correlationId={CorrelationId}",
                request.TenantId, request.StatementId, request.CorrelationId);
            return StatementDeliveryResult.RetryableFailure(
                provider: Name,
                correlationId: request.CorrelationId,
                reason: "TransportError");
        }
        catch (Exception ex)
        {
            // Belt-and-braces: an unmapped exception MUST collapse
            // to a deterministic Failed result so the orchestrator
            // can persist a row instead of throwing through the
            // controller. Reason is the type name only — exception
            // messages may carry response payload bytes.
            _log.LogError(ex,
                "NCM delivery unexpected error. tenantId={TenantId} statementId={StatementId} correlationId={CorrelationId}",
                request.TenantId, request.StatementId, request.CorrelationId);
            return StatementDeliveryResult.Failed(
                provider: Name,
                correlationId: request.CorrelationId,
                reason: ex.GetType().Name);
        }

        using (response)
        {
            var status = response.StatusCode;
            // Read the body once; we may need it for delivery-id
            // extraction (success) or for cheap recipient-error
            // sniffing (400). Cap at 4 KB so a misbehaving provider
            // cannot fill the log pipeline.
            string? bodyText = null;
            try
            {
                bodyText = await response.Content
                    .ReadAsStringAsync(ct)
                    .ConfigureAwait(false);
                if (bodyText is { Length: > 4096 })
                {
                    bodyText = bodyText[..4096];
                }
            }
            catch
            {
                // Body read errors are not fatal to status mapping.
                bodyText = null;
            }

            _log.LogInformation(
                "NCM delivery responded. tenantId={TenantId} statementId={StatementId} correlationId={CorrelationId} statusCode={StatusCode}",
                request.TenantId, request.StatementId, request.CorrelationId, (int)status);

            if ((int)status is >= 200 and < 300)
            {
                return StatementDeliveryResult.Sent(
                    provider: Name,
                    correlationId: request.CorrelationId,
                    deliveryId: TryExtractDeliveryId(bodyText) ?? request.CorrelationId);
            }

            return status switch
            {
                HttpStatusCode.BadRequest =>
                    LooksLikeRecipientError(bodyText)
                        ? StatementDeliveryResult.InvalidRecipient(
                            provider: Name,
                            correlationId: request.CorrelationId,
                            reason: "RecipientRejected")
                        : StatementDeliveryResult.Failed(
                            provider: Name,
                            correlationId: request.CorrelationId,
                            reason: "BadRequest"),

                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                    StatementDeliveryResult.ProviderNotConfigured(
                        provider: Name,
                        correlationId: request.CorrelationId),

                HttpStatusCode.RequestTimeout =>
                    StatementDeliveryResult.RetryableFailure(
                        provider: Name,
                        correlationId: request.CorrelationId,
                        reason: "ProviderTimeout"),

                HttpStatusCode.TooManyRequests =>
                    StatementDeliveryResult.RetryableFailure(
                        provider: Name,
                        correlationId: request.CorrelationId,
                        reason: "RateLimited"),

                _ when (int)status >= 500 =>
                    StatementDeliveryResult.RetryableFailure(
                        provider: Name,
                        correlationId: request.CorrelationId,
                        reason: "ProviderTransient"),

                _ => StatementDeliveryResult.Failed(
                    provider: Name,
                    correlationId: request.CorrelationId,
                    reason: $"Status{(int)status}"),
            };
        }
    }

    /// <summary>
    /// Best-effort delivery-id extraction from NCM's response. NCM's
    /// SendEmail handler returns a <c>GenericInsertUpdateResponse</c>
    /// with an optional <c>id</c> / <c>messageId</c>. If we can't
    /// parse it, the orchestrator falls back to the correlation id
    /// so the persisted delivery row is never empty.
    /// </summary>
    private static string? TryExtractDeliveryId(string? bodyText)
    {
        if (string.IsNullOrWhiteSpace(bodyText)) return null;
        try
        {
            using var doc = JsonDocument.Parse(bodyText);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;
            foreach (var name in new[] { "deliveryId", "DeliveryId", "messageId", "MessageId", "id", "Id" })
            {
                if (doc.RootElement.TryGetProperty(name, out var prop)
                    && prop.ValueKind == JsonValueKind.String)
                {
                    var value = prop.GetString();
                    if (!string.IsNullOrWhiteSpace(value)) return value;
                }
            }
        }
        catch (JsonException)
        {
            // Non-JSON bodies are normal for some providers; ignore.
        }
        return null;
    }

    /// <summary>
    /// Cheap heuristic for distinguishing "the address you gave me
    /// is wrong" from "your request was malformed". NCM returns
    /// plain-text or a small JSON body on 400; the keywords below
    /// match the standard SendGrid / NCM rejection vocabulary.
    /// Case-insensitive, ASCII-only — never reflects body bytes
    /// back to the caller.
    /// </summary>
    private static bool LooksLikeRecipientError(string? bodyText)
    {
        if (string.IsNullOrWhiteSpace(bodyText)) return false;
        ReadOnlySpan<string> markers = new[]
        {
            "email", "recipient", "address", "to ", "invalid", "smtp"
        };
        var lower = bodyText.ToLowerInvariant();
        foreach (var marker in markers)
        {
            if (lower.Contains(marker, StringComparison.Ordinal)) return true;
        }
        return false;
    }
}
