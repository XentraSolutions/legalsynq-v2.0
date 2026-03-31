using BuildingBlocks.Exceptions;
using CareConnect.Application.DTOs;
using CareConnect.Application.Interfaces;
using CareConnect.Application.Repositories;
using CareConnect.Domain;
using LegalSynq.AuditClient;
using LegalSynq.AuditClient.DTOs;
using LegalSynq.AuditClient.Enums;
using AuditVisibility = LegalSynq.AuditClient.Enums.VisibilityScope;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CareConnect.Application.Services;

public class ReferralService : IReferralService
{
    private readonly IReferralRepository _referrals;
    private readonly IProviderRepository _providers;
    private readonly INotificationService _notifications;
    private readonly INotificationRepository _notificationRepo;
    private readonly IReferralEmailService _emailService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOrganizationRelationshipResolver _relationshipResolver;
    private readonly IAuditEventClient _auditClient;
    private readonly ILogger<ReferralService> _logger;

    public ReferralService(
        IReferralRepository referrals,
        IProviderRepository providers,
        INotificationService notifications,
        INotificationRepository notificationRepo,
        IReferralEmailService emailService,
        IServiceScopeFactory scopeFactory,
        IOrganizationRelationshipResolver relationshipResolver,
        IAuditEventClient auditClient,
        ILogger<ReferralService> logger)
    {
        _referrals            = referrals;
        _providers            = providers;
        _notifications        = notifications;
        _notificationRepo     = notificationRepo;
        _emailService         = emailService;
        _scopeFactory         = scopeFactory;
        _relationshipResolver = relationshipResolver;
        _auditClient          = auditClient;
        _logger               = logger;
    }

    public async Task<PagedResponse<ReferralResponse>> SearchAsync(Guid tenantId, GetReferralsQuery query, CancellationToken ct = default)
    {
        ValidateQuery(query);

        var (items, totalCount) = await _referrals.SearchAsync(tenantId, query, ct);

        return new PagedResponse<ReferralResponse>
        {
            Items = items.Select(r => ToResponse(r)).ToList(),
            Page = query.Page,
            PageSize = query.PageSize,
            TotalCount = totalCount
        };
    }

    public async Task<ReferralResponse> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var referral = await _referrals.GetByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Referral '{id}' was not found.");

        // LSCC-005-01: load latest notification for email status display
        var latestNotif = await _notificationRepo.GetLatestByReferralAsync(tenantId, id, ct: ct);
        return ToResponse(referral, latestNotif);
    }

    public async Task<ReferralResponse> CreateAsync(Guid tenantId, Guid? userId, CreateReferralRequest request, CancellationToken ct = default)
    {
        ValidateCreate(request);

        // Providers are a platform-wide marketplace (cross-tenant discoverable).
        // Use GetByIdCrossAsync so a referral can target any active provider regardless
        // of whether their TenantId matches the referrer's tenant.
        var provider = await _providers.GetByIdCrossAsync(request.ProviderId, ct)
            ?? throw new NotFoundException($"Provider '{request.ProviderId}' was not found.");

        // Phase C: resolve the Identity OrganizationRelationship when both org IDs are provided.
        // The null resolver (default) always returns null — no runtime side-effects.
        Guid? orgRelationshipId = null;
        if (request.ReferringOrganizationId.HasValue && request.ReceivingOrganizationId.HasValue)
        {
            orgRelationshipId = await _relationshipResolver.FindActiveRelationshipAsync(
                request.ReferringOrganizationId.Value,
                request.ReceivingOrganizationId.Value,
                ct);

            // Phase H: log when both org IDs were supplied but no active relationship was resolved.
            // This indicates either a missing OrganizationRelationship record in Identity or a
            // wrong org ID pair — the referral will still be created but without relationship linkage.
            if (orgRelationshipId is null)
                _logger.LogWarning(
                    "Referral org-relationship resolution: no active OrganizationRelationship found " +
                    "between ReferringOrg={ReferringOrgId} and ReceivingOrg={ReceivingOrgId}. " +
                    "Referral will be created without OrganizationRelationshipId.",
                    request.ReferringOrganizationId.Value,
                    request.ReceivingOrganizationId.Value);
        }

        var referral = Referral.Create(
            tenantId,
            referringOrganizationId: request.ReferringOrganizationId,
            receivingOrganizationId: request.ReceivingOrganizationId,
            provider.Id,
            subjectPartyId: null,       // null = using inline fields (backward compat)
            subjectNameSnapshot: null,
            subjectDobSnapshot: null,
            request.ClientFirstName,
            request.ClientLastName,
            request.ClientDob,
            request.ClientPhone ?? string.Empty,
            request.ClientEmail ?? string.Empty,
            request.CaseNumber,
            request.RequestedService,
            request.Urgency,
            request.Notes,
            userId,
            organizationRelationshipId: orgRelationshipId,
            referrerEmail: request.ReferrerEmail,
            referrerName:  request.ReferrerName);

        await _referrals.AddAsync(referral, ct);

        // LSCC-005: Fire provider email notification (fire-and-observe — never gates creation).
        // A fresh DI scope is created so the background task gets its own DbContext instance,
        // avoiding a concurrent-access conflict with the request-scoped context still in use below.
        var scopeFactory = _scopeFactory;
        var logger       = _logger;
        _ = Task.Run(async () =>
        {
            using var scope       = scopeFactory.CreateScope();
            var       emailSvc    = scope.ServiceProvider.GetRequiredService<IReferralEmailService>();
            try { await emailSvc.SendNewReferralNotificationAsync(referral, provider, CancellationToken.None); }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Background referral notification failed for referral {ReferralId}.", referral.Id);
            }
        });

        // Canonical audit: careconnect.referral.created — fire-and-observe, never gates creation.
        var now = DateTimeOffset.UtcNow;
        _ = _auditClient.IngestAsync(new IngestAuditEventRequest
        {
            EventType     = "careconnect.referral.created",
            EventCategory = EventCategory.Business,
            SourceSystem  = "care-connect",
            SourceService = "referral-api",
            Visibility    = AuditVisibility.Tenant,
            Severity      = SeverityLevel.Info,
            OccurredAtUtc = now,
            Scope = new AuditEventScopeDto
            {
                ScopeType      = ScopeType.Tenant,
                TenantId       = tenantId.ToString(),
                OrganizationId = request.ReferringOrganizationId?.ToString(),
            },
            Actor = new AuditEventActorDto
            {
                Id   = userId?.ToString(),
                Type = ActorType.User,
                Name = userId?.ToString() ?? "(system)",
            },
            Entity      = new AuditEventEntityDto { Type = "Referral", Id = referral.Id.ToString() },
            Action      = "ReferralCreated",
            Description = $"Referral created for '{request.ClientFirstName} {request.ClientLastName}' requesting '{request.RequestedService}'.",
            Outcome     = "success",
            Metadata    = JsonSerializer.Serialize(new
            {
                referralId            = referral.Id,
                tenantId,
                providerId            = request.ProviderId,
                requestedService      = request.RequestedService,
                urgency               = request.Urgency,
                referringOrganizationId = request.ReferringOrganizationId,
                receivingOrganizationId = request.ReceivingOrganizationId,
            }),
            IdempotencyKey = IdempotencyKey.For("care-connect", "careconnect.referral.created", referral.Id.ToString()),
            Tags = ["referral", "created"],
        });

        var loaded = await _referrals.GetByIdAsync(tenantId, referral.Id, ct);
        return ToResponse(loaded!);
    }

    public async Task<ReferralResponse> UpdateAsync(Guid tenantId, Guid id, Guid? userId, UpdateReferralRequest request, CancellationToken ct = default)
    {
        var referral = await _referrals.GetByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Referral '{id}' was not found.");

        ValidateUpdate(request);

        ReferralStatusHistory? history = null;

        if (referral.Status != request.Status)
        {
            ReferralWorkflowRules.ValidateTransition(referral.Status, request.Status);

            history = ReferralStatusHistory.Create(
                referral.Id,
                tenantId,
                referral.Status,
                request.Status,
                userId,
                request.Notes);
        }

        bool statusChanged = history is not null;

        referral.Update(request.RequestedService, request.Urgency, request.Status, request.Notes, userId);
        await _referrals.UpdateAsync(referral, history, ct);

        // Fire notification hook after successful save — non-blocking on failure.
        if (statusChanged)
        {
            try { await _notifications.CreateReferralStatusChangedAsync(tenantId, referral.Id, userId, ct); }
            catch { /* Notification failure must not break the referral update. */ }
        }

        // Canonical audit: careconnect.referral.updated — fire-and-observe.
        var auditNow = DateTimeOffset.UtcNow;
        _ = _auditClient.IngestAsync(new IngestAuditEventRequest
        {
            EventType     = "careconnect.referral.updated",
            EventCategory = EventCategory.Business,
            SourceSystem  = "care-connect",
            SourceService = "referral-service",
            Visibility    = AuditVisibility.Tenant,
            Severity      = SeverityLevel.Info,
            OccurredAtUtc = auditNow,
            Scope = new AuditEventScopeDto { ScopeType = ScopeType.Tenant, TenantId = tenantId.ToString() },
            Actor = new AuditEventActorDto
            {
                Id   = userId?.ToString(),
                Type = userId.HasValue ? ActorType.User : ActorType.System,
            },
            Entity = new AuditEventEntityDto { Type = "Referral", Id = referral.Id.ToString() },
            Action      = statusChanged ? "ReferralStatusChanged" : "ReferralUpdated",
            Description = statusChanged
                ? $"Referral {referral.Id} status changed to '{request.Status}'."
                : $"Referral {referral.Id} updated.",
            After       = JsonSerializer.Serialize(new
            {
                status = request.Status,
                requestedService = request.RequestedService,
                urgency = request.Urgency,
                statusChanged,
            }),
            IdempotencyKey = IdempotencyKey.ForWithTimestamp(auditNow, "care-connect", "careconnect.referral.updated", referral.Id.ToString()),
            Tags = ["referral", "clinical", "status-change"],
        });

        var loaded = await _referrals.GetByIdAsync(tenantId, referral.Id, ct);
        return ToResponse(loaded!);
    }

    public async Task<List<ReferralStatusHistoryResponse>> GetHistoryAsync(Guid tenantId, Guid referralId, CancellationToken ct = default)
    {
        _ = await _referrals.GetByIdAsync(tenantId, referralId, ct)
            ?? throw new NotFoundException($"Referral '{referralId}' was not found.");

        var history = await _referrals.GetHistoryByReferralAsync(tenantId, referralId, ct);
        return history.Select(ToHistoryResponse).ToList();
    }

    // ── LSCC-005: Public token-based methods ─────────────────────────────────

    public async Task<ReferralViewTokenRouteResponse> ResolveViewTokenAsync(
        string token,
        CancellationToken ct = default)
    {
        var tokenResult = _emailService.ValidateViewToken(token);
        if (tokenResult is null)
        {
            // Emit audit for invalid/malformed token access
            EmitInvalidTokenAudit(token, "malformed-or-expired", null);
            return new ReferralViewTokenRouteResponse { RouteType = "invalid" };
        }

        var referral = await _referrals.GetByIdGlobalAsync(tokenResult.ReferralId, ct);
        if (referral is null)
        {
            EmitInvalidTokenAudit(token, "referral-not-found", tokenResult.ReferralId);
            return new ReferralViewTokenRouteResponse { RouteType = "notfound" };
        }

        // LSCC-005-01: Version check — rejects revoked tokens
        if (tokenResult.TokenVersion != referral.TokenVersion)
        {
            _logger.LogWarning(
                "Referral view token version mismatch for referral {ReferralId}: " +
                "token has version {TokenVersion}, referral has version {ReferralVersion}. Token revoked.",
                referral.Id, tokenResult.TokenVersion, referral.TokenVersion);
            EmitInvalidTokenAudit(token, "revoked", tokenResult.ReferralId);
            return new ReferralViewTokenRouteResponse { RouteType = "invalid" };
        }

        var provider = referral.Provider;
        if (provider is null)
        {
            _logger.LogWarning(
                "Referral {ReferralId} has no Provider navigation — cannot resolve view token route.",
                referral.Id);
            return new ReferralViewTokenRouteResponse { RouteType = "invalid" };
        }

        // OrganizationId is null → provider has no Identity org link → pending provider flow
        var routeType = provider.OrganizationId.HasValue ? "active" : "pending";
        return new ReferralViewTokenRouteResponse
        {
            RouteType  = routeType,
            ReferralId = referral.Id,
        };
    }

    public async Task<ReferralResponse> AcceptByTokenAsync(
        Guid referralId,
        string token,
        CancellationToken ct = default)
    {
        var tokenResult = _emailService.ValidateViewToken(token);
        if (tokenResult is null)
        {
            EmitInvalidTokenAudit(token, "malformed-or-expired", null);
            throw new UnauthorizedAccessException("Invalid or expired view token.");
        }

        if (tokenResult.ReferralId != referralId)
            throw new UnauthorizedAccessException("Token does not match the requested referral.");

        var referral = await _referrals.GetByIdGlobalAsync(referralId, ct)
            ?? throw new NotFoundException($"Referral '{referralId}' was not found.");

        // LSCC-005-01: Version check — rejects revoked tokens
        if (tokenResult.TokenVersion != referral.TokenVersion)
        {
            _logger.LogWarning(
                "Acceptance attempt with revoked token for referral {ReferralId} " +
                "(token version {TokenVersion}, referral version {ReferralVersion}).",
                referral.Id, tokenResult.TokenVersion, referral.TokenVersion);
            EmitInvalidTokenAudit(token, "revoked", referralId);
            throw new UnauthorizedAccessException("This referral link has been revoked. Please contact the referring party for a new link.");
        }

        // LSCC-005-01: Duplicate/replay hardening — status check prevents double-acceptance
        if (referral.Status != Referral.ValidStatuses.New)
        {
            _logger.LogInformation(
                "Replay acceptance attempt for referral {ReferralId}: already in status {Status}.",
                referral.Id, referral.Status);
            // Emit an audit event for the replay attempt
            var replayNow = DateTimeOffset.UtcNow;
            _ = _auditClient.IngestAsync(new IngestAuditEventRequest
            {
                EventType     = "careconnect.referral.accept.replay",
                EventCategory = EventCategory.Security,
                SourceSystem  = "care-connect",
                SourceService = "referral-api",
                Visibility    = AuditVisibility.Tenant,
                Severity      = SeverityLevel.Warn,
                OccurredAtUtc = replayNow,
                Scope         = new AuditEventScopeDto { ScopeType = ScopeType.Tenant, TenantId = referral.TenantId.ToString() },
                Actor         = new AuditEventActorDto { Id = "public-token", Type = ActorType.System },
                Entity        = new AuditEventEntityDto { Type = "Referral", Id = referral.Id.ToString() },
                Action        = "ReferralAcceptReplay",
                Description   = $"Duplicate acceptance attempt for referral '{referral.Id}' (status: '{referral.Status}'). Rejected.",
                Outcome       = "failure",
                IdempotencyKey = IdempotencyKey.ForWithTimestamp(replayNow, "care-connect", "careconnect.referral.accept.replay", referral.Id.ToString()),
                Tags           = ["referral", "replay", "security"],
            });
            throw new InvalidOperationException(
                $"Referral is not in Pending (New) status — current status: '{referral.Status}'.");
        }

        var provider = referral.Provider
            ?? throw new InvalidOperationException($"Provider data missing for referral '{referralId}'.");

        var history = ReferralStatusHistory.Create(
            referral.Id,
            referral.TenantId,
            referral.Status,
            Referral.ValidStatuses.Accepted,
            changedByUserId: null,
            notes: "Accepted via public token link.");

        referral.Accept(updatedByUserId: null);
        await _referrals.UpdateAsync(referral, history, ct);

        // LSCC-005 / LSCC-005-01: Fire confirmation emails (fire-and-observe — never gates acceptance).
        // Fresh scope so the background DbContext is isolated from the request scope used below.
        // Duplicate confirmation prevention: acceptance status is already committed, so any
        // concurrent/replay requests will hit the status != New guard above before reaching this point.
        var scopeFactory2 = _scopeFactory;
        var logger2       = _logger;
        _ = Task.Run(async () =>
        {
            using var scope2   = scopeFactory2.CreateScope();
            var       emailSvc = scope2.ServiceProvider.GetRequiredService<IReferralEmailService>();
            try { await emailSvc.SendAcceptanceConfirmationsAsync(referral, provider, CancellationToken.None); }
            catch (Exception ex)
            {
                logger2.LogWarning(ex,
                    "Background acceptance confirmation failed for referral {ReferralId}.", referral.Id);
            }
        });

        // Canonical audit: token-based acceptance — fire-and-observe.
        var now = DateTimeOffset.UtcNow;
        _ = _auditClient.IngestAsync(new IngestAuditEventRequest
        {
            EventType     = "careconnect.referral.accepted.by-token",
            EventCategory = EventCategory.Business,
            SourceSystem  = "care-connect",
            SourceService = "referral-api",
            Visibility    = AuditVisibility.Tenant,
            Severity      = SeverityLevel.Info,
            OccurredAtUtc = now,
            Scope         = new AuditEventScopeDto
            {
                ScopeType = ScopeType.Tenant,
                TenantId  = referral.TenantId.ToString(),
            },
            Actor = new AuditEventActorDto
            {
                Id   = "public-token",
                Type = ActorType.System,
                Name = "PublicTokenAccept",
            },
            Entity         = new AuditEventEntityDto { Type = "Referral", Id = referral.Id.ToString() },
            Action         = "ReferralAcceptedByToken",
            Description    = $"Referral '{referral.Id}' accepted via public view token.",
            Outcome        = "success",
            IdempotencyKey = IdempotencyKey.ForWithTimestamp(now, "care-connect", "careconnect.referral.accepted.by-token", referral.Id.ToString()),
            Tags           = ["referral", "accepted", "public-token"],
        });

        var loaded = await _referrals.GetByIdGlobalAsync(referral.Id, ct);
        return ToResponse(loaded!);
    }

    // ── LSCC-005-01: Hardening methods ───────────────────────────────────────

    /// <summary>
    /// Resends the provider notification email for a referral.
    /// Only available when the referral is still in New status.
    /// Creates a fresh notification record (ReferralEmailResent) with the current token version.
    /// </summary>
    public async Task<ReferralResponse> ResendEmailAsync(Guid tenantId, Guid referralId, CancellationToken ct = default)
    {
        var referral = await _referrals.GetByIdAsync(tenantId, referralId, ct)
            ?? throw new NotFoundException($"Referral '{referralId}' was not found.");

        if (referral.Status != Referral.ValidStatuses.New)
            throw new InvalidOperationException(
                $"Cannot resend provider notification: referral is already '{referral.Status}'. " +
                $"The provider has already actioned this referral.");

        var provider = referral.Provider
            ?? throw new InvalidOperationException($"Provider data missing for referral '{referralId}'.");

        // LSCC-005-02: capture any existing failed notification with an active retry schedule
        // so we can clear it if the manual resend succeeds (retry no longer needed).
        var existingFailedNotif = await _notificationRepo.GetLatestByReferralAsync(
            tenantId, referralId,
            notificationType: NotificationType.ReferralCreated,
            ct: ct);
        bool hadRetryScheduled = existingFailedNotif is { Status: "Failed", NextRetryAfterUtc: not null };

        // Resend is synchronous — caller expects immediate success/failure feedback.
        await _emailService.ResendNewReferralNotificationAsync(referral, provider, ct);

        // LSCC-005-02: if the resend succeeded, clear any pending auto-retry on the original
        // failed notification so the worker does not double-send.
        if (hadRetryScheduled)
        {
            // Fetch the notification just created by the resend to check its status.
            var newNotif = await _notificationRepo.GetLatestByReferralAsync(
                tenantId, referralId,
                notificationType: NotificationType.ReferralEmailResent,
                ct: ct);

            if (newNotif is { Status: "Sent" })
            {
                existingFailedNotif!.ClearRetrySchedule();
                await _notificationRepo.UpdateAsync(existingFailedNotif, ct);
                _logger.LogInformation(
                    "ResendEmailAsync: cleared auto-retry schedule on notification {OriginalId} " +
                    "because manual resend {NewId} succeeded for referral {ReferralId}.",
                    existingFailedNotif.Id, newNotif.Id, referralId);
            }
        }

        // Audit: email resend
        var now = DateTimeOffset.UtcNow;
        _ = _auditClient.IngestAsync(new IngestAuditEventRequest
        {
            EventType     = "careconnect.referral.email.resent",
            EventCategory = EventCategory.Business,
            SourceSystem  = "care-connect",
            SourceService = "referral-api",
            Visibility    = AuditVisibility.Tenant,
            Severity      = SeverityLevel.Info,
            OccurredAtUtc = now,
            Scope         = new AuditEventScopeDto { ScopeType = ScopeType.Tenant, TenantId = tenantId.ToString() },
            Actor         = new AuditEventActorDto { Type = ActorType.User },
            Entity        = new AuditEventEntityDto { Type = "Referral", Id = referral.Id.ToString() },
            Action        = "ReferralEmailResent",
            Description   = $"Provider notification email resent for referral '{referral.Id}'.",
            Outcome       = "success",
            IdempotencyKey = IdempotencyKey.ForWithTimestamp(now, "care-connect", "careconnect.referral.email.resent", referral.Id.ToString()),
            Tags           = ["referral", "email", "resent"],
        });

        var latest = await _notificationRepo.GetLatestByReferralAsync(tenantId, referralId, ct: ct);
        return ToResponse(referral, latest);
    }

    /// <summary>
    /// Revokes all previously issued view tokens for a referral by incrementing TokenVersion.
    /// Any token with an older version will be rejected as revoked.
    /// Newly generated tokens (e.g. from resend) will use the new version and will work.
    /// </summary>
    public async Task<ReferralResponse> RevokeTokenAsync(Guid tenantId, Guid referralId, CancellationToken ct = default)
    {
        var referral = await _referrals.GetByIdAsync(tenantId, referralId, ct)
            ?? throw new NotFoundException($"Referral '{referralId}' was not found.");

        var oldVersion = referral.TokenVersion;
        referral.IncrementTokenVersion();
        await _referrals.UpdateAsync(referral, null, ct);

        _logger.LogInformation(
            "Referral {ReferralId} token revoked: version {OldVersion} → {NewVersion}.",
            referral.Id, oldVersion, referral.TokenVersion);

        // Audit: token revocation
        var now = DateTimeOffset.UtcNow;
        _ = _auditClient.IngestAsync(new IngestAuditEventRequest
        {
            EventType     = "careconnect.referral.token.revoked",
            EventCategory = EventCategory.Security,
            SourceSystem  = "care-connect",
            SourceService = "referral-api",
            Visibility    = AuditVisibility.Tenant,
            Severity      = SeverityLevel.Warn,
            OccurredAtUtc = now,
            Scope         = new AuditEventScopeDto { ScopeType = ScopeType.Tenant, TenantId = tenantId.ToString() },
            Actor         = new AuditEventActorDto { Type = ActorType.User },
            Entity        = new AuditEventEntityDto { Type = "Referral", Id = referral.Id.ToString() },
            Action        = "ReferralTokenRevoked",
            Description   = $"View token revoked for referral '{referral.Id}'. Version {oldVersion} → {referral.TokenVersion}.",
            Outcome       = "success",
            IdempotencyKey = IdempotencyKey.ForWithTimestamp(now, "care-connect", "careconnect.referral.token.revoked", referral.Id.ToString()),
            Tags           = ["referral", "token", "revoked", "security"],
        });

        var latest = await _notificationRepo.GetLatestByReferralAsync(tenantId, referralId, ct: ct);
        return ToResponse(referral, latest);
    }

    /// <summary>
    /// Returns the notification history for a referral (email delivery records).
    /// </summary>
    public async Task<List<ReferralNotificationResponse>> GetNotificationsAsync(Guid tenantId, Guid referralId, CancellationToken ct = default)
    {
        _ = await _referrals.GetByIdAsync(tenantId, referralId, ct)
            ?? throw new NotFoundException($"Referral '{referralId}' was not found.");

        var notifs = await _notificationRepo.GetAllByReferralAsync(tenantId, referralId, ct);
        return notifs.Select(ToNotificationResponse).ToList();
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void EmitInvalidTokenAudit(string token, string reason, Guid? referralId)
    {
        var now = DateTimeOffset.UtcNow;
        _ = _auditClient.IngestAsync(new IngestAuditEventRequest
        {
            EventType     = "careconnect.referral.token.invalid",
            EventCategory = EventCategory.Security,
            SourceSystem  = "care-connect",
            SourceService = "referral-api",
            Visibility    = AuditVisibility.Tenant,
            Severity      = SeverityLevel.Warn,
            OccurredAtUtc = now,
            Scope         = new AuditEventScopeDto { ScopeType = ScopeType.Platform },
            Actor         = new AuditEventActorDto { Id = "anonymous", Type = ActorType.System },
            Entity        = new AuditEventEntityDto { Type = "Referral", Id = referralId?.ToString() ?? "unknown" },
            Action        = "TokenInvalid",
            Description   = $"Invalid referral view token presented. Reason: {reason}.",
            Outcome       = "failure",
            IdempotencyKey = IdempotencyKey.ForWithTimestamp(now, "care-connect", "careconnect.referral.token.invalid", referralId?.ToString() ?? "unknown"),
            Tags           = ["referral", "token", "invalid", "security"],
        });
    }

    private static void ValidateQuery(GetReferralsQuery q)
    {
        var errors = new Dictionary<string, string[]>();

        if (q.Page < 1)
            errors["page"] = new[] { "Page must be >= 1." };

        if (q.PageSize < 1)
            errors["pageSize"] = new[] { "PageSize must be >= 1." };
        else if (q.PageSize > 100)
            errors["pageSize"] = new[] { "PageSize must be <= 100." };

        if (errors.Count > 0)
            throw new ValidationException("One or more validation errors occurred.", errors);
    }

    private static void ValidateCreate(CreateReferralRequest r)
    {
        var errors = new Dictionary<string, string[]>();

        if (r.ProviderId == Guid.Empty)
            errors["providerId"] = new[] { "ProviderId is required." };

        if (string.IsNullOrWhiteSpace(r.ClientFirstName))
            errors["clientFirstName"] = new[] { "ClientFirstName is required." };

        if (string.IsNullOrWhiteSpace(r.ClientLastName))
            errors["clientLastName"] = new[] { "ClientLastName is required." };

        if (string.IsNullOrWhiteSpace(r.ClientPhone))
            errors["clientPhone"] = new[] { "ClientPhone is required." };

        if (string.IsNullOrWhiteSpace(r.ClientEmail))
            errors["clientEmail"] = new[] { "ClientEmail is required." };
        else if (!Regex.IsMatch(r.ClientEmail.Trim(), @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            errors["clientEmail"] = new[] { "ClientEmail format is invalid." };

        if (string.IsNullOrWhiteSpace(r.RequestedService))
            errors["requestedService"] = new[] { "RequestedService is required." };

        if (!Referral.ValidUrgencies.All.Contains(r.Urgency))
            errors["urgency"] = new[] { $"Urgency must be one of: {string.Join(", ", Referral.ValidUrgencies.All)}." };

        if (errors.Count > 0)
            throw new ValidationException("One or more validation errors occurred.", errors);
    }

    private static void ValidateUpdate(UpdateReferralRequest r)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(r.RequestedService))
            errors["requestedService"] = new[] { "RequestedService is required." };

        if (!Referral.ValidUrgencies.All.Contains(r.Urgency))
            errors["urgency"] = new[] { $"Urgency must be one of: {string.Join(", ", Referral.ValidUrgencies.All)}." };

        if (!Referral.ValidStatuses.All.Contains(r.Status))
            errors["status"] = new[] { $"Status must be one of: {string.Join(", ", Referral.ValidStatuses.All)}." };

        if (errors.Count > 0)
            throw new ValidationException("One or more validation errors occurred.", errors);
    }

    private static ReferralResponse ToResponse(Referral r, CareConnectNotification? latestNotif = null) => new()
    {
        Id = r.Id,
        TenantId = r.TenantId,
        ProviderId = r.ProviderId,
        ProviderName = r.Provider?.Name ?? string.Empty,
        ClientFirstName = r.ClientFirstName,
        ClientLastName = r.ClientLastName,
        ClientDob = r.ClientDob,
        ClientPhone = r.ClientPhone,
        ClientEmail = r.ClientEmail,
        CaseNumber = r.CaseNumber,
        RequestedService = r.RequestedService,
        Urgency = r.Urgency,
        Status = r.Status,
        Notes = r.Notes,
        CreatedAtUtc = r.CreatedAtUtc,
        UpdatedAtUtc = r.UpdatedAtUtc,
        // Phase 5: expose org context fields resolved at creation time
        ReferringOrganizationId = r.ReferringOrganizationId,
        ReceivingOrganizationId = r.ReceivingOrganizationId,
        OrganizationRelationshipId = r.OrganizationRelationshipId,
        // LSCC-005-01: hardening fields
        TokenVersion          = r.TokenVersion,
        ProviderEmailStatus   = latestNotif?.Status,
        ProviderEmailAttempts = latestNotif?.AttemptCount ?? 0,
        ProviderEmailFailureReason = latestNotif?.FailureReason,
    };

    private static ReferralStatusHistoryResponse ToHistoryResponse(ReferralStatusHistory h) => new()
    {
        Id = h.Id,
        ReferralId = h.ReferralId,
        OldStatus = h.OldStatus,
        NewStatus = h.NewStatus,
        ChangedByUserId = h.ChangedByUserId,
        ChangedAtUtc = h.ChangedAtUtc,
        Notes = h.Notes
    };

    private static ReferralNotificationResponse ToNotificationResponse(CareConnectNotification n) => new()
    {
        Id               = n.Id,
        NotificationType = n.NotificationType,
        RecipientType    = n.RecipientType,
        RecipientAddress = n.RecipientAddress,
        Status           = n.Status,
        AttemptCount     = n.AttemptCount,
        FailureReason    = n.FailureReason,
        SentAtUtc        = n.SentAtUtc,
        FailedAtUtc      = n.FailedAtUtc,
        LastAttemptAtUtc = n.LastAttemptAtUtc,
        CreatedAtUtc     = n.CreatedAtUtc,
        // LSCC-005-02: retry lifecycle fields
        TriggerSource     = n.TriggerSource,
        NextRetryAfterUtc = n.NextRetryAfterUtc,
        DerivedStatus     = ReferralRetryPolicy.GetDerivedStatus(n),
    };

    // LSCC-005-02: Audit timeline
    public async Task<List<ReferralAuditEventResponse>> GetAuditTimelineAsync(
        Guid tenantId,
        Guid referralId,
        CancellationToken ct = default)
    {
        _ = await _referrals.GetByIdAsync(tenantId, referralId, ct)
            ?? throw new NotFoundException($"Referral '{referralId}' was not found.");

        var events = new List<ReferralAuditEventResponse>();

        // ── Source 1: status history ───────────────────────────────────────
        var history = await _referrals.GetHistoryByReferralAsync(tenantId, referralId, ct);
        foreach (var h in history)
        {
            var (label, category) = h.NewStatus switch
            {
                "New"       => ("Referral Created",       "info"),
                "Accepted"  => ("Referral Accepted",      "success"),
                "Declined"  => ("Referral Declined",      "error"),
                "Cancelled" => ("Referral Cancelled",     "warning"),
                "Completed" => ("Referral Completed",     "success"),
                "Scheduled" => ("Appointment Scheduled",  "success"),
                _           => ($"Status → {h.NewStatus}", "info"),
            };
            events.Add(new ReferralAuditEventResponse
            {
                EventType  = $"referral.status.{h.NewStatus.ToLowerInvariant()}",
                Label      = label,
                OccurredAt = h.ChangedAtUtc,
                Detail     = h.Notes,
                Category   = category,
            });
        }

        // ── Source 2: notification records ────────────────────────────────
        var notifications = await _notificationRepo.GetAllByReferralAsync(tenantId, referralId, ct);
        foreach (var n in notifications)
        {
            var derived = ReferralRetryPolicy.GetDerivedStatus(n);
            var sourceLabel = n.TriggerSource switch
            {
                NotificationSource.ManualResend => "Manual Resend",
                NotificationSource.AutoRetry    => "Auto-Retry",
                _                               => "Notification",
            };

            var typeLabel = n.NotificationType switch
            {
                NotificationType.ReferralCreated          => "Provider Notification",
                NotificationType.ReferralEmailResent      => "Provider Notification (Resent)",
                NotificationType.ReferralEmailAutoRetry   => "Provider Notification (Auto-Retry)",
                NotificationType.ReferralAcceptedProvider => "Provider Acceptance Confirmation",
                NotificationType.ReferralAcceptedReferrer => "Referrer Acceptance Confirmation",
                _                                         => n.NotificationType,
            };

            var (statusLabel, category) = derived switch
            {
                "Sent"           => ("Sent",             "success"),
                "Failed"         => ("Failed",           "error"),
                "Retrying"       => ("Retrying",         "warning"),
                "RetryExhausted" => ("Retry Exhausted",  "error"),
                _                => (derived,             "info"),
            };

            var detail = derived switch
            {
                "Failed" or "RetryExhausted" when n.FailureReason is { Length: > 0 }
                    => $"Attempt {n.AttemptCount}: {TruncateReason(n.FailureReason, 120)}",
                "Retrying" when n.NextRetryAfterUtc.HasValue
                    => $"Attempt {n.AttemptCount} failed. Next retry after {n.NextRetryAfterUtc:HH:mm 'UTC'}.",
                "Sent"
                    => $"Attempt {n.AttemptCount} succeeded.",
                _ => n.AttemptCount > 1 ? $"Attempt {n.AttemptCount}" : null,
            };

            events.Add(new ReferralAuditEventResponse
            {
                EventType  = $"notification.{n.NotificationType.ToLowerInvariant()}.{derived.ToLowerInvariant()}",
                Label      = $"{typeLabel} — {statusLabel}",
                OccurredAt = n.LastAttemptAtUtc ?? n.CreatedAtUtc,
                Detail     = detail,
                Category   = category,
            });
        }

        return events.OrderBy(e => e.OccurredAt).ToList();
    }

    private static string TruncateReason(string reason, int maxLength)
        => reason.Length <= maxLength ? reason : reason[..maxLength] + "…";
}
