using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BuildingBlocks.Notifications;
using BuildingBlocks.Exceptions;
using Liens.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Liens.Infrastructure.Notifications;

public sealed class NotificationPublisher : INotificationPublisher
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<NotificationPublisher> _logger;

    public NotificationPublisher(
        IHttpClientFactory httpClientFactory,
        ILogger<NotificationPublisher> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task PublishAsync(
        string notificationType,
        Guid tenantId,
        Dictionary<string, string> data,
        CancellationToken ct = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("NotificationsService");

            var request = new NotificationsProducerRequest
            {
                Channel      = "event",
                ProductKey   = "liens",
                EventKey     = notificationType,
                SourceSystem = "liens-service",
                TemplateKey  = notificationType,
                TemplateData = data,
                Recipient    = new NotificationsRecipient { TenantId = tenantId.ToString() },
                Message      = new { type = notificationType },
                Metadata     = new Dictionary<string, string>
                {
                    ["notificationType"] = notificationType,
                    ["tenantId"]         = tenantId.ToString(),
                },
            };

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/notifications");
            httpRequest.Headers.Add("X-Tenant-Id", tenantId.ToString());
            httpRequest.Content = JsonContent.Create(request, options: JsonOpts);

            var response = await client.SendAsync(httpRequest, CancellationToken.None);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogDebug(
                    "Notification published: Type={NotificationType} Tenant={TenantId}",
                    notificationType, tenantId);
            }
            else
            {
                var body = string.Empty;
                try { body = await response.Content.ReadAsStringAsync(CancellationToken.None); } catch { }

                _logger.LogWarning(
                    "Notification publish returned {StatusCode}: Type={NotificationType} Tenant={TenantId} Body={Body}",
                    (int)response.StatusCode, notificationType, tenantId, body);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex,
                "Notification publish failed: Type={NotificationType} Tenant={TenantId}",
                notificationType, tenantId);
        }
    }

    public async Task<NotificationEmailSendResult> SendEmailAsync(
        string notificationType,
        Guid tenantId,
        string recipientEmail,
        string subject,
        string body,
        Dictionary<string, string> metadata,
        CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("NotificationsService");

        var request = new NotificationsProducerRequest
        {
            Channel      = "email",
            ProductKey   = "liens",
            EventKey     = notificationType,
            SourceSystem = "liens-service",
            Subject      = subject,
            Recipient    = new NotificationsRecipient
            {
                Mode     = "Email",
                TenantId = tenantId.ToString(),
                Email    = recipientEmail,
            },
            Message = new
            {
                type = notificationType,
                subject,
                body,
            },
            Metadata = metadata,
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/notifications");
        httpRequest.Headers.Add("X-Tenant-Id", tenantId.ToString());
        httpRequest.Content = JsonContent.Create(request, options: JsonOpts);

        using var response = await client.SendAsync(httpRequest, ct);
        var responseBody = await response.Content.ReadAsStringAsync(ct);

        NotificationEmailSendResult? result = null;
        if (!string.IsNullOrWhiteSpace(responseBody))
        {
            try
            {
                var dto = JsonSerializer.Deserialize<NotificationResultDto>(responseBody, JsonOpts);
                if (dto is not null)
                {
                    result = new NotificationEmailSendResult(
                        dto.Id == Guid.Empty ? null : dto.Id,
                        dto.Status,
                        dto.BlockedByPolicy,
                        dto.BlockedReasonCode,
                        dto.FailureCategory,
                        dto.LastErrorMessage);
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex,
                    "Notification email response could not be parsed: Type={NotificationType} Tenant={TenantId} Body={Body}",
                    notificationType, tenantId, responseBody);
            }
        }

        if (!response.IsSuccessStatusCode && result is null)
        {
            throw new ServiceUnavailableException(
                $"Notifications service returned {(int)response.StatusCode} while sending buyer lien email.");
        }

        result ??= new NotificationEmailSendResult(
            null,
            response.IsSuccessStatusCode ? "accepted" : "failed",
            false,
            null,
            null,
            response.IsSuccessStatusCode ? null : responseBody);

        _logger.LogInformation(
            "Notification email submitted: Type={NotificationType} Tenant={TenantId} Status={Status} NotificationId={NotificationId}",
            notificationType, tenantId, result.Status, result.NotificationId);

        return result;
    }

    private sealed class NotificationResultDto
    {
        public Guid Id { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool BlockedByPolicy { get; set; }
        public string? BlockedReasonCode { get; set; }
        public string? FailureCategory { get; set; }
        public string? LastErrorMessage { get; set; }
    }
}
