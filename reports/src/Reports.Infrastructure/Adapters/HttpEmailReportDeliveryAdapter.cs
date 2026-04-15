using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Reports.Contracts.Configuration;
using Reports.Contracts.Delivery;

namespace Reports.Infrastructure.Adapters;

public sealed class HttpEmailReportDeliveryAdapter : IReportDeliveryAdapter
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly EmailDeliverySettings _settings;
    private readonly ILogger<HttpEmailReportDeliveryAdapter> _log;

    public string MethodName => "Email";

    public HttpEmailReportDeliveryAdapter(
        IHttpClientFactory httpClientFactory,
        IOptions<EmailDeliverySettings> settings,
        ILogger<HttpEmailReportDeliveryAdapter> log)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
        _log = log;
    }

    public async Task<DeliveryResult> DeliverAsync(
        byte[] fileContent, string fileName, string contentType,
        string? deliveryConfigJson, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        string recipients = "unknown";
        string? subject = null;
        string? message = null;
        string? tenantId = null;

        if (!string.IsNullOrWhiteSpace(deliveryConfigJson))
        {
            try
            {
                using var doc = JsonDocument.Parse(deliveryConfigJson);
                if (doc.RootElement.TryGetProperty("recipients", out var r))
                    recipients = r.GetString() ?? "unknown";
                if (doc.RootElement.TryGetProperty("subject", out var s))
                    subject = s.GetString();
                if (doc.RootElement.TryGetProperty("message", out var m))
                    message = m.GetString();
                if (doc.RootElement.TryGetProperty("tenantId", out var t))
                    tenantId = t.GetString();
            }
            catch (JsonException ex)
            {
                _log.LogWarning(ex, "Email delivery: failed to parse deliveryConfigJson");
            }
        }

        var notificationId = Guid.NewGuid().ToString("N");
        int attempt = 0;
        int maxAttempts = Math.Max(1, _settings.MaxRetries + 1);
        Exception? lastException = null;

        while (attempt < maxAttempts)
        {
            attempt++;
            try
            {
                var client = _httpClientFactory.CreateClient("EmailDelivery");

                var payload = new
                {
                    channel = "email",
                    templateKey = "report.delivery",
                    templateData = new Dictionary<string, string>
                    {
                        ["recipients"] = recipients,
                        ["subject"] = subject ?? $"Report: {fileName}",
                        ["message"] = message ?? $"Your report '{fileName}' is attached.",
                        ["fileName"] = fileName,
                        ["fileSize"] = fileContent.Length.ToString(),
                    },
                    productType = "reports",
                    recipient = new { email = recipients },
                    message = new { type = "report.delivery" },
                    metadata = new Dictionary<string, string>
                    {
                        ["source"] = "reports-service",
                        ["notificationType"] = "report.delivery",
                        ["notificationId"] = notificationId,
                        ["fileName"] = fileName,
                    },
                    attachment = new
                    {
                        fileName,
                        contentType,
                        contentBase64 = Convert.ToBase64String(fileContent),
                    },
                };

                using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/notifications");
                if (!string.IsNullOrEmpty(tenantId))
                    request.Headers.Add("X-Tenant-Id", tenantId);
                if (!string.IsNullOrEmpty(_settings.ServiceToken))
                    request.Headers.Add("Authorization", $"Bearer {_settings.ServiceToken}");
                request.Content = JsonContent.Create(payload, options: JsonOpts);

                var response = await client.SendAsync(request, ct);

                sw.Stop();

                if (response.IsSuccessStatusCode)
                {
                    _log.LogInformation(
                        "Email delivery success: file={FileName} recipients={Recipients} notificationId={NotificationId} durationMs={DurationMs}",
                        fileName, recipients, notificationId, sw.ElapsedMilliseconds);

                    return new DeliveryResult
                    {
                        Success = true,
                        Method = MethodName,
                        Message = $"Email delivered to {recipients} with attachment {fileName}.",
                        DeliveredAtUtc = DateTimeOffset.UtcNow,
                        ExternalReferenceId = notificationId,
                        DurationMs = sw.ElapsedMilliseconds,
                        DetailJson = JsonSerializer.Serialize(new { recipients, fileName, fileSize = fileContent.Length, notificationId }),
                    };
                }

                var body = string.Empty;
                try { body = await response.Content.ReadAsStringAsync(CancellationToken.None); } catch { }

                _log.LogWarning(
                    "Email delivery returned {StatusCode}: file={FileName} recipients={Recipients} attempt={Attempt} body={Body}",
                    (int)response.StatusCode, fileName, recipients, attempt, body);

                lastException = new HttpRequestException($"Notifications service returned {(int)response.StatusCode}: {body}");

                if ((int)response.StatusCode >= 500 && attempt < maxAttempts)
                {
                    await Task.Delay(1000 * attempt, ct);
                    continue;
                }

                break;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                lastException = ex;
                _log.LogWarning(ex, "Email delivery attempt {Attempt} failed: file={FileName}", attempt, fileName);

                if (attempt < maxAttempts)
                {
                    await Task.Delay(1000 * attempt, ct);
                    continue;
                }

                break;
            }
        }

        sw.Stop();
        return new DeliveryResult
        {
            Success = false,
            Method = MethodName,
            Message = $"Email delivery failed after {attempt} attempt(s): {lastException?.Message}",
            DeliveredAtUtc = DateTimeOffset.UtcNow,
            ExternalReferenceId = notificationId,
            DurationMs = sw.ElapsedMilliseconds,
            IsRetryable = true,
            DetailJson = JsonSerializer.Serialize(new { recipients, fileName, error = lastException?.Message, attempts = attempt }),
        };
    }
}
