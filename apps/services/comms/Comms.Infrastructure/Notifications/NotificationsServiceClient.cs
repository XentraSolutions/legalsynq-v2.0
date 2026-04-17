using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Comms.Application.Interfaces;

namespace Comms.Infrastructure.Notifications;

public sealed class NotificationsServiceClient : INotificationsServiceClient
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<NotificationsServiceClient> _logger;

    public NotificationsServiceClient(
        IHttpClientFactory httpClientFactory,
        ILogger<NotificationsServiceClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<NotificationsSendResult> SendEmailAsync(
        OutboundEmailPayload payload, CancellationToken ct = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("NotificationsService");

            var templateData = new Dictionary<string, string>
            {
                ["subject"] = payload.Subject,
                ["bodyText"] = payload.BodyText ?? string.Empty,
                ["bodyHtml"] = payload.BodyHtml ?? string.Empty,
                ["fromEmail"] = payload.FromEmail,
                ["fromDisplayName"] = payload.FromDisplayName,
                ["internetMessageId"] = payload.InternetMessageId,
                ["inReplyToMessageId"] = payload.InReplyToMessageId ?? string.Empty,
                ["referencesHeader"] = payload.ReferencesHeader ?? string.Empty,
            };
            if (!string.IsNullOrWhiteSpace(payload.ReplyToEmail))
                templateData["replyToEmail"] = payload.ReplyToEmail;

            if (payload.TemplateData is not null)
            {
                foreach (var kvp in payload.TemplateData)
                    templateData[kvp.Key] = kvp.Value;
            }

            var metadata = new Dictionary<string, string>
            {
                ["source"] = "comms-service",
                ["internetMessageId"] = payload.InternetMessageId,
                ["tenantId"] = payload.TenantId.ToString(),
            };
            if (!string.IsNullOrWhiteSpace(payload.TemplateKey))
                metadata["templateKey"] = payload.TemplateKey;

            var notificationPayload = new
            {
                channel = "email",
                templateKey = payload.TemplateKey ?? "comms_outbound_email",
                templateData,
                productType = "commss",
                recipient = new
                {
                    tenantId = payload.TenantId.ToString(),
                    email = payload.ToAddresses,
                    cc = payload.CcAddresses,
                    bcc = payload.BccAddresses,
                },
                sender = new
                {
                    email = payload.FromEmail,
                    displayName = payload.FromDisplayName,
                    replyTo = payload.ReplyToEmail,
                },
                message = new
                {
                    type = "outbound_email",
                    subject = payload.Subject,
                    body = payload.BodyHtml ?? payload.BodyText ?? string.Empty,
                    textBody = payload.BodyText,
                },
                attachments = payload.Attachments?.Select(a => new
                {
                    documentId = a.DocumentId.ToString(),
                    fileName = a.FileName,
                    contentType = a.ContentType,
                    fileSizeBytes = a.FileSizeBytes,
                }).ToArray(),
                metadata,
                idempotencyKey = payload.IdempotencyKey,
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/notifications");
            request.Headers.Add("X-Tenant-Id", payload.TenantId.ToString());
            request.Content = JsonContent.Create(notificationPayload, options: JsonOpts);

            var response = await client.SendAsync(request, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<NotificationsApiResult>(responseBody, JsonOpts);

                _logger.LogInformation(
                    "Outbound email submitted to Notifications: RequestId={RequestId} Provider={Provider} Status={Status}",
                    result?.Id, result?.ProviderUsed, result?.Status);

                return new NotificationsSendResult(
                    Success: true,
                    NotificationsRequestId: result?.Id,
                    ProviderUsed: result?.ProviderUsed,
                    ProviderMessageId: null,
                    Status: result?.Status ?? "queued",
                    ErrorMessage: null);
            }
            else
            {
                _logger.LogWarning(
                    "Notifications service returned {StatusCode} for outbound email: {Body}",
                    (int)response.StatusCode, responseBody);

                return new NotificationsSendResult(
                    Success: false,
                    NotificationsRequestId: null,
                    ProviderUsed: null,
                    ProviderMessageId: null,
                    Status: "failed",
                    ErrorMessage: $"Notifications service returned {(int)response.StatusCode}: {responseBody}");
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to send outbound email via Notifications service");

            return new NotificationsSendResult(
                Success: false,
                NotificationsRequestId: null,
                ProviderUsed: null,
                ProviderMessageId: null,
                Status: "failed",
                ErrorMessage: $"Notifications service call failed: {ex.Message}");
        }
    }

    public async Task<NotificationsSendResult> SendOperationalAlertAsync(
        Application.DTOs.OperationalAlertPayload payload, CancellationToken ct = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("NotificationsService");

            var templateData = new Dictionary<string, string>
            {
                ["triggerType"] = payload.TriggerType,
                ["conversationId"] = payload.ConversationId.ToString(),
                ["priority"] = payload.Priority,
                ["dueAtUtc"] = payload.DueAtUtc.ToString("O"),
                ["conversationSubject"] = payload.ConversationSubject ?? string.Empty,
            };

            if (payload.QueueId.HasValue)
                templateData["queueId"] = payload.QueueId.Value.ToString();

            var metadata = new Dictionary<string, string>
            {
                ["source"] = "comms-service",
                ["tenantId"] = payload.TenantId.ToString(),
                ["triggerType"] = payload.TriggerType,
                ["conversationId"] = payload.ConversationId.ToString(),
            };

            var notificationPayload = new
            {
                channel = "internal",
                templateKey = payload.TriggerType,
                templateData,
                productType = "commss",
                recipient = new
                {
                    tenantId = payload.TenantId.ToString(),
                    userId = payload.TargetUserId.ToString(),
                },
                message = new
                {
                    type = "operational_alert",
                    subject = $"SLA Alert: {payload.TriggerType}",
                    body = $"SLA {payload.TriggerType} for conversation {payload.ConversationSubject ?? payload.ConversationId.ToString()}. Priority: {payload.Priority}. Due: {payload.DueAtUtc:O}",
                },
                metadata,
                idempotencyKey = payload.IdempotencyKey,
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/notifications");
            request.Headers.Add("X-Tenant-Id", payload.TenantId.ToString());
            request.Content = JsonContent.Create(notificationPayload, options: JsonOpts);

            var response = await client.SendAsync(request, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<NotificationsApiResult>(responseBody, JsonOpts);

                _logger.LogInformation(
                    "Operational alert submitted to Notifications: RequestId={RequestId} TriggerType={TriggerType}",
                    result?.Id, payload.TriggerType);

                return new NotificationsSendResult(
                    Success: true,
                    NotificationsRequestId: result?.Id,
                    ProviderUsed: result?.ProviderUsed,
                    ProviderMessageId: null,
                    Status: result?.Status ?? "queued",
                    ErrorMessage: null);
            }
            else
            {
                _logger.LogWarning(
                    "Notifications service returned {StatusCode} for operational alert: {Body}",
                    (int)response.StatusCode, responseBody);

                return new NotificationsSendResult(
                    Success: false,
                    NotificationsRequestId: null,
                    ProviderUsed: null,
                    ProviderMessageId: null,
                    Status: "failed",
                    ErrorMessage: $"Notifications service returned {(int)response.StatusCode}: {responseBody}");
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to send operational alert via Notifications service");

            return new NotificationsSendResult(
                Success: false,
                NotificationsRequestId: null,
                ProviderUsed: null,
                ProviderMessageId: null,
                Status: "failed",
                ErrorMessage: $"Notifications service call failed: {ex.Message}");
        }
    }

    private class NotificationsApiResult
    {
        public Guid? Id { get; set; }
        public string? Status { get; set; }
        public string? ProviderUsed { get; set; }
        public bool PlatformFallbackUsed { get; set; }
        public bool BlockedByPolicy { get; set; }
        public string? BlockedReasonCode { get; set; }
        public string? FailureCategory { get; set; }
        public string? LastErrorMessage { get; set; }
    }
}
