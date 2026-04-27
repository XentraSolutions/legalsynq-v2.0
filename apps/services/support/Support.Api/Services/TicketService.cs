using Support.Api.Audit;
using Support.Api.Auth;
using Support.Api.Data;
using Support.Api.Domain;
using Support.Api.Dtos;
using Support.Api.Notifications;
using Support.Api.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Support.Api.Services;

public class InvalidStatusTransitionException : Exception
{
    public InvalidStatusTransitionException(TicketStatus from, TicketStatus to)
        : base($"Invalid status transition: {from} -> {to}") { }
}

public class TicketNotFoundException : Exception { }
public class TenantMissingException : Exception { }

public interface ITicketService
{
    Task<TicketResponse> CreateAsync(CreateTicketRequest req, CancellationToken ct = default);
    Task<PagedResponse<TicketResponse>> ListAsync(TicketListQuery query, CancellationToken ct = default);
    Task<TicketResponse?> GetAsync(Guid id, CancellationToken ct = default);
    Task<TicketResponse> UpdateAsync(Guid id, UpdateTicketRequest req, CancellationToken ct = default);
    Task<TicketResponse> AssignAsync(Guid id, AssignTicketRequest req, CancellationToken ct = default);

    /// <summary>
    /// Internal service method — returns all tickets associated with a specific external customer
    /// within the tenant. Scoped by both tenantId and externalCustomerId to preserve tenant isolation.
    /// Does NOT restrict by VisibilityScope — use ListCustomerTicketsAsync for customer-facing access.
    /// </summary>
    Task<PagedResponse<TicketResponse>> ListByExternalCustomerAsync(
        string tenantId,
        Guid externalCustomerId,
        int page = 1,
        int pageSize = 25,
        CancellationToken ct = default);

    /// <summary>
    /// Customer-safe list — returns only CustomerVisible tickets owned by this customer within the tenant.
    /// Enforces: tenantId + externalCustomerId + VisibilityScope=CustomerVisible.
    /// Used exclusively by the CustomerAccess-protected endpoints.
    /// </summary>
    Task<PagedResponse<TicketResponse>> ListCustomerTicketsAsync(
        string tenantId,
        Guid externalCustomerId,
        int page = 1,
        int pageSize = 25,
        CancellationToken ct = default);

    /// <summary>
    /// Customer-safe get — returns a single CustomerVisible ticket owned by this customer.
    /// Enforces: tenantId + ticketId + externalCustomerId + VisibilityScope=CustomerVisible.
    /// Returns null if any constraint fails (caller should return 404).
    /// Used exclusively by the CustomerAccess-protected endpoints.
    /// </summary>
    Task<TicketResponse?> GetCustomerTicketAsync(
        string tenantId,
        Guid externalCustomerId,
        Guid ticketId,
        CancellationToken ct = default);
}

public class TicketListQuery
{
    public TicketStatus? Status { get; set; }
    public TicketPriority? Priority { get; set; }
    public TicketSeverity? Severity { get; set; }
    public TicketSource? Source { get; set; }
    public string? ProductCode { get; set; }
    public string? Category { get; set; }
    public string? Search { get; set; }
    public string? AssignedUserId { get; set; }
    public Guid? AssignedQueueId { get; set; }
    public bool? Unassigned { get; set; }
    public Guid? ExternalCustomerId { get; set; }
    public TicketVisibilityScope? VisibilityScope { get; set; }
    /// <summary>
    /// Optional tenant filter for PlatformAdmin cross-tenant queries.
    /// Ignored when the request is scoped by a tenant JWT claim.
    /// </summary>
    public string? TenantId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

public class TicketService : ITicketService
{
    private static readonly Dictionary<TicketStatus, HashSet<TicketStatus>> Allowed = new()
    {
        [TicketStatus.Open] = new() { TicketStatus.InProgress, TicketStatus.Pending, TicketStatus.Cancelled },
        [TicketStatus.InProgress] = new() { TicketStatus.Pending, TicketStatus.Resolved, TicketStatus.Cancelled },
        [TicketStatus.Pending] = new() { TicketStatus.InProgress, TicketStatus.Resolved, TicketStatus.Cancelled },
        [TicketStatus.Resolved] = new() { TicketStatus.Closed, TicketStatus.InProgress },
        [TicketStatus.Closed] = new(),
        [TicketStatus.Cancelled] = new(),
    };

    private readonly SupportDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly ITicketNumberGenerator _numbers;
    private readonly IEventLogger _events;
    private readonly ILogger<TicketService> _log;
    private readonly IWebHostEnvironment _env;
    private readonly INotificationPublisher _notifications;
    private readonly IAuditPublisher _audit;
    private readonly IActorAccessor _actor;
    private readonly IExternalCustomerService _externalCustomers;

    public TicketService(SupportDbContext db, ITenantContext tenant, ITicketNumberGenerator numbers,
        IEventLogger events, ILogger<TicketService> log, IWebHostEnvironment env,
        INotificationPublisher notifications, IAuditPublisher audit, IActorAccessor actor,
        IExternalCustomerService externalCustomers)
    {
        _db = db;
        _tenant = tenant;
        _numbers = numbers;
        _events = events;
        _log = log;
        _env = env;
        _notifications = notifications;
        _audit = audit;
        _actor = actor;
        _externalCustomers = externalCustomers;
    }

    private async Task TryAuditAsync(SupportAuditEvent evt, CancellationToken ct)
    {
        try
        {
            await _audit.PublishAsync(evt, ct);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex,
                "Audit dispatch threw event={EventType} resource={ResourceId}",
                evt.EventType, evt.ResourceId);
        }
    }

    private SupportAuditEvent BuildTicketAudit(
        string eventType,
        string action,
        SupportTicket t,
        IReadOnlyDictionary<string, object?> metadata)
    {
        var actor = _actor.Actor;
        var req = _actor.Request;
        return new SupportAuditEvent(
            EventType: eventType,
            TenantId: t.TenantId,
            ActorUserId: actor.UserId ?? _tenant.UserId,
            ActorEmail: actor.Email,
            ActorRoles: actor.Roles,
            ResourceType: SupportAuditResourceTypes.SupportTicket,
            ResourceId: t.Id.ToString(),
            ResourceNumber: t.TicketNumber,
            Action: action,
            Outcome: SupportAuditOutcomes.Success,
            OccurredAt: DateTime.UtcNow,
            CorrelationId: req.CorrelationId,
            IpAddress: req.IpAddress,
            UserAgent: req.UserAgent,
            Metadata: metadata);
    }

    private async Task TryPublishAsync(SupportNotification n, CancellationToken ct)
    {
        if (n.Recipients.Count == 0)
        {
            // Spec: "If no recipient can be resolved: log and skip dispatch."
            _log.LogInformation(
                "Notification skipped (no recipients) event={EventType} ticket={TicketNumber}",
                n.EventType, n.TicketNumber);
            return;
        }
        try
        {
            await _notifications.PublishAsync(n, ct);
        }
        catch (Exception ex)
        {
            // Hard guarantee: notification dispatch must never break ticket writes.
            _log.LogWarning(ex,
                "Notification dispatch threw event={EventType} ticket={TicketNumber}",
                n.EventType, n.TicketNumber);
        }
    }

    private static List<NotificationRecipient> RequesterRecipients(SupportTicket t)
    {
        var list = new List<NotificationRecipient>();
        if (!string.IsNullOrWhiteSpace(t.RequesterUserId))
            list.Add(new NotificationRecipient(NotificationRecipientKind.User, t.RequesterUserId, null));
        if (!string.IsNullOrWhiteSpace(t.RequesterEmail))
            list.Add(new NotificationRecipient(NotificationRecipientKind.Email, null, t.RequesterEmail));
        return list;
    }

    private static void AddAssignedUser(List<NotificationRecipient> list, SupportTicket t)
    {
        if (!string.IsNullOrWhiteSpace(t.AssignedUserId))
            list.Add(new NotificationRecipient(NotificationRecipientKind.User, t.AssignedUserId, null));
    }

    private async Task AddActiveQueueMembersAsync(List<NotificationRecipient> list, Guid? queueId, string tenantId, CancellationToken ct)
    {
        if (!queueId.HasValue) return;
        var members = await _db.QueueMembers.AsNoTracking()
            .Where(m => m.QueueId == queueId.Value && m.TenantId == tenantId && m.IsActive)
            .Select(m => m.UserId)
            .ToListAsync(ct);
        foreach (var uid in members)
        {
            if (string.IsNullOrWhiteSpace(uid)) continue;
            list.Add(new NotificationRecipient(NotificationRecipientKind.QueueMember, uid, null, queueId));
        }
    }

    private string ResolveTenantId(string? fromBody)
    {
        if (_tenant.IsResolved) return _tenant.TenantId!;
        // Body fallback only honored in Development / Testing.
        var allowBody = _env.IsDevelopment() || _env.IsEnvironment("Testing");
        if (allowBody && !string.IsNullOrWhiteSpace(fromBody)) return fromBody!;
        _log.LogWarning("Tenant resolution failure on support ticket request");
        throw new TenantMissingException();
    }

    private string RequireTenant()
    {
        if (!_tenant.IsResolved)
        {
            _log.LogWarning("Tenant resolution failure on support ticket request");
            throw new TenantMissingException();
        }
        return _tenant.TenantId!;
    }

    public async Task<TicketResponse> CreateAsync(CreateTicketRequest req, CancellationToken ct = default)
    {
        var tenantId = ResolveTenantId(req.TenantId);
        var now = DateTime.UtcNow;
        var ticketNumber = await _numbers.NextAsync(tenantId, ct);

        // Ownership fields — defaults to InternalUser/Internal path.
        var requesterType = TicketRequesterType.InternalUser;
        var visibilityScope = TicketVisibilityScope.Internal;
        Guid? externalCustomerId = null;
        var requesterEmail = req.RequesterEmail;
        var requesterName = req.RequesterName;

        // Optional external customer path.
        // Triggered only when ExternalCustomerEmail is supplied.
        if (!string.IsNullOrWhiteSpace(req.ExternalCustomerEmail))
        {
            var customer = await _externalCustomers.ResolveOrCreateAsync(
                tenantId,
                req.ExternalCustomerEmail,
                req.ExternalCustomerName,
                ct);

            requesterType      = TicketRequesterType.ExternalCustomer;
            visibilityScope    = TicketVisibilityScope.CustomerVisible;
            externalCustomerId = customer.Id;
            requesterEmail     = customer.Email;
            requesterName      = !string.IsNullOrWhiteSpace(requesterName) ? requesterName : customer.Name;

            _log.LogDebug("Ticket {TicketNumber} linked to ExternalCustomer {CustomerId} tenant={TenantId}",
                ticketNumber, customer.Id, tenantId);
        }

        var ticket = new SupportTicket
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProductCode = req.ProductCode,
            TicketNumber = ticketNumber,
            Title = req.Title,
            Description = req.Description,
            Status = TicketStatus.Open,
            Priority = req.Priority,
            Severity = req.Severity,
            Category = req.Category,
            Source = req.Source,
            RequesterUserId = req.RequesterUserId,
            RequesterName = requesterName,
            RequesterEmail = requesterEmail,
            RequesterType = requesterType,
            ExternalCustomerId = externalCustomerId,
            VisibilityScope = visibilityScope,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedByUserId = _tenant.UserId,
            UpdatedByUserId = _tenant.UserId,
        };

        _db.Tickets.Add(ticket);
        _events.Log(ticket.Id, tenantId, "created", "Ticket created",
            metadata: new { ticket_number = ticket.TicketNumber }, actorUserId: _tenant.UserId);
        await _db.SaveChangesAsync(ct);
        _log.LogInformation("Ticket created {TicketNumber} tenant={TenantId} requesterType={RequesterType}",
            ticket.TicketNumber, tenantId, ticket.RequesterType);

        var recipients = RequesterRecipients(ticket);
        var payload = new Dictionary<string, object?>
        {
            ["ticket_id"] = ticket.Id,
            ["ticket_number"] = ticket.TicketNumber,
            ["title"] = ticket.Title,
            ["status"] = ticket.Status.ToString(),
            ["priority"] = ticket.Priority.ToString(),
            ["tenant_id"] = ticket.TenantId,
            ["requester_user_id"] = ticket.RequesterUserId,
            ["requester_email"] = ticket.RequesterEmail,
            ["product_code"] = ticket.ProductCode,
            ["requester_type"] = ticket.RequesterType.ToString(),
            ["external_customer_id"] = ticket.ExternalCustomerId,
        };
        await TryPublishAsync(new SupportNotification(
            SupportNotificationEventTypes.TicketCreated,
            ticket.TenantId, ticket.Id, ticket.TicketNumber,
            recipients, payload, DateTime.UtcNow), ct);

        await TryAuditAsync(BuildTicketAudit(
            SupportAuditEventTypes.TicketCreated,
            SupportAuditActions.Create,
            ticket,
            new Dictionary<string, object?>
            {
                ["title"] = ticket.Title,
                ["priority"] = ticket.Priority.ToString(),
                ["status"] = ticket.Status.ToString(),
                ["product_code"] = ticket.ProductCode,
                ["requester_user_id"] = ticket.RequesterUserId,
                ["requester_email"] = ticket.RequesterEmail,
                ["requester_type"] = ticket.RequesterType.ToString(),
                ["external_customer_id"] = ticket.ExternalCustomerId,
                ["visibility_scope"] = ticket.VisibilityScope.ToString(),
            }), ct);

        return TicketResponse.From(ticket);
    }

    public async Task<PagedResponse<TicketResponse>> ListAsync(TicketListQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        IQueryable<SupportTicket> q;
        // PlatformAdmin check must come FIRST — the platform admin JWT carries a
        // synthetic tenant_id claim (system placeholder) that makes _tenant.IsResolved=true,
        // so we must not let the tenant-scoped branch run for platform admins.
        if (_actor.Actor.Roles.Contains(SupportRoles.PlatformAdmin, StringComparer.OrdinalIgnoreCase))
        {
            q = _db.Tickets.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(query.TenantId))
                q = q.Where(t => t.TenantId == query.TenantId);
        }
        else if (_tenant.IsResolved)
        {
            q = _db.Tickets.AsNoTracking().Where(t => t.TenantId == _tenant.TenantId);
        }
        else
        {
            _log.LogWarning("Tenant resolution failure on support ticket list request");
            throw new TenantMissingException();
        }
        if (query.Status.HasValue) q = q.Where(t => t.Status == query.Status.Value);
        if (query.Priority.HasValue) q = q.Where(t => t.Priority == query.Priority.Value);
        if (query.Severity.HasValue) q = q.Where(t => t.Severity == query.Severity.Value);
        if (query.Source.HasValue) q = q.Where(t => t.Source == query.Source.Value);
        if (!string.IsNullOrWhiteSpace(query.ProductCode)) q = q.Where(t => t.ProductCode == query.ProductCode);
        if (!string.IsNullOrWhiteSpace(query.Category)) q = q.Where(t => t.Category == query.Category);
        if (!string.IsNullOrWhiteSpace(query.AssignedUserId))
            q = q.Where(t => t.AssignedUserId == query.AssignedUserId);
        if (query.AssignedQueueId.HasValue)
        {
            var qid = query.AssignedQueueId.Value.ToString();
            q = q.Where(t => t.AssignedQueueId == qid);
        }
        if (query.Unassigned == true)
            q = q.Where(t => t.AssignedUserId == null && t.AssignedQueueId == null);
        if (query.ExternalCustomerId.HasValue)
            q = q.Where(t => t.ExternalCustomerId == query.ExternalCustomerId.Value);
        if (query.VisibilityScope.HasValue)
            q = q.Where(t => t.VisibilityScope == query.VisibilityScope.Value);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            q = q.Where(t => t.Title.Contains(s) || t.TicketNumber.Contains(s));
        }

        var total = await q.LongCountAsync(ct);
        var items = await q.OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResponse<TicketResponse>
        {
            Items = items.Select(TicketResponse.From).ToList(),
            Page = page,
            PageSize = pageSize,
            Total = total,
        };
    }

    public async Task<TicketResponse?> GetAsync(Guid id, CancellationToken ct = default)
    {
        SupportTicket? t;
        // PlatformAdmin check must come FIRST — see ListAsync for the full rationale.
        if (_actor.Actor.Roles.Contains(SupportRoles.PlatformAdmin, StringComparer.OrdinalIgnoreCase))
        {
            t = await _db.Tickets.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id, ct);
        }
        else if (_tenant.IsResolved)
        {
            t = await _db.Tickets.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == _tenant.TenantId, ct);
        }
        else
        {
            _log.LogWarning("Tenant resolution failure on support ticket get request");
            throw new TenantMissingException();
        }
        return t is null ? null : TicketResponse.From(t);
    }

    public async Task<TicketResponse> UpdateAsync(Guid id, UpdateTicketRequest req, CancellationToken ct = default)
    {
        SupportTicket? t;
        // PlatformAdmin check must come FIRST — the platform admin JWT carries a
        // synthetic tenant_id claim (system placeholder) that makes _tenant.IsResolved=true.
        // Without this ordering, the tenant-scoped query uses the fake tenant ID and the
        // ticket is never found, resulting in a spurious 404.
        if (_actor.Actor.Roles.Contains(SupportRoles.PlatformAdmin, StringComparer.OrdinalIgnoreCase))
        {
            t = await _db.Tickets.FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new TicketNotFoundException();
        }
        else
        {
            var scopedTenantId = RequireTenant();
            t = await _db.Tickets.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == scopedTenantId, ct)
                ?? throw new TicketNotFoundException();
        }
        var tenantId = t.TenantId;

        if (t.Status is TicketStatus.Closed or TicketStatus.Cancelled)
        {
            throw new InvalidStatusTransitionException(t.Status, req.Status ?? t.Status);
        }

        TicketStatus? statusChangedFrom = null;
        if (req.Status.HasValue && req.Status.Value != t.Status)
        {
            if (!Allowed[t.Status].Contains(req.Status.Value))
            {
                _log.LogWarning("Invalid status transition attempt {From} -> {To} on ticket {Id}", t.Status, req.Status.Value, t.Id);
                throw new InvalidStatusTransitionException(t.Status, req.Status.Value);
            }
            statusChangedFrom = t.Status;
            t.Status = req.Status.Value;
            if (t.Status == TicketStatus.Resolved && t.ResolvedAt is null) t.ResolvedAt = DateTime.UtcNow;
            if ((t.Status == TicketStatus.Closed || t.Status == TicketStatus.Cancelled) && t.ClosedAt is null)
            {
                t.ClosedAt = DateTime.UtcNow;
            }
        }

        if (req.Title is not null) t.Title = req.Title;
        if (req.Description is not null) t.Description = req.Description;
        if (req.Priority.HasValue) t.Priority = req.Priority.Value;
        if (req.Severity.HasValue) t.Severity = req.Severity.Value;
        if (req.Category is not null) t.Category = req.Category;
        if (req.AssignedUserId is not null) t.AssignedUserId = req.AssignedUserId;
        if (req.AssignedQueueId is not null) t.AssignedQueueId = req.AssignedQueueId;
        if (req.DueAt.HasValue) t.DueAt = req.DueAt;
        if (req.RequesterName is not null) t.RequesterName = req.RequesterName;
        if (req.RequesterEmail is not null) t.RequesterEmail = req.RequesterEmail;

        t.UpdatedAt = DateTime.UtcNow;
        t.UpdatedByUserId = _tenant.UserId;

        if (statusChangedFrom.HasValue)
        {
            _events.Log(t.Id, tenantId, "status_changed",
                $"Status changed from {statusChangedFrom.Value} to {t.Status}",
                metadata: new { from = statusChangedFrom.Value.ToString(), to = t.Status.ToString() },
                actorUserId: _tenant.UserId);
        }
        _events.Log(t.Id, tenantId, "updated", "Ticket updated", actorUserId: _tenant.UserId);

        await _db.SaveChangesAsync(ct);
        _log.LogInformation("Ticket updated {TicketNumber} tenant={TenantId}", t.TicketNumber, tenantId);

        var recipients = RequesterRecipients(t);
        AddAssignedUser(recipients, t);

        if (statusChangedFrom.HasValue)
        {
            var statusPayload = new Dictionary<string, object?>
            {
                ["ticket_id"] = t.Id,
                ["ticket_number"] = t.TicketNumber,
                ["title"] = t.Title,
                ["previous_status"] = statusChangedFrom.Value.ToString(),
                ["new_status"] = t.Status.ToString(),
                ["tenant_id"] = t.TenantId,
            };
            await TryPublishAsync(new SupportNotification(
                SupportNotificationEventTypes.TicketStatusChanged,
                t.TenantId, t.Id, t.TicketNumber,
                recipients, statusPayload, DateTime.UtcNow), ct);
        }

        var updatedPayload = new Dictionary<string, object?>
        {
            ["ticket_id"] = t.Id,
            ["ticket_number"] = t.TicketNumber,
            ["title"] = t.Title,
            ["status"] = t.Status.ToString(),
            ["priority"] = t.Priority.ToString(),
            ["tenant_id"] = t.TenantId,
        };
        await TryPublishAsync(new SupportNotification(
            SupportNotificationEventTypes.TicketUpdated,
            t.TenantId, t.Id, t.TicketNumber,
            recipients, updatedPayload, DateTime.UtcNow), ct);

        if (statusChangedFrom.HasValue)
        {
            await TryAuditAsync(BuildTicketAudit(
                SupportAuditEventTypes.TicketStatusChanged,
                SupportAuditActions.StatusChange,
                t,
                new Dictionary<string, object?>
                {
                    ["previous_status"] = statusChangedFrom.Value.ToString(),
                    ["new_status"] = t.Status.ToString(),
                }), ct);
        }

        await TryAuditAsync(BuildTicketAudit(
            SupportAuditEventTypes.TicketUpdated,
            SupportAuditActions.Update,
            t,
            new Dictionary<string, object?>
            {
                ["status"] = t.Status.ToString(),
                ["priority"] = t.Priority.ToString(),
                ["assigned_user_id"] = t.AssignedUserId,
                ["assigned_queue_id"] = t.AssignedQueueId,
                ["title_changed"] = req.Title is not null,
                ["description_changed"] = req.Description is not null,
                ["category_changed"] = req.Category is not null,
                ["due_at_changed"] = req.DueAt.HasValue,
            }), ct);

        return TicketResponse.From(t);
    }

    public async Task<TicketResponse> AssignAsync(Guid id, AssignTicketRequest req, CancellationToken ct = default)
    {
        SupportTicket? t;
        // PlatformAdmin check must come FIRST — same rationale as UpdateAsync and GetAsync.
        if (_actor.Actor.Roles.Contains(SupportRoles.PlatformAdmin, StringComparer.OrdinalIgnoreCase))
        {
            t = await _db.Tickets.FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new TicketNotFoundException();
        }
        else
        {
            var scopedTenantId = RequireTenant();
            t = await _db.Tickets.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == scopedTenantId, ct)
                ?? throw new TicketNotFoundException();
        }
        var tenantId = t.TenantId;

        var prevUser = t.AssignedUserId;
        var prevQueue = t.AssignedQueueId;
        bool cleared = false;

        if (req.ClearAssignment == true)
        {
            t.AssignedUserId = null;
            t.AssignedQueueId = null;
            cleared = true;
        }
        else
        {
            if (req.AssignedQueueId.HasValue)
            {
                var queueId = req.AssignedQueueId.Value;
                var queue = await _db.Queues.AsNoTracking()
                    .FirstOrDefaultAsync(q => q.Id == queueId && q.TenantId == tenantId, ct);
                if (queue is null) throw new QueueNotFoundException();
                if (!queue.IsActive) throw new QueueInactiveException();
                t.AssignedQueueId = queueId.ToString();
            }
            if (!string.IsNullOrWhiteSpace(req.AssignedUserId))
            {
                t.AssignedUserId = req.AssignedUserId.Trim();
            }
        }

        t.UpdatedAt = DateTime.UtcNow;
        t.UpdatedByUserId = _tenant.UserId;

        _events.Log(t.Id, tenantId, "assignment_changed",
            cleared ? "Assignment cleared" : "Assignment changed",
            metadata: new
            {
                previous_assigned_user_id = prevUser,
                previous_assigned_queue_id = prevQueue,
                assigned_user_id = t.AssignedUserId,
                assigned_queue_id = t.AssignedQueueId,
            },
            actorUserId: _tenant.UserId);

        await _db.SaveChangesAsync(ct);
        _log.LogInformation("Ticket {TicketNumber} assignment changed tenant={TenantId} cleared={Cleared}",
            t.TicketNumber, tenantId, cleared);

        var recipients = new List<NotificationRecipient>();
        AddAssignedUser(recipients, t);

        Guid? newQueueGuid = null;
        if (!string.IsNullOrWhiteSpace(t.AssignedQueueId) && Guid.TryParse(t.AssignedQueueId, out var qg))
        {
            newQueueGuid = qg;
        }
        await AddActiveQueueMembersAsync(recipients, newQueueGuid, tenantId, ct);

        var payload = new Dictionary<string, object?>
        {
            ["ticket_id"] = t.Id,
            ["ticket_number"] = t.TicketNumber,
            ["title"] = t.Title,
            ["assigned_user_id"] = t.AssignedUserId,
            ["assigned_queue_id"] = t.AssignedQueueId,
            ["previous_assigned_user_id"] = prevUser,
            ["previous_assigned_queue_id"] = prevQueue,
            ["tenant_id"] = t.TenantId,
        };
        await TryPublishAsync(new SupportNotification(
            SupportNotificationEventTypes.TicketAssigned,
            t.TenantId, t.Id, t.TicketNumber,
            recipients, payload, DateTime.UtcNow), ct);

        await TryAuditAsync(BuildTicketAudit(
            SupportAuditEventTypes.TicketAssignmentChanged,
            SupportAuditActions.Assign,
            t,
            new Dictionary<string, object?>
            {
                ["previous_assigned_user_id"] = prevUser,
                ["previous_assigned_queue_id"] = prevQueue,
                ["assigned_user_id"] = t.AssignedUserId,
                ["assigned_queue_id"] = t.AssignedQueueId,
                ["cleared"] = cleared,
            }), ct);

        return TicketResponse.From(t);
    }

    public async Task<PagedResponse<TicketResponse>> ListByExternalCustomerAsync(
        string tenantId,
        Guid externalCustomerId,
        int page = 1,
        int pageSize = 25,
        CancellationToken ct = default)
    {
        page     = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var q = _db.Tickets
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.ExternalCustomerId == externalCustomerId);

        var total = await q.LongCountAsync(ct);
        var items = await q
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResponse<TicketResponse>
        {
            Items    = items.Select(TicketResponse.From).ToList(),
            Page     = page,
            PageSize = pageSize,
            Total    = total,
        };
    }

    public async Task<PagedResponse<TicketResponse>> ListCustomerTicketsAsync(
        string tenantId,
        Guid externalCustomerId,
        int page = 1,
        int pageSize = 25,
        CancellationToken ct = default)
    {
        page     = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var q = _db.Tickets
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId
                     && t.ExternalCustomerId == externalCustomerId
                     && t.VisibilityScope == TicketVisibilityScope.CustomerVisible);

        var total = await q.LongCountAsync(ct);
        var items = await q
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResponse<TicketResponse>
        {
            Items    = items.Select(TicketResponse.From).ToList(),
            Page     = page,
            PageSize = pageSize,
            Total    = total,
        };
    }

    public async Task<TicketResponse?> GetCustomerTicketAsync(
        string tenantId,
        Guid externalCustomerId,
        Guid ticketId,
        CancellationToken ct = default)
    {
        var t = await _db.Tickets
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.TenantId == tenantId
                && x.Id == ticketId
                && x.ExternalCustomerId == externalCustomerId
                && x.VisibilityScope == TicketVisibilityScope.CustomerVisible, ct);

        return t is null ? null : TicketResponse.From(t);
    }
}
