using System.Net.Http.Headers;
using System.Text.Json;
using Commerce.Application.Common.Exceptions;
using Commerce.Application.Common.Time;
using Commerce.Application.Payments.Abstractions;
using Commerce.Domain.Payments.Enums;
using Commerce.Infrastructure.Payments.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Commerce.Infrastructure.Payments.Stripe;

/// <summary>
/// Stripe adapter. Uses a direct HTTP client against Stripe's REST API
/// rather than Stripe.NET so we can keep deterministic builds with no
/// extra package dependency. SDK types do NOT cross into Application
/// or Domain — the adapter only returns the POCOs defined in
/// <c>IPaymentProvider</c>. Disabled by default.
/// </summary>
public sealed class StripePaymentProvider : IPaymentProvider
{
    private readonly HttpClient _http;
    private readonly IOptionsMonitor<PaymentProvidersOptions> _options;
    private readonly IClock _clock;
    private readonly ILogger<StripePaymentProvider> _logger;

    public StripePaymentProvider(
        HttpClient http,
        IOptionsMonitor<PaymentProvidersOptions> options,
        IClock clock,
        ILogger<StripePaymentProvider> logger)
    {
        _http = http;
        _options = options;
        _clock = clock;
        _logger = logger;
    }

    public PaymentProviderType ProviderType => PaymentProviderType.Stripe;

    public bool IsEnabled => _options.CurrentValue.Stripe.Enabled;

    private StripeOptions Cfg => _options.CurrentValue.Stripe;

    private void EnsureUsable(string setting, string? value)
    {
        if (!IsEnabled) throw new PaymentProviderDisabledException("Stripe");
        if (string.IsNullOrWhiteSpace(value))
            throw new PaymentProviderConfigurationException("Stripe", setting);
    }

    public async Task<ProviderCustomerResult> CreateOrGetCustomerAsync(
        ProviderCustomerRequest request, CancellationToken ct)
    {
        EnsureUsable("SecretKey", Cfg.SecretKey);

        var form = new List<KeyValuePair<string, string>>();
        if (!string.IsNullOrWhiteSpace(request.Email)) form.Add(new("email", request.Email!));
        if (!string.IsNullOrWhiteSpace(request.Name)) form.Add(new("name", request.Name!));
        form.Add(new("metadata[billing_account_id]", request.BillingAccountId.ToString()));
        if (request.Metadata is not null)
            foreach (var kv in request.Metadata)
                form.Add(new($"metadata[{kv.Key}]", kv.Value));

        using var resp = await PostAsync("/v1/customers", form, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        EnsureSuccess(resp, body);

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var id = root.GetProperty("id").GetString()
                 ?? throw new PaymentProviderException("Stripe", "Customer response missing id.");
        var email = root.TryGetProperty("email", out var e) ? e.GetString() : null;
        var name = root.TryGetProperty("name", out var n) ? n.GetString() : null;
        return new ProviderCustomerResult(id, email, name);
    }

    public async Task<ProviderCheckoutResult> CreateCheckoutSessionAsync(
        ProviderCheckoutRequest request, CancellationToken ct)
    {
        EnsureUsable("SecretKey", Cfg.SecretKey);

        if (request.LineItems is null || request.LineItems.Count == 0)
            throw new PaymentProviderException("Stripe",
                "Checkout session requires at least one line item.");

        var form = new List<KeyValuePair<string, string>>
        {
            new("mode", "subscription"),
            new("customer", request.ProviderCustomerId),
            new("success_url", request.SuccessUrl),
            new("cancel_url", request.CancelUrl),
            new("metadata[billing_account_id]", request.BillingAccountId.ToString()),
            new("metadata[subscription_id]", request.SubscriptionId.ToString())
        };
        for (var i = 0; i < request.LineItems.Count; i++)
        {
            var li = request.LineItems[i];
            form.Add(new($"line_items[{i}][price]", li.ProviderPriceId));
            form.Add(new($"line_items[{i}][quantity]",
                li.Quantity.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }
        if (request.Metadata is not null)
            foreach (var kv in request.Metadata)
                form.Add(new($"metadata[{kv.Key}]", kv.Value));

        using var resp = await PostAsync("/v1/checkout/sessions", form, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        EnsureSuccess(resp, body);

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var id = root.GetProperty("id").GetString()
                 ?? throw new PaymentProviderException("Stripe", "Checkout response missing id.");
        var url = root.TryGetProperty("url", out var u) ? u.GetString() : null;
        if (string.IsNullOrWhiteSpace(url))
            throw new PaymentProviderException("Stripe", "Checkout response missing url.");
        var subId = root.TryGetProperty("subscription", out var s) ? s.GetString() : null;
        DateTime? expires = null;
        if (root.TryGetProperty("expires_at", out var e) && e.ValueKind == JsonValueKind.Number
            && e.TryGetInt64(out var epoch))
        {
            expires = DateTimeOffset.FromUnixTimeSeconds(epoch).UtcDateTime;
        }
        return new ProviderCheckoutResult(id, url, subId, expires);
    }

    public void VerifyWebhook(ProviderWebhookPayload payload)
    {
        if (!IsEnabled) throw new PaymentProviderDisabledException("Stripe");
        StripeSignatureVerifier.Verify(
            payload.RawBody,
            payload.SignatureHeader,
            Cfg.WebhookSecret,
            Cfg.SignatureToleranceSeconds,
            _clock.UtcNow);
    }

    public NormalizedProviderEvent TranslateWebhookEvent(string rawBody)
        => StripeEventTranslator.Translate(rawBody);

    // -------------------------------------------------------------- HTTP

    private async Task<HttpResponseMessage> PostAsync(
        string path, IEnumerable<KeyValuePair<string, string>> form, CancellationToken ct)
    {
        var baseUrl = string.IsNullOrWhiteSpace(Cfg.ApiBaseUrl) ? "https://api.stripe.com" : Cfg.ApiBaseUrl;
        var url = $"{baseUrl.TrimEnd('/')}{path}";
        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new FormUrlEncodedContent(form)
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Cfg.SecretKey);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return await _http.SendAsync(req, ct);
    }

    private void EnsureSuccess(HttpResponseMessage resp, string body)
    {
        if (resp.IsSuccessStatusCode) return;
        // Sanitize: never log or surface API keys / secrets.
        var snippet = body is { Length: > 500 } ? body[..500] : body;
        _logger.LogWarning("Stripe API call failed with status {Status}", (int)resp.StatusCode);
        throw new PaymentProviderException("Stripe",
            $"HTTP {(int)resp.StatusCode}: {snippet}");
    }
}
