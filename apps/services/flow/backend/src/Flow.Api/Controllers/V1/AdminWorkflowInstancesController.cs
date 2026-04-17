using BuildingBlocks.Authorization;
using Flow.Application.Interfaces;
using Flow.Domain.Entities;
using Flow.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Flow.Api.Controllers.V1;

/// <summary>
/// E9.1 — read-only admin listing of <see cref="WorkflowInstance"/> rows for
/// the Control Center cross-product workflow operations view.
///
/// <para>
/// Authorization: <see cref="Policies.PlatformOrTenantAdmin"/>. Cross-tenant
/// visibility is granted only to <see cref="Roles.PlatformAdmin"/>; tenant
/// admins are scoped to their own tenant on the server side regardless of
/// any inbound query parameter.
/// </para>
///
/// <para>
/// The handler bypasses the per-tenant EF query filter via
/// <c>IgnoreQueryFilters()</c> and re-applies the appropriate tenant
/// predicate explicitly in code so a PlatformAdmin can see all rows while
/// any other admin sees only their own tenant. This endpoint is read-only;
/// execution / mutation surfaces remain on the existing controllers.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/admin/workflow-instances")]
[Authorize(Policy = Policies.PlatformOrTenantAdmin)]
public class AdminWorkflowInstancesController : ControllerBase
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    private readonly IFlowDbContext _db;
    private readonly ITenantProvider _tenantProvider;
    private readonly ILogger<AdminWorkflowInstancesController> _logger;

    public AdminWorkflowInstancesController(
        IFlowDbContext db,
        ITenantProvider tenantProvider,
        ILogger<AdminWorkflowInstancesController> logger)
    {
        _db = db;
        _tenantProvider = tenantProvider;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? productKey,
        [FromQuery] string? status,
        [FromQuery] string? tenantId,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        CancellationToken ct = default)
    {
        var p  = page < 1 ? 1 : page;
        var ps = pageSize < 1 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);

        var isPlatformAdmin = User.IsInRole(Roles.PlatformAdmin);

        // Bypass the per-tenant EF query filter and apply tenant scoping
        // explicitly: PlatformAdmin sees everything (optionally narrowed by
        // an explicit tenantId param), TenantAdmin always sees only their
        // own tenant — the inbound tenantId param is ignored for them.
        IQueryable<WorkflowInstance> q = _db.WorkflowInstances
            .AsNoTracking()
            .IgnoreQueryFilters();

        if (isPlatformAdmin)
        {
            if (!string.IsNullOrWhiteSpace(tenantId))
            {
                var t = tenantId.Trim().ToLowerInvariant();
                q = q.Where(w => w.TenantId == t);
            }
        }
        else
        {
            string callerTid;
            try { callerTid = _tenantProvider.GetTenantId(); }
            catch { return Forbid(); }
            if (string.IsNullOrEmpty(callerTid)) return Forbid();
            q = q.Where(w => w.TenantId == callerTid);
        }

        if (!string.IsNullOrWhiteSpace(productKey))
        {
            var pk = productKey.Trim();
            q = q.Where(w => w.ProductKey == pk);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var s = status.Trim();
            q = q.Where(w => w.Status == s);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term    = search.Trim();
            var pattern = $"%{term}%";
            // Lightweight contains-style search across the fields an
            // operator is most likely to paste in: instance id, correlation
            // key, and current step key. MySQL's default collation is
            // case-insensitive, so a plain LIKE is sufficient — using
            // EF.Functions.Like keeps the predicate translatable.
            q = q.Where(w =>
                (w.CorrelationKey != null && EF.Functions.Like(w.CorrelationKey, pattern)) ||
                (w.CurrentStepKey != null && EF.Functions.Like(w.CurrentStepKey, pattern)) ||
                EF.Functions.Like(w.Id.ToString(), pattern));
        }

        var total = await q.CountAsync(ct);

        // Project before paging so we can join the workflow definition's
        // display name and the optional product mapping (source entity)
        // without dragging a fat entity graph into memory.
        //
        // SECURITY: the joined sources also call IgnoreQueryFilters() so
        // PlatformAdmin can read across tenants, but each subquery
        // re-applies an explicit `TenantId == w.TenantId` predicate. This
        // prevents a stray cross-tenant mapping/definition row (data-quality
        // edge case or future bug) from leaking another tenant's
        // source-entity metadata onto an instance row.
        //
        // DETERMINISM: a workflow instance can in theory have more than one
        // ProductWorkflowMapping (e.g. legacy + active). Active rows are
        // preferred; ties are broken by most-recent UpdatedAt then CreatedAt
        // so the selected mapping is stable across query plans.
        var defs = _db.FlowDefinitions.AsNoTracking().IgnoreQueryFilters();
        var maps = _db.ProductWorkflowMappings.AsNoTracking().IgnoreQueryFilters();

        var rows = await q
            .OrderByDescending(w => w.UpdatedAt ?? w.CreatedAt)
            .Skip((p - 1) * ps)
            .Take(ps)
            .Select(w => new
            {
                Instance = w,
                DefinitionName = defs
                    .Where(d => d.Id == w.WorkflowDefinitionId && d.TenantId == w.TenantId)
                    .Select(d => d.Name)
                    .FirstOrDefault(),
                Mapping = maps
                    .Where(m => m.WorkflowInstanceId == w.Id && m.TenantId == w.TenantId)
                    .OrderByDescending(m => m.Status == "Active" ? 1 : 0)
                    .ThenByDescending(m => m.UpdatedAt ?? m.CreatedAt)
                    .Select(m => new { m.SourceEntityType, m.SourceEntityId })
                    .FirstOrDefault(),
            })
            .ToListAsync(ct);

        var items = rows.Select(r => new AdminWorkflowInstanceListItem
        {
            Id                   = r.Instance.Id,
            TenantId             = r.Instance.TenantId,
            ProductKey           = r.Instance.ProductKey,
            WorkflowDefinitionId = r.Instance.WorkflowDefinitionId,
            WorkflowName         = r.DefinitionName,
            Status               = r.Instance.Status,
            CurrentStepKey       = r.Instance.CurrentStepKey,
            AssignedToUserId     = r.Instance.AssignedToUserId,
            CorrelationKey       = r.Instance.CorrelationKey,
            SourceEntityType     = r.Mapping?.SourceEntityType,
            SourceEntityId       = r.Mapping?.SourceEntityId,
            StartedAt            = r.Instance.StartedAt,
            CompletedAt          = r.Instance.CompletedAt,
            UpdatedAt            = r.Instance.UpdatedAt,
            CreatedAt            = r.Instance.CreatedAt,
        }).ToList();

        _logger.LogInformation(
            "AdminWorkflowInstances.List platformAdmin={IsPlatformAdmin} count={Count} total={Total} filters: product={ProductKey} status={Status} tenant={TenantId} search={SearchPresent}",
            isPlatformAdmin, items.Count, total, productKey, status, tenantId, !string.IsNullOrWhiteSpace(search));

        return Ok(new AdminWorkflowInstanceListResponse
        {
            Items     = items,
            TotalCount = total,
            Page      = p,
            PageSize  = ps,
        });
    }
}

public sealed record AdminWorkflowInstanceListItem
{
    public Guid Id { get; init; }
    public string TenantId { get; init; } = string.Empty;
    public string ProductKey { get; init; } = string.Empty;
    public Guid WorkflowDefinitionId { get; init; }
    public string? WorkflowName { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? CurrentStepKey { get; init; }
    public string? AssignedToUserId { get; init; }
    public string? CorrelationKey { get; init; }
    public string? SourceEntityType { get; init; }
    public string? SourceEntityId { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed record AdminWorkflowInstanceListResponse
{
    public List<AdminWorkflowInstanceListItem> Items { get; init; } = new();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}
