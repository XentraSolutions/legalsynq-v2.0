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
    private readonly INotificationEventRepository _eventRepo;
    private readonly IDeliveryIssueRepository _deliveryIssueRepo;
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
        INotificationEventRepository eventRepo,
        IDeliveryIssueRepository deliveryIssueRepo,
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
        _notificationRepo    = notificationRepo;
        _attemptRepo         = attemptRepo;
        _eventRepo           = eventRepo;
        _deliveryIssueRepo   = deliveryIssueRepo;
        _routingService      = routingService;
        _contactEnforcement  = contactEnforcement;
        _usageEvaluation     = usageEvaluation;
        _metering            = metering;
        _templateResolution  = templateResolution;
        _templateRendering   = templateRendering;
        _brandingResolution  = brandingResolution;
        _sendGridAdapter     = sendGridAdapter;
        _twilioAdapter       = twilioAdapter;
        _recipientResolver   = recipientResolver;
        _auditClient         = auditClient;
        _logger              = logger;
    }

    // ─── Submit ──────────────────────────────────────────────────────────────

    public async Task<NotificationResultDto> SubmitAsync(Guid tenantId, SubmitNotificationDto request)
    {
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

        if (!string.IsNullOrEmpty(request.IdempotencyKey))
        {
            var existing = await _notificationRepo.FindByIdempotencyKeyAsync(tenantId, request.IdempotencyKey);
            if (existing != null)
                return MapToResult(existing);
        }

        var fanOutMode = mode ?? (recipientEl.ValueKind == JsonValueKind.Array ? "Batch" : "FanOut");
        var resolved   = await _recipientResolver.ResolveAsync(tenantId, recipientEl);
        var roleKey    = ReadRecipientField(recipientEl, "roleKey");
        var orgId      = ReadRecipientField(recipientEl, "orgId");

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
            _logger.LogWarning("Notification fan-out resolved 0 recipients for tenant {TenantId} mode {Mode}", tenantId, fanOutMode);

        return BuildFanOutResult(parent, summary, dispatched);
    }

    // ─── List / Get ───────────────────────────────────────────────────────────

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

    public async Task<PagedNotificationsResponse> ListPagedAsync(Guid tenantId, NotificationListQuery query)
    {
        var (items, total) = await _notificationRepo.GetPagedAsync(tenantId, query);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var page     = Math.Max(1, query.Page);
        var totalPages = (int)Math.Ceiling(total / (double)pageSize);

        return new PagedNotificationsResponse
        {
            Items      = items.Select(MapToDto).ToList(),
            Page       = page,
            PageSize   = pageSize,
            TotalCount = total,
            TotalPages = totalPages,
            AppliedFilters = new AppliedFiltersDto
            {
                Status        = query.Status,
                Channel       = query.Channel,
                Provider      = query.Provider,
                Recipient     = query.Recipient,
                ProductKey    = query.ProductKey,
                From          = query.From,
                To            = query.To,
                SortBy        = query.SortBy,
                SortDirection = query.SortDirection,
            },
        };
    }

    // ─── Stats ────────────────────────────────────────────────────────────────

    public async Task<NotificationStatsDto> GetStatsAsync(Guid tenantId, NotificationStatsQuery query)
    {
        var data = await _notificationRepo.GetStatsAsync(tenantId, query);

        var queued = (data.StatusCounts.GetValueOrDefault("accepted", 0)
                    + data.StatusCounts.GetValueOrDefault("processing", 0)
                    + data.StatusCounts.GetValueOrDefault("retrying", 0));

        return new NotificationStatsDto
        {
            TotalCount        = data.TotalCount,
            QueuedCount       = queued,
            SentCount         = data.StatusCounts.GetValueOrDefault("sent", 0),
            DeliveredCount    = data.DeliveredCount,
            FailedCount       = data.StatusCounts.GetValueOrDefault("failed", 0)
                              + data.StatusCounts.GetValueOrDefault("dead-letter", 0),
            SuppressedCount   = data.StatusCounts.GetValueOrDefault("blocked", 0),
            PartialCount      = data.StatusCounts.GetValueOrDefault("partial", 0),
            ChannelBreakdown  = data.ChannelCounts,
            ProviderBreakdown = data.ProviderCounts,
            StatusDistribution = data.StatusCounts,
            RecentTrend       = data.Trend,
            AppliedFilters    = new AppliedFiltersDto
            {
                Channel    = query.Channel,
                Status     = query.Status,
                Provider   = query.Provider,
                ProductKey = query.ProductKey,
                From       = query.From,
                To         = query.To,
            },
        };
    }

    // ─── Events ───────────────────────────────────────────────────────────────

    public async Task<List<NotificationEventDto>> GetEventsAsync(Guid tenantId, Guid id)
    {
        var notification = await _notificationRepo.GetByIdAndTenantAsync(id, tenantId);
        if (notification == null) return new List<NotificationEventDto>();

        var events = new List<NotificationEventDto>();

        // Synthesize lifecycle event: notification created
        events.Add(new NotificationEventDto
        {
            Id          = id,
            EventType   = "created",
            Source      = "system",
            Timestamp   = notification.CreatedAt,
            Description = $"Notification accepted (channel={notification.Channel}, status={notification.Status})",
        });

        // Synthesize attempt events from NotificationAttempt records
        var attempts = await _attemptRepo.GetByNotificationIdAsync(id);
        foreach (var attempt in attempts)
        {
            events.Add(new NotificationEventDto
            {
                Id          = attempt.Id,
                EventType   = attempt.IsFailover ? "failover_attempted" : "attempted",
                Source      = "system",
                Timestamp   = attempt.CreatedAt,
                Description = $"Attempt #{attempt.AttemptNumber} via {attempt.Provider} — status: {attempt.Status}",
                Provider    = attempt.Provider,
                ProviderMessageId = attempt.ProviderMessageId,
            });

            if (attempt.CompletedAt.HasValue && attempt.Status != "sending")
            {
                events.Add(new NotificationEventDto
                {
                    Id          = attempt.Id,
                    EventType   = attempt.Status == "sent" ? "sent" : "attempt_failed",
                    Source      = "system",
                    Timestamp   = attempt.CompletedAt.Value,
                    Description = attempt.Status == "sent"
                        ? $"Sent via {attempt.Provider}"
                        : $"Attempt #{attempt.AttemptNumber} failed: {attempt.ErrorMessage ?? attempt.FailureCategory}",
                    Provider    = attempt.Provider,
                    ProviderMessageId = attempt.ProviderMessageId,
                });
            }
        }

        // Provider webhook events (delivered, bounced, etc.)
        var providerEvents = await _eventRepo.GetByNotificationIdAsync(id);
        foreach (var evt in providerEvents)
        {
            events.Add(new NotificationEventDto
            {
                Id          = evt.Id,
                EventType   = evt.NormalizedEventType,
                Source      = $"provider:{evt.Provider}",
                Timestamp   = evt.EventTimestamp,
                Description = $"Provider event: {evt.RawEventType} (normalized: {evt.NormalizedEventType})",
                Provider    = evt.Provider,
                ProviderMessageId = evt.ProviderMessageId,
                MetadataJson = evt.MetadataJson,
            });
        }

        // Final/intermediate state synthetic event for non-sent outcomes
        if (notification.Status is "blocked" or "failed" or "partial" or "dead-letter")
        {
            events.Add(new NotificationEventDto
            {
                Id          = notification.Id,
                EventType   = notification.Status,
                Source      = "system",
                Timestamp   = notification.UpdatedAt,
                Description = notification.LastErrorMessage
                    ?? $"Notification reached terminal status: {notification.Status}",
            });
        }
        else if (notification.Status == "retrying")
        {
            events.Add(new NotificationEventDto
            {
                Id          = notification.Id,
                EventType   = "retrying",
                Source      = "system",
                Timestamp   = notification.UpdatedAt,
                Description = notification.LastErrorMessage
                    ?? $"Notification scheduled for retry #{notification.RetryCount}",
            });
        }

        return events.OrderBy(e => e.Timestamp).ThenBy(e => e.EventType).ToList();
    }

    // ─── Issues ───────────────────────────────────────────────────────────────

    public async Task<List<NotificationIssueDto>> GetIssuesAsync(Guid tenantId, Guid id)
    {
        // Verify tenant ownership
        var notification = await _notificationRepo.GetByIdAndTenantAsync(id, tenantId);
        if (notification == null) return new List<NotificationIssueDto>();

        var issues = await _deliveryIssueRepo.GetByNotificationIdAsync(id);
        return issues.Select(i => new NotificationIssueDto
        {
            Id                = i.Id,
            IssueType         = i.IssueType,
            Channel           = i.Channel,
            Provider          = string.IsNullOrEmpty(i.Provider) ? null : i.Provider,
            RecommendedAction = i.RecommendedAction,
            DetailsJson       = i.DetailsJson,
            IsResolved        = i.IsResolved,
            ResolvedAt        = i.ResolvedAt,
            CreatedAt         = i.CreatedAt,
        }).ToList();
    }

    // ─── Retry ────────────────────────────────────────────────────────────────

    private static readonly HashSet<string> RetryableFailureCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "retryable_provider_failure",
        "provider_unavailable",
        "auth_config_failure",
    };

    public async Task<RetryResultDto?> RetryAsync(Guid tenantId, Guid id, string? actorUserId = null)
    {
        var notification = await _notificationRepo.GetByIdAndTenantAsync(id, tenantId);
        if (notification == null) return null;

        if (notification.Status != "failed")
            return new RetryResultDto
            {
                NotificationId = id,
                PreviousStatus = notification.Status,
                NewStatus      = notification.Status,
                FailureCategory = "not_retryable",
                LastErrorMessage = $"Notification is not in a retryable state (current status: {notification.Status})",
                RetriedAt      = DateTime.UtcNow,
            };

        if (!string.IsNullOrEmpty(notification.FailureCategory) &&
            !RetryableFailureCategories.Contains(notification.FailureCategory))
            return new RetryResultDto
            {
                NotificationId = id,
                PreviousStatus = notification.Status,
                NewStatus      = notification.Status,
                FailureCategory = notification.FailureCategory,
                LastErrorMessage = $"Failure category '{notification.FailureCategory}' is not retryable",
                RetriedAt      = DateTime.UtcNow,
            };

        var previousStatus = notification.Status;

        // Determine base attempt number from existing attempts
        var existingAttempts = await _attemptRepo.GetByNotificationIdAsync(id);
        var baseAttemptNumber = existingAttempts.Count;

        notification.Status = "processing";
        notification.FailureCategory = null;
        notification.LastErrorMessage = null;
        await _notificationRepo.UpdateAsync(notification);

        await ExecuteSendLoopAsync(tenantId, notification, baseAttemptNumber);

        try
        {
            await _auditClient.IngestAsync(new IngestAuditEventRequest
            {
                EventType    = "notification.retry",
                Action       = "notification.retry",
                SourceSystem = "notifications",
                Description  = $"Operator-triggered retry for notification {id}; previous status: {previousStatus}; new status: {notification.Status}; actor: {actorUserId ?? "internal"}",
                Scope        = new AuditEventScopeDto { TenantId = tenantId.ToString(), UserId = actorUserId }
            });
        }
        catch { /* audit best-effort */ }

        return new RetryResultDto
        {
            NotificationId   = id,
            PreviousStatus   = previousStatus,
            NewStatus        = notification.Status,
            ProviderUsed     = notification.ProviderUsed,
            FailureCategory  = notification.FailureCategory,
            LastErrorMessage = notification.LastErrorMessage,
            RetriedAt        = DateTime.UtcNow,
        };
    }

    // ─── Resend ───────────────────────────────────────────────────────────────

    public async Task<ResendResultDto?> ResendAsync(Guid tenantId, Guid id, string? actorUserId = null)
    {
        var original = await _notificationRepo.GetByIdAndTenantAsync(id, tenantId);
        if (original == null) return null;

        // Build metadata with resendOf link
        Dictionary<string, object?> metaDict = new();
        if (!string.IsNullOrEmpty(original.MetadataJson))
        {
            try { metaDict = JsonSerializer.Deserialize<Dictionary<string, object?>>(original.MetadataJson) ?? new(); }
            catch { metaDict = new(); }
        }
        metaDict["resendOf"] = original.Id.ToString();
        var newMetaJson = JsonSerializer.Serialize(metaDict);

        // Rebuild original message/recipient from stored JSON
        object recipient;
        object message;
        try { recipient = JsonSerializer.Deserialize<object>(original.RecipientJson) ?? new(); } catch { recipient = new(); }
        try { message   = JsonSerializer.Deserialize<object>(original.MessageJson)   ?? new(); } catch { message   = new(); }

        var resendRequest = new SubmitNotificationDto
        {
            Channel        = original.Channel,
            Recipient      = recipient,
            Message        = message,
            Metadata       = JsonSerializer.Deserialize<object>(newMetaJson),
            IdempotencyKey = null,            // Force fresh dispatch
            TemplateKey    = original.TemplateKey,
            Severity       = original.Severity,
            Category       = original.Category,
        };

        var result = await SubmitAsync(tenantId, resendRequest);

        try
        {
            await _auditClient.IngestAsync(new IngestAuditEventRequest
            {
                EventType    = "notification.resend",
                Action       = "notification.resend",
                SourceSystem = "notifications",
                Description  = $"Operator-triggered resend of notification {id}; new notification {result.Id}; actor: {actorUserId ?? "internal"}",
                Scope        = new AuditEventScopeDto { TenantId = tenantId.ToString(), UserId = actorUserId }
            });
        }
        catch { /* audit best-effort */ }

        return new ResendResultDto
        {
            OriginalNotificationId = id,
            NewNotificationId      = result.Id,
            Status                 = result.Status,
            CreatedAt              = DateTime.UtcNow,
        };
    }

    // ─── Admin cross-tenant operations ───────────────────────────────────────

    public async Task<PagedNotificationsResponse> AdminListPagedAsync(Guid? tenantId, NotificationListQuery query, string actorUserId)
    {
        var query2 = new NotificationListQuery
        {
            Page          = query.Page == 0 ? 1 : query.Page,
            PageSize      = query.PageSize == 0 ? 50 : query.PageSize,
            Status        = query.Status,
            Channel       = query.Channel,
            Provider      = query.Provider,
            Recipient     = query.Recipient,
            ProductKey    = query.ProductKey,
            From          = query.From,
            To            = query.To,
            SortBy        = query.SortBy,
            SortDirection = query.SortDirection,
        };

        var (items, total) = await _notificationRepo.GetPagedAdminAsync(tenantId, query2);

        _logger.LogInformation(
            "Admin list query by {ActorUserId}: tenantFilter={TenantId} page={Page} pageSize={PageSize} total={Total}",
            actorUserId, tenantId?.ToString() ?? "ALL", query2.Page, query2.PageSize, total);

        try
        {
            await _auditClient.IngestAsync(new IngestAuditEventRequest
            {
                EventType    = "admin.notification.list",
                Action       = "admin.notification.list",
                SourceSystem = "notifications",
                Description  = $"Admin list query by {actorUserId}; tenantFilter={tenantId?.ToString() ?? "ALL"}; total={total}",
                Scope        = new AuditEventScopeDto
                {
                    ScopeType = LegalSynq.AuditClient.Enums.ScopeType.Platform,
                    TenantId  = tenantId?.ToString(),
                    UserId    = actorUserId,
                }
            });
        }
        catch { /* audit best-effort */ }

        var pageSize = Math.Clamp(query2.PageSize, 1, 200);
        return new PagedNotificationsResponse
        {
            Items          = items.Select(MapToDto).ToList(),
            TotalCount     = total,
            Page           = query2.Page,
            PageSize       = pageSize,
            TotalPages     = (int)Math.Ceiling((double)total / pageSize),
            AppliedFilters = new AppliedFiltersDto
            {
                Status        = query2.Status,
                Channel       = query2.Channel,
                Provider      = query2.Provider,
                Recipient     = query2.Recipient,
                ProductKey    = query2.ProductKey,
                From          = query2.From,
                To            = query2.To,
                SortBy        = query2.SortBy,
                SortDirection = query2.SortDirection,
            },
        };
    }

    public async Task<NotificationStatsDto> AdminGetStatsAsync(Guid? tenantId, NotificationStatsQuery query, string actorUserId)
    {
        var data = await _notificationRepo.GetStatsAdminAsync(tenantId, query);

        _logger.LogInformation(
            "Admin stats query by {ActorUserId}: tenantFilter={TenantId}",
            actorUserId, tenantId?.ToString() ?? "ALL");

        try
        {
            await _auditClient.IngestAsync(new IngestAuditEventRequest
            {
                EventType    = "admin.notification.stats",
                Action       = "admin.notification.stats",
                SourceSystem = "notifications",
                Description  = $"Admin stats query by {actorUserId}; tenantFilter={tenantId?.ToString() ?? "ALL"}; total={data.TotalCount}",
                Scope        = new AuditEventScopeDto
                {
                    ScopeType = LegalSynq.AuditClient.Enums.ScopeType.Platform,
                    TenantId  = tenantId?.ToString(),
                    UserId    = actorUserId,
                }
            });
        }
        catch { /* audit best-effort */ }

        return BuildStatsDto(data, query);
    }

    public async Task<List<NotificationEventDto>> AdminGetEventsAsync(Guid notificationId, string actorUserId)
    {
        var notification = await _notificationRepo.GetByIdAsync(notificationId);
        if (notification == null) return new List<NotificationEventDto>();

        _logger.LogInformation(
            "Admin events lookup by {ActorUserId}: notificationId={NotificationId} tenantId={TenantId}",
            actorUserId, notificationId, notification.TenantId);

        try
        {
            await _auditClient.IngestAsync(new IngestAuditEventRequest
            {
                EventType    = "admin.notification.events",
                Action       = "admin.notification.events",
                SourceSystem = "notifications",
                Description  = $"Admin events lookup by {actorUserId}; notificationId={notificationId}; tenantId={notification.TenantId}",
                Scope        = new AuditEventScopeDto
                {
                    ScopeType = LegalSynq.AuditClient.Enums.ScopeType.Platform,
                    TenantId  = notification.TenantId.ToString(),
                    UserId    = actorUserId,
                }
            });
        }
        catch { /* audit best-effort */ }

        return await BuildEventTimelineAsync(notification);
    }

    public async Task<List<NotificationIssueDto>> AdminGetIssuesAsync(Guid notificationId, string actorUserId)
    {
        var notification = await _notificationRepo.GetByIdAsync(notificationId);
        if (notification == null) return new List<NotificationIssueDto>();

        _logger.LogInformation(
            "Admin issues lookup by {ActorUserId}: notificationId={NotificationId} tenantId={TenantId}",
            actorUserId, notificationId, notification.TenantId);

        try
        {
            await _auditClient.IngestAsync(new IngestAuditEventRequest
            {
                EventType    = "admin.notification.issues",
                Action       = "admin.notification.issues",
                SourceSystem = "notifications",
                Description  = $"Admin issues lookup by {actorUserId}; notificationId={notificationId}; tenantId={notification.TenantId}",
                Scope        = new AuditEventScopeDto
                {
                    ScopeType = LegalSynq.AuditClient.Enums.ScopeType.Platform,
                    TenantId  = notification.TenantId.ToString(),
                    UserId    = actorUserId,
                }
            });
        }
        catch { /* audit best-effort */ }

        var issues = await _deliveryIssueRepo.GetByNotificationIdAsync(notificationId);
        return issues.Select(i => new NotificationIssueDto
        {
            Id                = i.Id,
            IssueType         = i.IssueType,
            Channel           = i.Channel,
            Provider          = string.IsNullOrEmpty(i.Provider) ? null : i.Provider,
            RecommendedAction = i.RecommendedAction,
            DetailsJson       = i.DetailsJson,
            IsResolved        = i.IsResolved,
            ResolvedAt        = i.ResolvedAt,
            CreatedAt         = i.CreatedAt,
        }).ToList();
    }

    public async Task<RetryResultDto?> AdminRetryAsync(Guid notificationId, string actorUserId)
    {
        var notification = await _notificationRepo.GetByIdAsync(notificationId);
        if (notification == null) return null;

        var tenantId = notification.TenantId.GetValueOrDefault();

        if (notification.Status != "failed")
            return new RetryResultDto
            {
                NotificationId   = notificationId,
                PreviousStatus   = notification.Status,
                NewStatus        = notification.Status,
                FailureCategory  = "not_retryable",
                LastErrorMessage = $"Notification is not in a retryable state (current status: {notification.Status})",
                RetriedAt        = DateTime.UtcNow,
            };

        if (!string.IsNullOrEmpty(notification.FailureCategory) &&
            !RetryableFailureCategories.Contains(notification.FailureCategory))
            return new RetryResultDto
            {
                NotificationId   = notificationId,
                PreviousStatus   = notification.Status,
                NewStatus        = notification.Status,
                FailureCategory  = notification.FailureCategory,
                LastErrorMessage = $"Failure category '{notification.FailureCategory}' is not retryable",
                RetriedAt        = DateTime.UtcNow,
            };

        var previousStatus = notification.Status;
        var existingAttempts = await _attemptRepo.GetByNotificationIdAsync(notificationId);
        var baseAttemptNumber = existingAttempts.Count;

        notification.Status = "processing";
        notification.FailureCategory = null;
        notification.LastErrorMessage = null;
        await _notificationRepo.UpdateAsync(notification);

        await ExecuteSendLoopAsync(tenantId, notification, baseAttemptNumber);

        _logger.LogInformation(
            "Admin retry by {ActorUserId}: notificationId={NotificationId} tenantId={TenantId} previousStatus={Prev} newStatus={New}",
            actorUserId, notificationId, tenantId, previousStatus, notification.Status);

        try
        {
            await _auditClient.IngestAsync(new IngestAuditEventRequest
            {
                EventType    = "admin.notification.retry",
                Action       = "admin.notification.retry",
                SourceSystem = "notifications",
                Description  = $"Admin retry by {actorUserId}; notificationId={notificationId}; tenantId={tenantId}; previousStatus={previousStatus}; newStatus={notification.Status}",
                Scope        = new AuditEventScopeDto
                {
                    ScopeType = LegalSynq.AuditClient.Enums.ScopeType.Platform,
                    TenantId  = tenantId.ToString(),
                    UserId    = actorUserId,
                }
            });
        }
        catch { /* audit best-effort */ }

        return new RetryResultDto
        {
            NotificationId   = notificationId,
            PreviousStatus   = previousStatus,
            NewStatus        = notification.Status,
            ProviderUsed     = notification.ProviderUsed,
            FailureCategory  = notification.FailureCategory,
            LastErrorMessage = notification.LastErrorMessage,
            RetriedAt        = DateTime.UtcNow,
        };
    }

    public async Task<ResendResultDto?> AdminResendAsync(Guid notificationId, string actorUserId)
    {
        var original = await _notificationRepo.GetByIdAsync(notificationId);
        if (original == null) return null;

        var tenantId = original.TenantId.GetValueOrDefault();

        Dictionary<string, object?> metaDict = new();
        if (!string.IsNullOrEmpty(original.MetadataJson))
        {
            try { metaDict = JsonSerializer.Deserialize<Dictionary<string, object?>>(original.MetadataJson) ?? new(); }
            catch { metaDict = new(); }
        }
        metaDict["resendOf"]    = original.Id.ToString();
        metaDict["adminResend"] = actorUserId;
        var newMetaJson = JsonSerializer.Serialize(metaDict);

        object recipient;
        object message;
        try { recipient = JsonSerializer.Deserialize<object>(original.RecipientJson) ?? new(); } catch { recipient = new(); }
        try { message   = JsonSerializer.Deserialize<object>(original.MessageJson)   ?? new(); } catch { message   = new(); }

        var resendRequest = new SubmitNotificationDto
        {
            Channel        = original.Channel,
            Recipient      = recipient,
            Message        = message,
            Metadata       = JsonSerializer.Deserialize<object>(newMetaJson),
            IdempotencyKey = null,
            TemplateKey    = original.TemplateKey,
            Severity       = original.Severity,
            Category       = original.Category,
        };

        var result = await SubmitAsync(tenantId, resendRequest);

        _logger.LogInformation(
            "Admin resend by {ActorUserId}: originalId={OriginalId} tenantId={TenantId} newId={NewId}",
            actorUserId, notificationId, tenantId, result.Id);

        try
        {
            await _auditClient.IngestAsync(new IngestAuditEventRequest
            {
                EventType    = "admin.notification.resend",
                Action       = "admin.notification.resend",
                SourceSystem = "notifications",
                Description  = $"Admin resend by {actorUserId}; originalId={notificationId}; tenantId={tenantId}; newId={result.Id}",
                Scope        = new AuditEventScopeDto
                {
                    ScopeType = LegalSynq.AuditClient.Enums.ScopeType.Platform,
                    TenantId  = tenantId.ToString(),
                    UserId    = actorUserId,
                }
            });
        }
        catch { /* audit best-effort */ }

        return new ResendResultDto
        {
            OriginalNotificationId = notificationId,
            NewNotificationId      = result.Id,
            Status                 = result.Status,
            CreatedAt              = DateTime.UtcNow,
        };
    }

    // ─── Shared helpers ───────────────────────────────────────────────────────

    private async Task<List<NotificationEventDto>> BuildEventTimelineAsync(Notification notification)
    {
        var events = new List<NotificationEventDto>();

        events.Add(new NotificationEventDto
        {
            Id          = notification.Id,
            EventType   = "created",
            Source      = "system",
            Timestamp   = notification.CreatedAt,
            Description = $"Notification accepted (channel={notification.Channel}, status={notification.Status})",
        });

        var attempts = await _attemptRepo.GetByNotificationIdAsync(notification.Id);
        foreach (var attempt in attempts)
        {
            events.Add(new NotificationEventDto
            {
                Id          = attempt.Id,
                EventType   = attempt.IsFailover ? "failover_attempted" : "attempted",
                Source      = "system",
                Timestamp   = attempt.CreatedAt,
                Description = $"Attempt #{attempt.AttemptNumber} via {attempt.Provider} — status: {attempt.Status}",
                Provider    = attempt.Provider,
                ProviderMessageId = attempt.ProviderMessageId,
            });

            if (attempt.CompletedAt.HasValue && attempt.Status != "sending")
            {
                events.Add(new NotificationEventDto
                {
                    Id          = attempt.Id,
                    EventType   = attempt.Status == "sent" ? "sent" : "attempt_failed",
                    Source      = "system",
                    Timestamp   = attempt.CompletedAt.Value,
                    Description = attempt.Status == "sent"
                        ? $"Sent via {attempt.Provider}"
                        : $"Attempt #{attempt.AttemptNumber} failed: {attempt.ErrorMessage ?? attempt.FailureCategory}",
                    Provider    = attempt.Provider,
                    ProviderMessageId = attempt.ProviderMessageId,
                });
            }
        }

        var providerEvents = await _eventRepo.GetByNotificationIdAsync(notification.Id);
        foreach (var evt in providerEvents)
        {
            events.Add(new NotificationEventDto
            {
                Id          = evt.Id,
                EventType   = evt.NormalizedEventType,
                Source      = $"provider:{evt.Provider}",
                Timestamp   = evt.EventTimestamp,
                Description = $"Provider event: {evt.RawEventType} (normalized: {evt.NormalizedEventType})",
                Provider    = evt.Provider,
                ProviderMessageId = evt.ProviderMessageId,
                MetadataJson = evt.MetadataJson,
            });
        }

        if (notification.Status is "blocked" or "failed" or "partial")
        {
            events.Add(new NotificationEventDto
            {
                Id          = notification.Id,
                EventType   = notification.Status,
                Source      = "system",
                Timestamp   = notification.UpdatedAt,
                Description = notification.LastErrorMessage
                    ?? $"Notification reached terminal status: {notification.Status}",
            });
        }

        return events.OrderBy(e => e.Timestamp).ThenBy(e => e.EventType).ToList();
    }

    private static NotificationStatsDto BuildStatsDto(NotificationStatsData data, NotificationStatsQuery? query = null)
    {
        var queued = data.StatusCounts.GetValueOrDefault("accepted", 0)
                   + data.StatusCounts.GetValueOrDefault("processing", 0)
                   + data.StatusCounts.GetValueOrDefault("retrying", 0);

        return new NotificationStatsDto
        {
            TotalCount         = data.TotalCount,
            QueuedCount        = queued,
            SentCount          = data.StatusCounts.GetValueOrDefault("sent", 0),
            DeliveredCount     = data.DeliveredCount,
            FailedCount        = data.StatusCounts.GetValueOrDefault("failed", 0)
                               + data.StatusCounts.GetValueOrDefault("dead-letter", 0),
            SuppressedCount    = data.StatusCounts.GetValueOrDefault("blocked", 0),
            PartialCount       = data.StatusCounts.GetValueOrDefault("partial", 0),
            ChannelBreakdown   = data.ChannelCounts,
            ProviderBreakdown  = data.ProviderCounts,
            StatusDistribution = data.StatusCounts,
            RecentTrend        = data.Trend,
            AppliedFilters     = new AppliedFiltersDto
            {
                Channel    = query?.Channel,
                Status     = query?.Status,
                Provider   = query?.Provider,
                ProductKey = query?.ProductKey,
                From       = query?.From,
                To         = query?.To,
            },
        };
    }

    // ─── Send loop (shared by initial dispatch and retry) ────────────────────

    private async Task ExecuteSendLoopAsync(Guid tenantId, Notification notification, int baseAttemptNumber = 0)
    {
        var contactValue = ExtractContactValue(notification.Channel, notification.RecipientJson);
        var routes = await _routingService.ResolveRoutesAsync(tenantId, notification.Channel);

        string? subject = notification.RenderedSubject;
        string? body    = notification.RenderedBody;
        string? html    = null;

        if (string.IsNullOrEmpty(subject) || string.IsNullOrEmpty(body))
        {
            try
            {
                var msg = JsonSerializer.Deserialize<JsonElement>(notification.MessageJson);
                subject ??= msg.TryGetProperty("subject", out var s) ? s.GetString() : "";
                body    ??= msg.TryGetProperty("body",    out var b) ? b.GetString() : "";
                html      = msg.TryGetProperty("html",    out var h) ? h.GetString() : null;
            }
            catch { /* use whatever we have */ }
        }

        foreach (var route in routes)
        {
            var attemptNumber = baseAttemptNumber + routes.IndexOf(route) + 1;
            var attempt = await _attemptRepo.CreateAsync(new NotificationAttempt
            {
                TenantId            = tenantId,
                NotificationId      = notification.Id,
                Channel             = notification.Channel,
                Provider            = route.ProviderType,
                Status              = "sending",
                AttemptNumber       = attemptNumber,
                ProviderOwnershipMode = route.OwnershipMode,
                ProviderConfigId    = route.TenantProviderConfigId,
                IsFailover          = route.IsFailover
            });

            _ = _metering.MeterAsync(new MeterEventInput
            {
                TenantId = tenantId,
                UsageUnit = notification.Channel == "email" ? "email_attempt" : "sms_attempt",
                Channel = notification.Channel,
                NotificationId = notification.Id,
                NotificationAttemptId = attempt.Id,
                Provider = route.ProviderType,
                ProviderOwnershipMode = route.OwnershipMode,
                ProviderConfigId = route.TenantProviderConfigId
            });

            bool success;
            string? providerMessageId = null;
            ProviderFailure? failure = null;

            if (notification.Channel == "email")
            {
                var result = await _sendGridAdapter.SendAsync(new EmailSendPayload
                {
                    To = contactValue ?? "", Subject = subject ?? "", Body = body ?? "", Html = html
                });
                success = result.Success;
                providerMessageId = result.ProviderMessageId;
                failure = result.Failure;
            }
            else
            {
                var result = await _twilioAdapter.SendAsync(new SmsSendPayload
                {
                    To = contactValue ?? "", Body = body ?? ""
                });
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

                notification.Status              = "sent";
                notification.ProviderUsed        = route.ProviderType;
                notification.ProviderOwnershipMode = route.OwnershipMode;
                notification.ProviderConfigId    = route.TenantProviderConfigId;
                notification.PlatformFallbackUsed = route.IsPlatformFallback;
                await _notificationRepo.UpdateAsync(notification);

                _ = _metering.MeterAsync(new MeterEventInput
                {
                    TenantId = tenantId,
                    UsageUnit = notification.Channel == "email" ? "email_sent" : "sms_sent",
                    Channel = notification.Channel,
                    NotificationId = notification.Id,
                    NotificationAttemptId = attempt.Id,
                    Provider = route.ProviderType,
                    ProviderOwnershipMode = route.OwnershipMode
                });
                try { await _auditClient.IngestAsync(new IngestAuditEventRequest { EventType = "notification.sent", Action = "notification.sent", SourceSystem = "notifications", Description = "Notification sent successfully", Scope = new AuditEventScopeDto { TenantId = tenantId.ToString() } }); } catch { }
                return;
            }

            attempt.Status = "failed";
            attempt.FailureCategory = failure?.Category;
            attempt.ErrorMessage = failure?.Message;
            attempt.CompletedAt = DateTime.UtcNow;
            await _attemptRepo.UpdateAsync(attempt);

            if (route.IsFailover)
                _ = _metering.MeterAsync(new MeterEventInput { TenantId = tenantId, UsageUnit = "provider_failover_attempt", Channel = notification.Channel, NotificationId = notification.Id, NotificationAttemptId = attempt.Id, Provider = route.ProviderType });

            if (failure?.Retryable != true) break;
        }

        var isRetryable = routes.Count > 0;
        if (!isRetryable)
        {
            notification.Status = "failed";
            notification.FailureCategory = "auth_config_failure";
            notification.LastErrorMessage = "No provider routes configured";
            await _notificationRepo.UpdateAsync(notification);
            try { await _auditClient.IngestAsync(new IngestAuditEventRequest { EventType = "notification.failed", Action = "notification.failed", SourceSystem = "notifications", Description = "Notification failed - no provider routes", Scope = new AuditEventScopeDto { TenantId = tenantId.ToString() } }); } catch { }
        }
        else if (notification.RetryCount >= notification.MaxRetries)
        {
            notification.Status = "dead-letter";
            notification.FailureCategory = "max_retries_exhausted";
            notification.LastErrorMessage = $"Delivery failed after {notification.RetryCount} retries - all routes exhausted";
            await _notificationRepo.UpdateAsync(notification);
            await CreateDeadLetterIssueAsync(notification);
            try { await _auditClient.IngestAsync(new IngestAuditEventRequest { EventType = "notification.dead_letter", Action = "notification.dead_letter", SourceSystem = "notifications", Description = $"Notification moved to dead-letter after {notification.RetryCount} retries", Scope = new AuditEventScopeDto { TenantId = tenantId.ToString() } }); } catch { }
        }
        else
        {
            notification.RetryCount++;
            notification.NextRetryAt = ComputeNextRetryAt(notification.RetryCount);
            notification.Status = "retrying";
            notification.FailureCategory = "retryable_provider_failure";
            notification.LastErrorMessage = $"All routes exhausted - retry #{notification.RetryCount} scheduled at {notification.NextRetryAt:u}";
            await _notificationRepo.UpdateAsync(notification);
            try { await _auditClient.IngestAsync(new IngestAuditEventRequest { EventType = "notification.retrying", Action = "notification.retrying", SourceSystem = "notifications", Description = $"Notification scheduled for retry #{notification.RetryCount}", Scope = new AuditEventScopeDto { TenantId = tenantId.ToString() } }); } catch { }
        }
    }

    private static DateTime ComputeNextRetryAt(int retryCount) => retryCount switch
    {
        1 => DateTime.UtcNow.AddMinutes(1),
        2 => DateTime.UtcNow.AddMinutes(5),
        _ => DateTime.UtcNow.AddMinutes(30),
    };

    private async Task CreateDeadLetterIssueAsync(Notification notification)
    {
        try
        {
            await _deliveryIssueRepo.CreateIfNotExistsAsync(new DeliveryIssue
            {
                TenantId             = notification.TenantId.GetValueOrDefault(),
                NotificationId       = notification.Id,
                Channel              = notification.Channel,
                Provider             = notification.ProviderUsed ?? "unknown",
                IssueType            = "max_retries_exhausted",
                RecommendedAction    = "Notification exceeded maximum retry attempts. Manual intervention or resend required.",
                DetailsJson          = JsonSerializer.Serialize(new
                {
                    retryCount       = notification.RetryCount,
                    maxRetries       = notification.MaxRetries,
                    failureCategory  = notification.FailureCategory,
                    lastErrorMessage = notification.LastErrorMessage,
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create dead-letter issue for notification {Id}", notification.Id);
        }
    }

    // ─── Worker operations ────────────────────────────────────────────────────

    public async Task ProcessAutoRetryAsync(Guid notificationId)
    {
        var notification = await _notificationRepo.GetByIdAsync(notificationId);
        if (notification == null)
        {
            _logger.LogWarning("ProcessAutoRetryAsync: notification {Id} not found", notificationId);
            return;
        }

        if (notification.Status != "retrying")
        {
            _logger.LogWarning("ProcessAutoRetryAsync: notification {Id} is not in retrying status (current: {Status}) — skipping", notificationId, notification.Status);
            return;
        }

        var tenantId = notification.TenantId.GetValueOrDefault();
        var existingAttempts = await _attemptRepo.GetByNotificationIdAsync(notificationId);
        var baseAttemptNumber = existingAttempts.Count;

        notification.Status = "processing";
        notification.NextRetryAt = null;
        await _notificationRepo.UpdateAsync(notification);

        _logger.LogInformation("ProcessAutoRetryAsync: executing retry #{RetryCount} for notification {Id}", notification.RetryCount, notificationId);

        await ExecuteSendLoopAsync(tenantId, notification, baseAttemptNumber);

        try
        {
            await _auditClient.IngestAsync(new IngestAuditEventRequest
            {
                EventType    = "notification.auto_retry",
                Action       = "notification.auto_retry",
                SourceSystem = "notifications",
                Description  = $"Worker auto-retry #{notification.RetryCount} for notification {notificationId}; result: {notification.Status}",
                Scope        = new AuditEventScopeDto { TenantId = tenantId.ToString() }
            });
        }
        catch { /* audit best-effort */ }
    }

    public async Task ReconcileStalledAsync()
    {
        var stalled = await _notificationRepo.GetStalledProcessingAsync(TimeSpan.FromMinutes(5), batchSize: 20);
        if (stalled.Count == 0) return;

        _logger.LogWarning("ReconcileStalledAsync: found {Count} stalled processing notifications", stalled.Count);

        foreach (var notification in stalled)
        {
            var tenantId = notification.TenantId.GetValueOrDefault();

            if (notification.RetryCount < notification.MaxRetries)
            {
                notification.RetryCount++;
                notification.NextRetryAt = ComputeNextRetryAt(notification.RetryCount);
                notification.Status = "retrying";
                notification.FailureCategory = "stalled_processing";
                notification.LastErrorMessage = $"Notification stalled in processing state — retry #{notification.RetryCount} scheduled";
                await _notificationRepo.UpdateAsync(notification);
                _logger.LogInformation("Stalled notification {Id} scheduled for retry #{Retry}", notification.Id, notification.RetryCount);
            }
            else
            {
                notification.Status = "dead-letter";
                notification.FailureCategory = "max_retries_exhausted";
                notification.LastErrorMessage = $"Delivery failed: stalled after {notification.RetryCount} retries";
                await _notificationRepo.UpdateAsync(notification);
                await CreateDeadLetterIssueAsync(notification);
                _logger.LogWarning("Stalled notification {Id} moved to dead-letter after {Retries} retries", notification.Id, notification.RetryCount);
            }

            try
            {
                await _auditClient.IngestAsync(new IngestAuditEventRequest
                {
                    EventType    = "notification.stalled_reconciled",
                    Action       = "notification.stalled_reconciled",
                    SourceSystem = "notifications",
                    Description  = $"Stalled notification {notification.Id} reconciled to status '{notification.Status}'",
                    Scope        = new AuditEventScopeDto { TenantId = tenantId.ToString() }
                });
            }
            catch { /* audit best-effort */ }
        }
    }

    // ─── Single dispatch ─────────────────────────────────────────────────────

    private async Task<NotificationResultDto> DispatchSingleAsync(Guid tenantId, SubmitNotificationDto request, string recipientJson)
    {
        var messageJson = JsonSerializer.Serialize(request.Message);

        // Merge canonical producer context fields into metadata (LS-NOTIF-CORE-020).
        // Producer-supplied metadata is preserved; canonical fields are added as
        // fallback keys so they never overwrite intentional metadata values.
        Dictionary<string, object?> metaDict;
        if (request.Metadata != null)
        {
            try
            {
                var raw = JsonSerializer.Serialize(request.Metadata);
                metaDict = JsonSerializer.Deserialize<Dictionary<string, object?>>(raw) ?? new();
            }
            catch { metaDict = new(); }
        }
        else { metaDict = new(); }

        if (!string.IsNullOrEmpty(request.EventKey))      metaDict.TryAdd("eventKey",      request.EventKey);
        if (!string.IsNullOrEmpty(request.SourceSystem))  metaDict.TryAdd("sourceSystem",  request.SourceSystem);
        if (!string.IsNullOrEmpty(request.CorrelationId)) metaDict.TryAdd("correlationId", request.CorrelationId);
        if (!string.IsNullOrEmpty(request.RequestedBy))   metaDict.TryAdd("requestedBy",   request.RequestedBy);

        var metadataJson = metaDict.Count > 0 ? JsonSerializer.Serialize(metaDict) : null;

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

        // Canonical product key: prefer ProductKey, fall back to legacy ProductType.
        var effectiveProductKey = request.ProductKey ?? request.ProductType;

        var notification = new Notification
        {
            TenantId = tenantId, Channel = request.Channel, Status = "accepted",
            RecipientJson = recipientJson, MessageJson = messageJson, MetadataJson = metadataJson,
            IdempotencyKey = request.IdempotencyKey, TemplateKey = request.TemplateKey,
            BlockedByPolicy = enforcement is { Allowed: false },
            BlockedReasonCode = enforcement is { Allowed: false } ? enforcement.ReasonCode : null,
            OverrideUsed = enforcement?.OverrideUsed ?? false,
            Severity = request.Severity,
            Category = request.Category ?? effectiveProductKey
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
            if (!string.IsNullOrEmpty(effectiveProductKey))
                resolved = await _templateResolution.ResolveByProductAsync(tenantId, request.TemplateKey, request.Channel, effectiveProductKey);
            else
                resolved = await _templateResolution.ResolveAsync(tenantId, request.TemplateKey, request.Channel);

            if (resolved != null)
            {
                templateId = resolved.Template.Id;
                templateVersionId = resolved.Version.Id;

                RenderResult rendered;
                if (request.BrandedRendering == true && !string.IsNullOrEmpty(effectiveProductKey))
                {
                    var branding = await _brandingResolution.ResolveAsync(tenantId, effectiveProductKey);
                    var tokens = _brandingResolution.BuildBrandingTokens(branding);
                    rendered = _templateRendering.RenderBranded(resolved.Version.SubjectTemplate, resolved.Version.BodyTemplate, resolved.Version.TextTemplate, request.TemplateData, tokens);
                }
                else
                {
                    rendered = _templateRendering.Render(resolved.Version.SubjectTemplate, resolved.Version.BodyTemplate, resolved.Version.TextTemplate, request.TemplateData);
                }

                renderedSubject = rendered.Subject;
                renderedBody    = rendered.Body;
                renderedText    = rendered.Text;

                _ = _metering.MeterAsync(new MeterEventInput { TenantId = tenantId, UsageUnit = "template_render", Channel = request.Channel, NotificationId = notification.Id });
            }
        }

        notification.TemplateId        = templateId;
        notification.TemplateVersionId = templateVersionId;
        notification.RenderedSubject   = renderedSubject;
        notification.RenderedBody      = renderedBody;
        notification.RenderedText      = renderedText;
        notification.Status            = "processing";

        notification = await _notificationRepo.CreateAsync(notification);
        _ = _metering.MeterAsync(new MeterEventInput { TenantId = tenantId, UsageUnit = "api_notification_request", Channel = request.Channel, NotificationId = notification.Id });

        // In-app deliveries have no provider — the persisted Notification is the delivery.
        if (string.Equals(request.Channel, "in-app", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(request.Channel, "inapp",  StringComparison.OrdinalIgnoreCase))
        {
            notification.Status = "sent";
            await _notificationRepo.UpdateAsync(notification);
            try { await _auditClient.IngestAsync(new IngestAuditEventRequest { EventType = "notification.sent", Action = "notification.sent", SourceSystem = "notifications", Description = "In-app notification persisted", Scope = new AuditEventScopeDto { TenantId = tenantId.ToString() } }); } catch { }
            return MapToResult(notification);
        }

        await ExecuteSendLoopAsync(tenantId, notification);
        return MapToResult(notification);
    }

    // ─── Fan-out helpers ──────────────────────────────────────────────────────

    private static string? ClassifySkipReason(string channel, ResolvedRecipient r)
    {
        var ch = channel?.Trim().ToLowerInvariant();
        return ch switch
        {
            "email"                 => string.IsNullOrWhiteSpace(r.Email)  ? "no_email_on_file"  : null,
            "sms"                   => string.IsNullOrWhiteSpace(r.Phone)  ? "no_phone_on_file"  : null,
            "push"                  => string.IsNullOrWhiteSpace(r.UserId) ? "no_user_for_push"  : null,
            "in-app" or "inapp"     => string.IsNullOrWhiteSpace(r.UserId) ? "no_user_for_inapp" : null,
            _                       => null,
        };
    }

    private async Task<Notification> PersistFanOutParentAsync(
        Guid tenantId, SubmitNotificationDto request, string recipientJson, FanOutSummary summary)
    {
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
        else { metaDict = new(); }

        var camelOpts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var summaryJson = JsonSerializer.Serialize(summary, camelOpts);
        metaDict["fanout"] = JsonSerializer.Deserialize<JsonElement>(summaryJson);

        var status =
            summary.TotalResolved == 0                               ? "blocked" :
            summary.SentCount     == summary.TotalResolved           ? "sent"    :
            summary.SentCount     == 0 && summary.FailedCount == 0   ? "blocked" :
            summary.SentCount     == 0                               ? "failed"  :
                                                                       "partial";

        var parent = new Notification
        {
            TenantId      = tenantId,
            Channel       = request.Channel,
            Status        = status,
            RecipientJson = recipientJson,
            MessageJson   = JsonSerializer.Serialize(request.Message),
            MetadataJson  = JsonSerializer.Serialize(metaDict),
            IdempotencyKey = request.IdempotencyKey,
            TemplateKey   = request.TemplateKey,
            BlockedByPolicy = status == "blocked",
            BlockedReasonCode = summary.TotalResolved == 0 ? "recipient_set_empty" : null,
            LastErrorMessage = status == "sent"
                ? null
                : $"fanout: resolved={summary.TotalResolved} sent={summary.SentCount} " +
                  $"failed={summary.FailedCount} blocked={summary.BlockedCount} skipped={summary.SkippedCount}",
            Severity = request.Severity,
            Category = request.Category ?? request.ProductKey ?? request.ProductType,
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
            Mode          = mode,
            RoleKey       = roleKey,
            OrgId         = orgId,
            Channel       = channel,
            TotalResolved = totalResolved,
            SentCount     = sent,
            FailedCount   = failed,
            BlockedCount  = blocked,
            SkippedCount  = skipped,
            DeliveredByChannel = sent > 0 ? new Dictionary<string, int> { [channel] = sent } : new(),
            SkippedByReason = skippedByReason,
            BlockedByReason = blockedByReason,
            Recipients    = perRecipient,
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
        if (!string.IsNullOrEmpty(r.Phone))  dict["phone"]  = r.Phone;
        if (!string.IsNullOrEmpty(r.OrgId))  dict["orgId"]  = r.OrgId;

        return new SubmitNotificationDto
        {
            Channel        = src.Channel,
            Recipient      = dict,
            Message        = src.Message,
            Metadata       = src.Metadata,
            IdempotencyKey = string.IsNullOrEmpty(src.IdempotencyKey)
                ? null : $"{src.IdempotencyKey}:{r.StableKey}",
            TemplateKey    = src.TemplateKey,
            TemplateData   = src.TemplateData,
            ProductType    = src.ProductType,
            ProductKey     = src.ProductKey,
            EventKey       = src.EventKey,
            SourceSystem   = src.SourceSystem,
            CorrelationId  = src.CorrelationId,
            RequestedBy    = src.RequestedBy,
            Priority       = src.Priority,
            BrandedRendering  = src.BrandedRendering,
            OverrideSuppression = src.OverrideSuppression,
            OverrideReason = src.OverrideReason,
            Severity       = src.Severity,
            Category       = src.Category,
        };
    }

    // ─── Fan-out nested types ─────────────────────────────────────────────────

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

    // ─── Mappers ──────────────────────────────────────────────────────────────

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
            if (channel == "sms")   return doc.RootElement.TryGetProperty("phone", out var p) ? p.GetString() : null;
            return null;
        }
        catch { return null; }
    }
}
