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
    private readonly IRecipientResolver _recipientResolver;
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
        IRecipientResolver recipientResolver,
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
        _recipientResolver = recipientResolver;
        _auditClient = auditClient;
        _logger = logger;
    }

    public async Task<NotificationResultDto> SubmitAsync(Guid tenantId, SubmitNotificationDto request)
    {
        // Parse the recipient envelope to detect role/org fan-out modes.
        var recipientJson = JsonSerializer.Serialize(request.Recipient);
        JsonElement recipientEl;
        try { recipientEl = JsonDocument.Parse(recipientJson).RootElement.Clone(); }
        catch { recipientEl = default; }

        var mode = ReadRecipientMode(recipientEl);
        var isFanOut = recipientEl.ValueKind == JsonValueKind.Array
                    || string.Equals(mode, "Role", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(mode, "Org",  StringComparison.OrdinalIgnoreCase);

        if (!isFanOut)
            return await DispatchSingleAsync(tenantId, request, recipientJson);

        // Role/Org/Batch fan-out: resolver expands every envelope (single
        // object or array) and dedupes the union by StableKey, then we
        // persist a parent "fan-out summary" notification capturing how
        // many users were resolved, delivered, blocked, or skipped — and
        // why — so operators can diagnose envelopes that unexpectedly
        // delivered to nobody. The idempotency key applies to the parent;
        // re-submitting must return the existing parent rather than
        // violating the (TenantId, IdempotencyKey) unique index.
        if (!string.IsNullOrEmpty(request.IdempotencyKey))
        {
            var existing = await _notificationRepo.FindByIdempotencyKeyAsync(tenantId, request.IdempotencyKey);
            if (existing != null)
                return MapToResult(existing);
        }

<<<<<<< HEAD
        var fanOutMode = mode ?? (recipientEl.ValueKind == JsonValueKind.Array ? "Batch" : "FanOut");
        var resolved   = await _recipientResolver.ResolveAsync(tenantId, recipientEl);
        var roleKey    = ReadRecipientField(recipientEl, "roleKey");
        var orgId      = ReadRecipientField(recipientEl, "orgId");
=======
        var resolved = await _recipientResolver.ResolveAsync(tenantId, recipientEl);
        var roleKey  = ReadRecipientField(recipientEl, "roleKey");
        var orgId    = ReadRecipientField(recipientEl, "orgId");
>>>>>>> 5264eabc (Show how role/org notifications fanned out (members reached, blocked, skipped))

        var perRecipient = new List<FanOutPerRecipient>(resolved.Count);
        var dispatched   = new List<NotificationResultDto>(resolved.Count);

        foreach (var r in resolved)
        {
            var skipReason = ClassifySkipReason(request.Channel, r);
            if (skipReason != null)
            {
                perRecipient.Add(new FanOutPerRecipient
                {
                    UserId = r.UserId, Email = r.Email, OrgId = r.OrgId,
                    Status = "skipped", Reason = skipReason,
                });
                continue;
            }

            var perRequest = ClonePerRecipient(request, r);
            var perRecipientJson = JsonSerializer.Serialize(perRequest.Recipient);
            var dispatchResult = await DispatchSingleAsync(tenantId, perRequest, perRecipientJson);
            dispatched.Add(dispatchResult);

            perRecipient.Add(new FanOutPerRecipient
            {
                UserId = r.UserId, Email = r.Email, OrgId = r.OrgId,
                Status = dispatchResult.Status,
                Reason = dispatchResult.BlockedReasonCode ?? dispatchResult.FailureCategory,
                NotificationId = dispatchResult.Id == Guid.Empty ? null : dispatchResult.Id.ToString(),
            });
        }

        var summary = BuildFanOutSummary(fanOutMode, roleKey, orgId, request.Channel, resolved.Count, perRecipient);
        var parent  = await PersistFanOutParentAsync(tenantId, request, recipientJson, summary);

        try
        {
            await _auditClient.IngestAsync(new IngestAuditEventRequest
            {
                EventType = "notification.fanout",
                Action = "notification.fanout",
                SourceSystem = "notifications",
                Description = $"Fan-out {fanOutMode}: resolved={summary.TotalResolved} sent={summary.SentCount} " +
                              $"failed={summary.FailedCount} blocked={summary.BlockedCount} skipped={summary.SkippedCount}",
                Scope = new AuditEventScopeDto { TenantId = tenantId.ToString() }
            });
        }
        catch { /* audit best-effort */ }

        if (resolved.Count == 0)
        {
            _logger.LogWarning("Notification fan-out resolved 0 recipients for tenant {TenantId} mode {Mode}", tenantId, fanOutMode);
        }

        return BuildFanOutResult(parent, summary, dispatched);
    }

    // Skip members that cannot be reached on the requested channel so the
    // fan-out summary records a clear reason instead of a doomed dispatch.
    // Note: ResolvedRecipient has no phone today, so sms always skips.
    private static string? ClassifySkipReason(string channel, ResolvedRecipient r)
    {
        var ch = channel?.Trim().ToLowerInvariant();
        return ch switch
        {
            "email"          => string.IsNullOrWhiteSpace(r.Email)  ? "no_email_on_file" : null,
            "sms"            => "no_phone_on_file",
            "push"           => string.IsNullOrWhiteSpace(r.UserId) ? "no_user_for_push" : null,
            "in-app" or "inapp" => string.IsNullOrWhiteSpace(r.UserId) ? "no_user_for_inapp" : null,
            _                => null,
        };
    }

    private async Task<Notification> PersistFanOutParentAsync(
        Guid tenantId, SubmitNotificationDto request, string recipientJson, FanOutSummary summary)
    {
        // Merge summary into MetadataJson under "fanout" so the UI can read
        // it without a schema migration; producer keys are preserved.
        Dictionary<string, object?> metaDict;
        if (request.Metadata != null)
        {
            try
            {
                var existing = JsonSerializer.Serialize(request.Metadata);
                metaDict = JsonSerializer.Deserialize<Dictionary<string, object?>>(existing) ?? new();
            }
            catch { metaDict = new(); }
        }
        else
        {
            metaDict = new();
        }
        // CamelCase so the web UI can read predictable JSON keys.
        var camelOpts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var summaryJson = JsonSerializer.Serialize(summary, camelOpts);
        metaDict["fanout"] = JsonSerializer.Deserialize<JsonElement>(summaryJson);

        var status =
            summary.TotalResolved == 0                              ? "blocked" :
            summary.SentCount     == summary.TotalResolved          ? "sent"    :
            summary.SentCount     == 0 && summary.FailedCount  == 0 ? "blocked" :
            summary.SentCount     == 0                              ? "failed"  :
                                                                      "partial";

        var parent = new Notification
        {
            TenantId = tenantId,
            Channel  = request.Channel,
            Status   = status,
            RecipientJson = recipientJson,
            MessageJson   = JsonSerializer.Serialize(request.Message),
            MetadataJson  = JsonSerializer.Serialize(metaDict),
            IdempotencyKey = request.IdempotencyKey,
            TemplateKey    = request.TemplateKey,
            BlockedByPolicy = status == "blocked",
            BlockedReasonCode = summary.TotalResolved == 0 ? "recipient_set_empty" : null,
            // Only surface a "last error" string for non-successful outcomes;
            // fan-out counts always live in MetadataJson.fanout for the UI.
            LastErrorMessage = status == "sent"
                ? null
                : $"fanout: resolved={summary.TotalResolved} sent={summary.SentCount} " +
                  $"failed={summary.FailedCount} blocked={summary.BlockedCount} skipped={summary.SkippedCount}",
            Severity = request.Severity,
            Category = request.Category,
        };
        return await _notificationRepo.CreateAsync(parent);
    }

    private static FanOutSummary BuildFanOutSummary(
        string? mode, string? roleKey, string? orgId, string channel,
        int totalResolved, List<FanOutPerRecipient> perRecipient)
    {
        var sent    = perRecipient.Count(p => p.Status == "sent");
        var failed  = perRecipient.Count(p => p.Status == "failed");
        var blocked = perRecipient.Count(p => p.Status == "blocked");
        var skipped = perRecipient.Count(p => p.Status == "skipped");

        var skippedByReason = perRecipient
            .Where(p => p.Status == "skipped" && !string.IsNullOrEmpty(p.Reason))
            .GroupBy(p => p.Reason!)
            .ToDictionary(g => g.Key, g => g.Count());

        var blockedByReason = perRecipient
            .Where(p => p.Status == "blocked" && !string.IsNullOrEmpty(p.Reason))
            .GroupBy(p => p.Reason!)
            .ToDictionary(g => g.Key, g => g.Count());

        return new FanOutSummary
        {
            Mode = mode,
            RoleKey = roleKey,
            OrgId = orgId,
            Channel = channel,
            TotalResolved = totalResolved,
            SentCount = sent,
            FailedCount = failed,
            BlockedCount = blocked,
            SkippedCount = skipped,
            DeliveredByChannel = sent > 0
                ? new Dictionary<string, int> { [channel] = sent }
                : new Dictionary<string, int>(),
            SkippedByReason = skippedByReason,
            BlockedByReason = blockedByReason,
            Recipients = perRecipient,
        };
    }

    private static NotificationResultDto BuildFanOutResult(
        Notification parent, FanOutSummary summary, List<NotificationResultDto> dispatched) => new()
    {
        Id = parent.Id,
        Status = parent.Status,
        ProviderUsed = dispatched.FirstOrDefault(r => !string.IsNullOrEmpty(r.ProviderUsed))?.ProviderUsed,
        PlatformFallbackUsed = dispatched.Any(r => r.PlatformFallbackUsed),
        BlockedByPolicy = parent.BlockedByPolicy || summary.BlockedCount > 0,
        BlockedReasonCode = parent.BlockedReasonCode
            ?? dispatched.FirstOrDefault(r => !string.IsNullOrEmpty(r.BlockedReasonCode))?.BlockedReasonCode,
        OverrideUsed = dispatched.Any(r => r.OverrideUsed),
        FailureCategory = dispatched.FirstOrDefault(r => !string.IsNullOrEmpty(r.FailureCategory))?.FailureCategory,
        LastErrorMessage = parent.LastErrorMessage,
    };

    private static string? ReadRecipientField(JsonElement obj, string name)
    {
        if (obj.ValueKind != JsonValueKind.Object) return null;
        foreach (var prop in obj.EnumerateObject())
        {
            if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
                return prop.Value.ValueKind == JsonValueKind.String ? prop.Value.GetString() : null;
        }
        return null;
    }

    private static SubmitNotificationDto ClonePerRecipient(SubmitNotificationDto src, ResolvedRecipient r)
    {
        var dict = new Dictionary<string, string?>
        {
            ["mode"] = !string.IsNullOrEmpty(r.UserId) ? "UserId" : "Email",
        };
        if (!string.IsNullOrEmpty(r.UserId)) dict["userId"] = r.UserId;
        if (!string.IsNullOrEmpty(r.Email))  dict["email"]  = r.Email;
        if (!string.IsNullOrEmpty(r.OrgId))  dict["orgId"]  = r.OrgId;

        return new SubmitNotificationDto
        {
            Channel = src.Channel,
            Recipient = dict,
            Message = src.Message,
            Metadata = src.Metadata,
            // Per-recipient idempotency suffix preserves the producer's grouping intent.
            IdempotencyKey = string.IsNullOrEmpty(src.IdempotencyKey)
                ? null : $"{src.IdempotencyKey}:{r.StableKey}",
            TemplateKey = src.TemplateKey,
            TemplateData = src.TemplateData,
            ProductType = src.ProductType,
            BrandedRendering = src.BrandedRendering,
            OverrideSuppression = src.OverrideSuppression,
            OverrideReason = src.OverrideReason,
        };
    }

    /// <summary>Snapshot of a Role/Org fan-out, persisted under MetadataJson.fanout.</summary>
    public sealed class FanOutSummary
    {
        public string? Mode { get; set; }
        public string? RoleKey { get; set; }
        public string? OrgId { get; set; }
        public string Channel { get; set; } = string.Empty;
        public int TotalResolved { get; set; }
        public int SentCount { get; set; }
        public int FailedCount { get; set; }
        public int BlockedCount { get; set; }
        public int SkippedCount { get; set; }
        public Dictionary<string, int> DeliveredByChannel { get; set; } = new();
        public Dictionary<string, int> SkippedByReason { get; set; } = new();
        public Dictionary<string, int> BlockedByReason { get; set; } = new();
        public List<FanOutPerRecipient> Recipients { get; set; } = new();
    }

    public sealed class FanOutPerRecipient
    {
        public string? UserId { get; set; }
        public string? Email { get; set; }
        public string? OrgId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Reason { get; set; }
        public string? NotificationId { get; set; }
    }

    private async Task<NotificationResultDto> DispatchSingleAsync(Guid tenantId, SubmitNotificationDto request, string recipientJson)
    {
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
            OverrideUsed = enforcement?.OverrideUsed ?? false,
            Severity = request.Severity,
            Category = request.Category
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

        // In-app deliveries have no provider — the persisted Notification record is the delivery.
        if (string.Equals(request.Channel, "in-app", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(request.Channel, "inapp",  StringComparison.OrdinalIgnoreCase))
        {
            notification.Status = "sent";
            await _notificationRepo.UpdateAsync(notification);
            try { await _auditClient.IngestAsync(new IngestAuditEventRequest { EventType = "notification.sent", Action = "notification.sent", SourceSystem = "notifications", Description = "In-app notification persisted", Scope = new AuditEventScopeDto { TenantId = tenantId.ToString() } }); } catch { }
            return MapToResult(notification);
        }

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
        Severity = n.Severity, Category = n.Category,
        CreatedAt = n.CreatedAt, UpdatedAt = n.UpdatedAt
    };

    /// <summary>
    /// Reads the recipient mode tolerating both string ("Role") and numeric (2)
    /// JSON representations of <see cref="Contracts.Notifications.RecipientMode"/>.
    /// Producers may serialize the enum either way depending on their JSON
    /// options; the resolver layer must accept both.
    /// </summary>
    private static string? ReadRecipientMode(JsonElement recipient)
    {
        if (recipient.ValueKind != JsonValueKind.Object) return null;
        foreach (var prop in recipient.EnumerateObject())
        {
            if (!string.Equals(prop.Name, "mode", StringComparison.OrdinalIgnoreCase)) continue;
            return prop.Value.ValueKind switch
            {
                JsonValueKind.String => prop.Value.GetString(),
                JsonValueKind.Number when prop.Value.TryGetInt32(out var n) => n switch
                {
                    0 => "UserId",
                    1 => "Email",
                    2 => "Role",
                    3 => "Org",
                    _ => null,
                },
                _ => null,
            };
        }
        return null;
    }

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
