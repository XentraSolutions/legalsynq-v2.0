using System.Text.Json;
using Microsoft.Extensions.Logging;
using Notifications.Application.DTOs;
using Notifications.Application.Interfaces;
using Notifications.Domain;
using LegalSynq.AuditClient;
using LegalSynq.AuditClient.DTOs;

namespace Notifications.Infrastructure.Services;

public class NotificationServiceImpl : INotificationService
{
    private readonly INotificationRepository _notificationRepo;
    private readonly INotificationAttemptRepository _attemptRepo;
    private readonly IProviderRoutingService _routingService;
    private readonly IContactEnforcementService _contactEnforcement;
    private readonly IUsageEvaluationService _usageEvaluation;
    private readonly IUsageMeteringService _metering;
    private readonly ITemplateResolutionService _templateResolution;
    private readonly ITemplateRenderingService _templateRendering;
    private readonly IBrandingResolutionService _brandingResolution;
    private readonly IEmailProviderAdapter _sendGridAdapter;
    private readonly ISmsProviderAdapter _twilioAdapter;
    private readonly IAuditEventClient _auditClient;
    private readonly ILogger<NotificationServiceImpl> _logger;

    public NotificationServiceImpl(
        INotificationRepository notificationRepo,
        INotificationAttemptRepository attemptRepo,
        IProviderRoutingService routingService,
        IContactEnforcementService contactEnforcement,
        IUsageEvaluationService usageEvaluation,
        IUsageMeteringService metering,
        ITemplateResolutionService templateResolution,
        ITemplateRenderingService templateRendering,
        IBrandingResolutionService brandingResolution,
        IEmailProviderAdapter sendGridAdapter,
        ISmsProviderAdapter twilioAdapter,
        IAuditEventClient auditClient,
        ILogger<NotificationServiceImpl> logger)
    {
        _notificationRepo = notificationRepo;
        _attemptRepo = attemptRepo;
        _routingService = routingService;
        _contactEnforcement = contactEnforcement;
        _usageEvaluation = usageEvaluation;
        _metering = metering;
        _templateResolution = templateResolution;
        _templateRendering = templateRendering;
        _brandingResolution = brandingResolution;
        _sendGridAdapter = sendGridAdapter;
        _twilioAdapter = twilioAdapter;
        _auditClient = auditClient;
        _logger = logger;
    }

    public async Task<NotificationResultDto> SubmitAsync(Guid tenantId, SubmitNotificationDto request)
    {
        var recipientJson = JsonSerializer.Serialize(request.Recipient);
        var messageJson = JsonSerializer.Serialize(request.Message);
        var metadataJson = request.Metadata != null ? JsonSerializer.Serialize(request.Metadata) : null;

        if (!string.IsNullOrEmpty(request.IdempotencyKey))
        {
            var existing = await _notificationRepo.FindByIdempotencyKeyAsync(tenantId, request.IdempotencyKey);
            if (existing != null)
                return MapToResult(existing);
        }

        var rateCheck = await _usageEvaluation.CheckRequestAllowedAsync(tenantId, request.Channel);
        if (!rateCheck.Allowed)
        {
            _ = _metering.MeterAsync(new MeterEventInput { TenantId = tenantId, UsageUnit = "api_notification_request_rejected", Channel = request.Channel });
            return new NotificationResultDto { Status = "blocked", BlockedByPolicy = true, BlockedReasonCode = rateCheck.Code, LastErrorMessage = rateCheck.Reason };
        }

        var contactValue = ExtractContactValue(request.Channel, recipientJson);
        ContactEnforcementResult? enforcement = null;
        if (!string.IsNullOrEmpty(contactValue))
        {
            enforcement = await _contactEnforcement.EvaluateAsync(new ContactEnforcementInput
            {
                TenantId = tenantId, Channel = request.Channel, ContactValue = contactValue,
                OverrideSuppression = request.OverrideSuppression ?? false, OverrideReason = request.OverrideReason
            });
        }

        var notification = new Notification
        {
            TenantId = tenantId, Channel = request.Channel, Status = "accepted",
            RecipientJson = recipientJson, MessageJson = messageJson, MetadataJson = metadataJson,
            IdempotencyKey = request.IdempotencyKey, TemplateKey = request.TemplateKey,
            BlockedByPolicy = enforcement is { Allowed: false },
            BlockedReasonCode = enforcement is { Allowed: false } ? enforcement.ReasonCode : null,
            OverrideUsed = enforcement?.OverrideUsed ?? false
        };

        if (enforcement is { Allowed: false })
        {
            notification.Status = "blocked";
            notification = await _notificationRepo.CreateAsync(notification);
            _ = _metering.MeterAsync(new MeterEventInput { TenantId = tenantId, UsageUnit = "suppressed_notification_request_rejected", Channel = request.Channel, NotificationId = notification.Id });
            return MapToResult(notification);
        }

        string? renderedSubject = null, renderedBody = null, renderedText = null;
        Guid? templateId = null, templateVersionId = null;

        if (!string.IsNullOrEmpty(request.TemplateKey) && request.TemplateData != null)
        {
            ResolvedTemplate? resolved;
            if (!string.IsNullOrEmpty(request.ProductType))
                resolved = await _templateResolution.ResolveByProductAsync(tenantId, request.TemplateKey, request.Channel, request.ProductType);
            else
                resolved = await _templateResolution.ResolveAsync(tenantId, request.TemplateKey, request.Channel);

            if (resolved != null)
            {
                templateId = resolved.Template.Id;
                templateVersionId = resolved.Version.Id;

                RenderResult rendered;
                if (request.BrandedRendering == true && !string.IsNullOrEmpty(request.ProductType))
                {
                    var branding = await _brandingResolution.ResolveAsync(tenantId, request.ProductType);
                    var tokens = _brandingResolution.BuildBrandingTokens(branding);
                    rendered = _templateRendering.RenderBranded(resolved.Version.SubjectTemplate, resolved.Version.BodyTemplate, resolved.Version.TextTemplate, request.TemplateData, tokens);
                }
                else
                {
                    rendered = _templateRendering.Render(resolved.Version.SubjectTemplate, resolved.Version.BodyTemplate, resolved.Version.TextTemplate, request.TemplateData);
                }

                renderedSubject = rendered.Subject;
                renderedBody = rendered.Body;
                renderedText = rendered.Text;

                _ = _metering.MeterAsync(new MeterEventInput { TenantId = tenantId, UsageUnit = "template_render", Channel = request.Channel, NotificationId = notification.Id });
            }
        }

        notification.TemplateId = templateId;
        notification.TemplateVersionId = templateVersionId;
        notification.RenderedSubject = renderedSubject;
        notification.RenderedBody = renderedBody;
        notification.RenderedText = renderedText;
        notification.Status = "processing";

        notification = await _notificationRepo.CreateAsync(notification);
        _ = _metering.MeterAsync(new MeterEventInput { TenantId = tenantId, UsageUnit = "api_notification_request", Channel = request.Channel, NotificationId = notification.Id });

        var routes = await _routingService.ResolveRoutesAsync(tenantId, request.Channel);

        var msg = JsonSerializer.Deserialize<JsonElement>(messageJson);
        var subject = renderedSubject ?? (msg.TryGetProperty("subject", out var s) ? s.GetString() : "");
        var body = renderedBody ?? (msg.TryGetProperty("body", out var b) ? b.GetString() : "");
        var html = msg.TryGetProperty("html", out var h) ? h.GetString() : null;

        foreach (var route in routes)
        {
            var attemptNumber = routes.IndexOf(route) + 1;
            var attempt = await _attemptRepo.CreateAsync(new NotificationAttempt
            {
                TenantId = tenantId, NotificationId = notification.Id,
                Channel = request.Channel, Provider = route.ProviderType,
                Status = "sending", AttemptNumber = attemptNumber,
                ProviderOwnershipMode = route.OwnershipMode,
                ProviderConfigId = route.TenantProviderConfigId,
                IsFailover = route.IsFailover
            });

            _ = _metering.MeterAsync(new MeterEventInput { TenantId = tenantId, UsageUnit = request.Channel == "email" ? "email_attempt" : "sms_attempt", Channel = request.Channel, NotificationId = notification.Id, NotificationAttemptId = attempt.Id, Provider = route.ProviderType, ProviderOwnershipMode = route.OwnershipMode, ProviderConfigId = route.TenantProviderConfigId });

            bool success;
            string? providerMessageId = null;
            ProviderFailure? failure = null;

            if (request.Channel == "email")
            {
                var result = await _sendGridAdapter.SendAsync(new EmailSendPayload { To = contactValue ?? "", Subject = subject ?? "", Body = body ?? "", Html = html });
                success = result.Success;
                providerMessageId = result.ProviderMessageId;
                failure = result.Failure;
            }
            else
            {
                var result = await _twilioAdapter.SendAsync(new SmsSendPayload { To = contactValue ?? "", Body = body ?? "" });
                success = result.Success;
                providerMessageId = result.ProviderMessageId;
                failure = result.Failure;
            }

            if (success)
            {
                attempt.Status = "sent";
                attempt.ProviderMessageId = providerMessageId;
                attempt.CompletedAt = DateTime.UtcNow;
                await _attemptRepo.UpdateAsync(attempt);

                notification.Status = "sent";
                notification.ProviderUsed = route.ProviderType;
                notification.ProviderOwnershipMode = route.OwnershipMode;
                notification.ProviderConfigId = route.TenantProviderConfigId;
                notification.PlatformFallbackUsed = route.IsPlatformFallback;
                await _notificationRepo.UpdateAsync(notification);

                _ = _metering.MeterAsync(new MeterEventInput { TenantId = tenantId, UsageUnit = request.Channel == "email" ? "email_sent" : "sms_sent", Channel = request.Channel, NotificationId = notification.Id, NotificationAttemptId = attempt.Id, Provider = route.ProviderType, ProviderOwnershipMode = route.OwnershipMode });
                try { await _auditClient.IngestAsync(new IngestAuditEventRequest { EventType = "notification.sent", Action = "notification.sent", SourceSystem = "notifications", Description = "Notification sent successfully", Scope = new AuditEventScopeDto { TenantId = tenantId.ToString() } }); } catch { }
                return MapToResult(notification);
            }

            attempt.Status = "failed";
            attempt.FailureCategory = failure?.Category;
            attempt.ErrorMessage = failure?.Message;
            attempt.CompletedAt = DateTime.UtcNow;
            await _attemptRepo.UpdateAsync(attempt);

            if (route.IsFailover)
                _ = _metering.MeterAsync(new MeterEventInput { TenantId = tenantId, UsageUnit = "provider_failover_attempt", Channel = request.Channel, NotificationId = notification.Id, NotificationAttemptId = attempt.Id, Provider = route.ProviderType });

            if (failure?.Retryable != true) break;
        }

        notification.Status = "failed";
        notification.FailureCategory = routes.Count > 0 ? "retryable_provider_failure" : "auth_config_failure";
        notification.LastErrorMessage = "All provider routes exhausted";
        await _notificationRepo.UpdateAsync(notification);
        try { await _auditClient.IngestAsync(new IngestAuditEventRequest { EventType = "notification.failed", Action = "notification.failed", SourceSystem = "notifications", Description = "Notification failed - all routes exhausted", Scope = new AuditEventScopeDto { TenantId = tenantId.ToString() } }); } catch { }
        return MapToResult(notification);
    }

    public async Task<NotificationDto?> GetByIdAsync(Guid tenantId, Guid id)
    {
        var n = await _notificationRepo.GetByIdAndTenantAsync(id, tenantId);
        return n != null ? MapToDto(n) : null;
    }

    public async Task<List<NotificationDto>> ListAsync(Guid tenantId, int limit = 50, int offset = 0)
    {
        var list = await _notificationRepo.GetByTenantAsync(tenantId, limit, offset);
        return list.Select(MapToDto).ToList();
    }

    private static NotificationResultDto MapToResult(Notification n) => new()
    {
        Id = n.Id, Status = n.Status, ProviderUsed = n.ProviderUsed,
        PlatformFallbackUsed = n.PlatformFallbackUsed, BlockedByPolicy = n.BlockedByPolicy,
        BlockedReasonCode = n.BlockedReasonCode, OverrideUsed = n.OverrideUsed,
        FailureCategory = n.FailureCategory, LastErrorMessage = n.LastErrorMessage
    };

    private static NotificationDto MapToDto(Notification n) => new()
    {
        Id = n.Id, TenantId = n.TenantId, Channel = n.Channel, Status = n.Status,
        RecipientJson = n.RecipientJson, MessageJson = n.MessageJson, MetadataJson = n.MetadataJson,
        IdempotencyKey = n.IdempotencyKey, ProviderUsed = n.ProviderUsed,
        FailureCategory = n.FailureCategory, LastErrorMessage = n.LastErrorMessage,
        TemplateId = n.TemplateId, TemplateVersionId = n.TemplateVersionId, TemplateKey = n.TemplateKey,
        RenderedSubject = n.RenderedSubject, RenderedBody = n.RenderedBody, RenderedText = n.RenderedText,
        ProviderOwnershipMode = n.ProviderOwnershipMode, ProviderConfigId = n.ProviderConfigId,
        PlatformFallbackUsed = n.PlatformFallbackUsed, BlockedByPolicy = n.BlockedByPolicy,
        BlockedReasonCode = n.BlockedReasonCode, OverrideUsed = n.OverrideUsed,
        CreatedAt = n.CreatedAt, UpdatedAt = n.UpdatedAt
    };

    private static string? ExtractContactValue(string channel, string recipientJson)
    {
        try
        {
            var doc = JsonDocument.Parse(recipientJson);
            if (channel == "email") return doc.RootElement.TryGetProperty("email", out var e) ? e.GetString() : null;
            if (channel == "sms") return doc.RootElement.TryGetProperty("phone", out var p) ? p.GetString() : null;
            return null;
        }
        catch { return null; }
    }
}
