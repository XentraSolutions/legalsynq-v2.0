using Support.Api.Audit;
using Support.Api.Auth;
using Support.Api.Data;
using Support.Api.Domain;
using Support.Api.Dtos;
using Support.Api.Notifications;
using Support.Api.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Support.Api.Services;

public class TimelineQuery
{
    public CommentVisibility? Visibility { get; set; }
    public CommentType? CommentType { get; set; }
}

public interface ICommentService
{
    Task<CommentResponse> AddAsync(Guid ticketId, CreateCommentRequest req, CancellationToken ct = default);
    Task<List<CommentResponse>> ListAsync(Guid ticketId, CommentVisibility? visibility, CommentType? commentType, CancellationToken ct = default);
    Task<List<TimelineItem>> TimelineAsync(Guid ticketId, CancellationToken ct = default);

    /// <summary>
    /// Customer-safe comment — verifies tenantId + externalCustomerId + VisibilityScope=CustomerVisible
    /// before creating the comment. Always creates as CommentType=CustomerReply, Visibility=CustomerVisible.
    /// Throws TicketNotFoundException if any ownership constraint fails.
    /// Used exclusively by the CustomerAccess-protected endpoints.
    /// </summary>
    Task<CommentResponse> AddCustomerCommentAsync(
        string tenantId,
        Guid externalCustomerId,
        Guid ticketId,
        string body,
        string? authorEmail = null,
        string? authorName = null,
        CancellationToken ct = default);
}

public class CommentService : ICommentService
{
    private readonly SupportDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IEventLogger _events;
    private readonly ILogger<CommentService> _log;
    private readonly INotificationPublisher _notifications;
    private readonly IAuditPublisher _audit;
    private readonly IActorAccessor _actor;
    private readonly IUserEmailResolver _emailResolver;

    public CommentService(SupportDbContext db, ITenantContext tenant, IEventLogger events,
        ILogger<CommentService> log, INotificationPublisher notifications,
        IAuditPublisher audit, IActorAccessor actor, IUserEmailResolver emailResolver)
    {
        _db = db;
        _tenant = tenant;
        _events = events;
        _log = log;
        _notifications = notifications;
        _audit = audit;
        _actor = actor;
        _emailResolver = emailResolver;
    }

    private string RequireTenant()
    {
        if (!_tenant.IsResolved) throw new TenantMissingException();
        return _tenant.TenantId!;
    }

    private bool IsPlatformAdmin =>
        _actor.Actor.Roles.Contains(SupportRoles.PlatformAdmin, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Resolves the ticket and returns its entity.
    /// - Tenant-scoped users: enforces tenant ownership (ticketId + tenantId).
    /// - PlatformAdmin: finds by ticketId alone (cross-tenant access).
    /// - Anyone else without a tenant claim: throws TenantMissingException.
    /// </summary>
    private async Task<SupportTicket> ResolveTicketAsync(Guid ticketId, CancellationToken ct)
    {
        SupportTicket? t;
        // PlatformAdmin check must come FIRST — the platform admin JWT carries a
        // synthetic tenant_id claim that makes _tenant.IsResolved=true. Without this
        // ordering, platform admins would be scoped to the system placeholder tenant.
        if (IsPlatformAdmin)
        {
            t = await _db.Tickets.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == ticketId, ct);
        }
        else if (_tenant.IsResolved)
        {
            t = await _db.Tickets.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == ticketId && x.TenantId == _tenant.TenantId, ct);
        }
        else
        {
            throw new TenantMissingException();
        }
        if (t is null) throw new TicketNotFoundException();
        return t;
    }

    private async Task<SupportTicket> RequireOwnedTicketAsync(Guid ticketId, string tenantId, CancellationToken ct)
    {
        var t = await _db.Tickets.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == ticketId && x.TenantId == tenantId, ct);
        if (t is null) throw new TicketNotFoundException();
        return t;
    }

    public async Task<CommentResponse> AddAsync(Guid ticketId, CreateCommentRequest req, CancellationToken ct = default)
    {
        var ticket = await ResolveTicketAsync(ticketId, ct);
        var tenantId = ticket.TenantId;

        var comment = new SupportTicketComment
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            TenantId = tenantId,
            CommentType = req.CommentType ?? CommentType.CustomerReply,
            Visibility = req.Visibility ?? CommentVisibility.CustomerVisible,
            Body = req.Body,
            AuthorUserId = req.AuthorUserId ?? _tenant.UserId,
            AuthorName = req.AuthorName,
            AuthorEmail = req.AuthorEmail,
            CreatedAt = DateTime.UtcNow,
        };
        _db.TicketComments.Add(comment);

        _events.Log(ticketId, tenantId, "comment_added", "Comment added",
            metadata: new { comment_id = comment.Id, visibility = comment.Visibility.ToString() },
            actorUserId: comment.AuthorUserId);

        await _db.SaveChangesAsync(ct);
        _log.LogInformation("Comment {CommentId} added to ticket {TicketId} tenant={TenantId}", comment.Id, ticketId, tenantId);

        await TryPublishCommentNotificationAsync(ticket, comment, ct);
        await TryPublishCommentAuditAsync(ticket, comment, ct);

        return CommentResponse.From(comment);
    }

    private async Task TryPublishCommentAuditAsync(SupportTicket ticket, SupportTicketComment comment, CancellationToken ct)
    {
        try
        {
            var actor = _actor.Actor;
            var req = _actor.Request;
            // Body length only — never include body content in audit metadata.
            var bodyLen = comment.Body?.Length ?? 0;
            var evt = new SupportAuditEvent(
                EventType: SupportAuditEventTypes.TicketCommentAdded,
                TenantId: ticket.TenantId,
                ActorUserId: actor.UserId ?? comment.AuthorUserId,
                ActorEmail: actor.Email ?? comment.AuthorEmail,
                ActorRoles: actor.Roles,
                ResourceType: SupportAuditResourceTypes.SupportTicket,
                ResourceId: ticket.Id.ToString(),
                ResourceNumber: ticket.TicketNumber,
                Action: SupportAuditActions.CommentAdd,
                Outcome: SupportAuditOutcomes.Success,
                OccurredAt: DateTime.UtcNow,
                CorrelationId: req.CorrelationId,
                IpAddress: req.IpAddress,
                UserAgent: req.UserAgent,
                Metadata: new Dictionary<string, object?>
                {
                    ["comment_id"] = comment.Id,
                    ["comment_type"] = comment.CommentType.ToString(),
                    ["visibility"] = comment.Visibility.ToString(),
                    ["author_user_id"] = comment.AuthorUserId,
                    ["body_length"] = bodyLen,
                });
            await _audit.PublishAsync(evt, ct);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex,
                "Audit dispatch threw event=support.ticket.comment_added ticket={TicketNumber}",
                ticket.TicketNumber);
        }
    }

    private async Task TryPublishCommentNotificationAsync(SupportTicket ticket, SupportTicketComment comment, CancellationToken ct)
    {
        try
        {
            var recipients = await ResolveCommentRecipientsAsync(ticket, comment, ct);
            if (recipients.Count == 0)
            {
                // Spec: "If no recipient can be resolved: log and skip dispatch."
                _log.LogInformation(
                    "Notification skipped (no recipients) event=support.ticket.comment_added ticket={TicketNumber}",
                    ticket.TicketNumber);
                return;
            }
            var payload = new Dictionary<string, object?>
            {
                ["ticket_id"] = ticket.Id,
                ["ticket_number"] = ticket.TicketNumber,
                ["title"] = ticket.Title,
                ["comment_id"] = comment.Id,
                ["comment_type"] = comment.CommentType.ToString(),
                ["visibility"] = comment.Visibility.ToString(),
                ["author_user_id"] = comment.AuthorUserId,
                ["tenant_id"] = ticket.TenantId,
            };
            var notification = new SupportNotification(
                SupportNotificationEventTypes.TicketCommentAdded,
                ticket.TenantId, ticket.Id, ticket.TicketNumber,
                recipients, payload, DateTime.UtcNow);
            await _notifications.PublishAsync(notification, ct);
        }
        catch (Exception ex)
        {
            // Notification dispatch must never break comment writes.
            _log.LogWarning(ex,
                "Notification dispatch threw event=support.ticket.comment_added ticket={TicketNumber}",
                ticket.TicketNumber);
        }
    }

    private async Task<List<NotificationRecipient>> ResolveCommentRecipientsAsync(
        SupportTicket ticket, SupportTicketComment comment, CancellationToken ct)
    {
        var list = new List<NotificationRecipient>();
        var isInternal = comment.Visibility == CommentVisibility.Internal
                         || comment.CommentType == CommentType.InternalNote;
        var isCustomerReply = comment.CommentType == CommentType.CustomerReply;

        if (isInternal)
        {
            // Internal comments may notify internal support staff (assigned user)
            // only — never the requester/customer.
            if (!string.IsNullOrWhiteSpace(ticket.AssignedUserId))
                list.Add(await MakeAssignedUserRecipientAsync(ticket, ct));
            return list;
        }

        if (isCustomerReply)
        {
            // Customer replies notify support participants (assigned user).
            if (!string.IsNullOrWhiteSpace(ticket.AssignedUserId))
                list.Add(await MakeAssignedUserRecipientAsync(ticket, ct));
        }
        else
        {
            // Customer-visible support reply: notify the requester.
            if (!string.IsNullOrWhiteSpace(ticket.RequesterUserId))
                list.Add(new NotificationRecipient(NotificationRecipientKind.User, ticket.RequesterUserId, null));
            if (!string.IsNullOrWhiteSpace(ticket.RequesterEmail))
                list.Add(new NotificationRecipient(NotificationRecipientKind.Email, null, ticket.RequesterEmail));
        }

        return list;
    }

    private async Task<NotificationRecipient> MakeAssignedUserRecipientAsync(SupportTicket ticket, CancellationToken ct)
    {
        var email = await _emailResolver.ResolveAsync(ticket.AssignedUserId!, ticket.TenantId, ct);
        return string.IsNullOrWhiteSpace(email)
            ? new NotificationRecipient(NotificationRecipientKind.User, ticket.AssignedUserId, null)
            : new NotificationRecipient(NotificationRecipientKind.Email, null, email);
    }

    public async Task<List<CommentResponse>> ListAsync(Guid ticketId, CommentVisibility? visibility, CommentType? commentType, CancellationToken ct = default)
    {
        var ticket = await ResolveTicketAsync(ticketId, ct);
        var tenantId = ticket.TenantId;

        var q = _db.TicketComments.AsNoTracking()
            .Where(c => c.TicketId == ticketId && c.TenantId == tenantId);
        if (visibility.HasValue) q = q.Where(c => c.Visibility == visibility.Value);
        if (commentType.HasValue) q = q.Where(c => c.CommentType == commentType.Value);

        var items = await q.OrderBy(c => c.CreatedAt).ToListAsync(ct);
        return items.Select(CommentResponse.From).ToList();
    }

    public async Task<List<TimelineItem>> TimelineAsync(Guid ticketId, CancellationToken ct = default)
    {
        var ticket = await ResolveTicketAsync(ticketId, ct);
        var tenantId = ticket.TenantId;

        var comments = await _db.TicketComments.AsNoTracking()
            .Where(c => c.TicketId == ticketId && c.TenantId == tenantId)
            .ToListAsync(ct);
        var events = await _db.TicketEvents.AsNoTracking()
            .Where(e => e.TicketId == ticketId && e.TenantId == tenantId)
            .ToListAsync(ct);

        var items = new List<TimelineItem>(comments.Count + events.Count);
        items.AddRange(comments.Select(c => new TimelineItem
        {
            Type = "comment",
            CreatedAt = c.CreatedAt,
            Body = c.Body,
            Summary = null,
            CommentType = c.CommentType.ToString(),
            Visibility = c.Visibility.ToString(),
            ActorUserId = c.AuthorUserId,
            ActorName = c.AuthorName,
            ActorEmail = c.AuthorEmail,
        }));
        items.AddRange(events.Select(e => new TimelineItem
        {
            Type = "event",
            CreatedAt = e.CreatedAt,
            Summary = e.Summary,
            EventType = e.EventType,
            ActorUserId = e.ActorUserId,
            MetadataJson = e.MetadataJson,
        }));

        return items.OrderBy(i => i.CreatedAt).ToList();
    }

    public async Task<CommentResponse> AddCustomerCommentAsync(
        string tenantId,
        Guid externalCustomerId,
        Guid ticketId,
        string body,
        string? authorEmail = null,
        string? authorName = null,
        CancellationToken ct = default)
    {
        // Enforce: tenant + externalCustomerId + CustomerVisible
        var ticket = await _db.Tickets
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.TenantId == tenantId
                && x.Id == ticketId
                && x.ExternalCustomerId == externalCustomerId
                && x.VisibilityScope == TicketVisibilityScope.CustomerVisible, ct);

        if (ticket is null) throw new TicketNotFoundException();

        var comment = new SupportTicketComment
        {
            Id          = Guid.NewGuid(),
            TicketId    = ticketId,
            TenantId    = tenantId,
            CommentType = CommentType.CustomerReply,
            Visibility  = CommentVisibility.CustomerVisible,
            Body        = body,
            AuthorUserId = null,
            AuthorEmail  = authorEmail,
            AuthorName   = authorName,
            CreatedAt    = DateTime.UtcNow,
        };
        _db.TicketComments.Add(comment);

        _events.Log(ticketId, tenantId, "customer_comment_added", "Customer comment added",
            metadata: new { comment_id = comment.Id, external_customer_id = externalCustomerId },
            actorUserId: null);

        await _db.SaveChangesAsync(ct);
        _log.LogInformation("Customer comment {CommentId} added to ticket {TicketId} tenant={TenantId} externalCustomerId={CustomerId}",
            comment.Id, ticketId, tenantId, externalCustomerId);

        try
        {
            var actor = _actor.Actor;
            var req = _actor.Request;
            var evt = new SupportAuditEvent(
                EventType: SupportAuditEventTypes.TicketCommentAdded,
                TenantId: ticket.TenantId,
                ActorUserId: actor.UserId,
                ActorEmail: actor.Email ?? authorEmail,
                ActorRoles: actor.Roles,
                ResourceType: SupportAuditResourceTypes.SupportTicket,
                ResourceId: ticket.Id.ToString(),
                ResourceNumber: ticket.TicketNumber,
                Action: SupportAuditActions.CommentAdd,
                Outcome: SupportAuditOutcomes.Success,
                OccurredAt: DateTime.UtcNow,
                CorrelationId: req.CorrelationId,
                IpAddress: req.IpAddress,
                UserAgent: req.UserAgent,
                Metadata: new Dictionary<string, object?>
                {
                    ["comment_id"]          = comment.Id,
                    ["comment_type"]        = comment.CommentType.ToString(),
                    ["visibility"]          = comment.Visibility.ToString(),
                    ["author_email"]        = authorEmail,
                    ["requester_type"]      = ticket.RequesterType.ToString(),
                    ["external_customer_id"] = externalCustomerId,
                    ["body_length"]         = body?.Length ?? 0,
                });
            await _audit.PublishAsync(evt, ct);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex,
                "Audit dispatch threw event=support.ticket.comment_added ticket={TicketNumber}",
                ticket.TicketNumber);
        }

        return CommentResponse.From(comment);
    }
}
