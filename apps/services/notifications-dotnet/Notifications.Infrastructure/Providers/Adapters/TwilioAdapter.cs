using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Notifications.Application.Interfaces;

namespace Notifications.Infrastructure.Providers.Adapters;

public class TwilioAdapter : ISmsProviderAdapter
{
    public string ProviderType => "twilio";

    private readonly string _accountSid;
    private readonly string _authToken;
    private readonly string _defaultFromNumber;
    private readonly HttpClient _http;
    private readonly ILogger<TwilioAdapter> _logger;

    public TwilioAdapter(string accountSid, string authToken, string defaultFromNumber, HttpClient http, ILogger<TwilioAdapter> logger)
    {
        _accountSid = accountSid;
        _authToken = authToken;
        _defaultFromNumber = defaultFromNumber;
        _http = http;
        _logger = logger;
    }

    public Task<bool> ValidateConfigAsync()
        => Task.FromResult(!string.IsNullOrEmpty(_accountSid) && !string.IsNullOrEmpty(_authToken) && !string.IsNullOrEmpty(_defaultFromNumber));

    public async Task<SmsSendResult> SendAsync(SmsSendPayload payload)
    {
        if (!await ValidateConfigAsync())
            return new SmsSendResult { Success = false, Failure = new ProviderFailure { Category = "auth_config_failure", Message = "Twilio is not configured", Retryable = false } };

        var url = $"https://api.twilio.com/2010-04-01/Accounts/{_accountSid}/Messages.json";
        var formContent = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("To", payload.To),
            new KeyValuePair<string, string>("From", payload.From ?? _defaultFromNumber),
            new KeyValuePair<string, string>("Body", payload.Body)
        });

        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = formContent };
        var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_accountSid}:{_authToken}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", auth);

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var response = await _http.SendAsync(request, cts.Token);
            var statusCode = (int)response.StatusCode;
            var responseBody = await response.Content.ReadAsStringAsync(cts.Token);

            if (statusCode == 201)
            {
                string? sid = null;
                try { var parsed = JsonSerializer.Deserialize<JsonElement>(responseBody); sid = parsed.TryGetProperty("sid", out var s) ? s.GetString() : null; } catch { }
                _logger.LogInformation("Twilio: SMS sent successfully to {To}, SID={Sid}", payload.To, sid);
                return new SmsSendResult { Success = true, ProviderMessageId = sid };
            }

            var category = ClassifyError(statusCode, responseBody);
            _logger.LogWarning("Twilio: send failed {StatusCode} {Category}", statusCode, category);
            return new SmsSendResult { Success = false, Failure = new ProviderFailure { Category = category, ProviderCode = statusCode.ToString(), Message = responseBody[..Math.Min(responseBody.Length, 500)], Retryable = category is "retryable_provider_failure" or "provider_unavailable" } };
        }
        catch (Exception ex)
        {
            var isTimeout = ex is TaskCanceledException or OperationCanceledException;
            _logger.LogError(ex, "Twilio: network error during send");
            return new SmsSendResult { Success = false, Failure = new ProviderFailure { Category = isTimeout ? "provider_unavailable" : "retryable_provider_failure", Message = ex.Message, Retryable = true } };
        }
    }

    public async Task<ProviderHealthResult> HealthCheckAsync()
    {
        if (string.IsNullOrEmpty(_accountSid) || string.IsNullOrEmpty(_authToken)) return new ProviderHealthResult { Status = "down" };
        var start = DateTime.UtcNow;
        try
        {
            var url = $"https://api.twilio.com/2010-04-01/Accounts/{_accountSid}.json";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_accountSid}:{_authToken}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", auth);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            var response = await _http.SendAsync(request, cts.Token);
            var latencyMs = (int)(DateTime.UtcNow - start).TotalMilliseconds;
            var status = (int)response.StatusCode switch { 200 => "healthy", 401 or 403 => "down", >= 500 => "degraded", _ => "healthy" };
            return new ProviderHealthResult { Status = status, LatencyMs = latencyMs };
        }
        catch { return new ProviderHealthResult { Status = "down", LatencyMs = (int)(DateTime.UtcNow - start).TotalMilliseconds }; }
    }

    private static string ClassifyError(int statusCode, string body)
    {
        if (statusCode is 401 or 403) return "auth_config_failure";
        if (statusCode == 400)
        {
            var lower = body.ToLowerInvariant();
            if (lower.Contains("21211") || lower.Contains("21614") || lower.Contains("invalid")) return "invalid_recipient";
            return "non_retryable_failure";
        }
        if (statusCode is 429 or >= 500) return "retryable_provider_failure";
        return "non_retryable_failure";
    }
}
