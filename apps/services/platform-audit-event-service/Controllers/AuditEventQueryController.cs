using Microsoft.AspNetCore.Mvc;
using PlatformAuditEventService.Authorization;
using PlatformAuditEventService.DTOs;
using PlatformAuditEventService.Services;
using PlatformAuditEventService.Utilities;

// Disambiguate from legacy PlatformAuditEventService.DTOs.AuditEventQueryRequest.
using AuditEventQueryRequest   = PlatformAuditEventService.DTOs.Query.AuditEventQueryRequest;
using AuditEventQueryResponse  = PlatformAuditEventService.DTOs.Query.AuditEventQueryResponse;
using AuditEventRecordResponse = PlatformAuditEventService.DTOs.Query.AuditEventRecordResponse;

namespace PlatformAuditEventService.Controllers;

/// <summary>
/// Query and retrieval endpoints for persisted audit event records.
///
/// Route prefix: /audit
///
/// Authorization: resolved per-request by <see cref="Middleware.QueryAuthMiddleware"/>.
/// Each action calls <see cref="IQueryAuthorizer.Authorize"/> which:
///   1. Validates the caller has sufficient scope for this query.
///   2. Mutates the query in-place to enforce scope constraints (tenant, org, actor, visibility).
///
/// Scoped endpoints (entity, actor, user, tenant, organization) accept additional
/// filter parameters from the query string. Path segments always take precedence
/// over the corresponding query-string field.
/// </summary>
[ApiController]
[Route("audit")]
[Produces("application/json")]
public sealed class AuditEventQueryController : ControllerBase
{
    private readonly IAuditEventQueryService             _queryService;
    private readonly IQueryAuthorizer                    _authorizer;
    private readonly ILogger<AuditEventQueryController>  _logger;

    public AuditEventQueryController(
        IAuditEventQueryService            queryService,
        IQueryAuthorizer                   authorizer,
        ILogger<AuditEventQueryController> logger)
    {
        _queryService = queryService;
        _authorizer   = authorizer;
        _logger       = logger;
    }

    // ── GET /audit/events ─────────────────────────────────────────────────────

    /// <summary>
    /// Execute a filtered, paginated query over all accessible audit event records.
    ///
    /// All filter parameters are optional. Multiple filters are AND-ed together.
    /// The caller's scope constrains the result set — callers without PlatformAdmin
    /// scope are restricted to their own tenant's records.
    /// </summary>
    /// <param name="query">Filter and pagination parameters (bound from query string).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Query succeeded. Body contains the paginated result.</response>
    /// <response code="400">Invalid query parameters.</response>
    /// <response code="401">No valid credentials were presented.</response>
    /// <response code="403">Caller's scope is insufficient for this query.</response>
    [HttpGet("events")]
    [ProducesResponseType(typeof(ApiResponse<AuditEventQueryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>),                  StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>),                  StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>),                  StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListEvents(
        [FromQuery] AuditEventQueryRequest query,
        CancellationToken ct)
    {
        var deny = AuthorizeQuery(query);
        if (deny is not null) return deny;

        var result  = await _queryService.QueryAsync(query, ct);
        var traceId = TraceIdAccessor.Current();

        _logger.LogDebug(
            "GET /audit/events → TotalCount={Total} TraceId={TraceId}",
            result.TotalCount, traceId);

        return Ok(ApiResponse<AuditEventQueryResponse>.Ok(result, traceId: traceId));
    }

    // ── GET /audit/events/{auditId} ───────────────────────────────────────────

    /// <summary>
    /// Retrieve a single audit event record by its stable public identifier.
    /// </summary>
    /// <param name="auditId">The platform-assigned AuditId (UUID).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Record found.</response>
    /// <response code="401">No valid credentials were presented.</response>
    /// <response code="403">Caller's scope is insufficient.</response>
    /// <response code="404">No record exists with the given AuditId.</response>
    [HttpGet("events/{auditId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AuditEventRecordResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>),                   StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>),                   StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>),                   StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEvent(Guid auditId, CancellationToken ct)
    {
        // For single-record lookup, authorize with an empty query.
        // Scope constraints that apply to the single-record response (visibility, etc.)
        // are enforced at the query level when the record is fetched.
        var probeQuery = new AuditEventQueryRequest();
        var deny = AuthorizeQuery(probeQuery);
        if (deny is not null) return deny;

        var traceId = TraceIdAccessor.Current();
        var record  = await _queryService.GetByAuditIdAsync(auditId, ct);

        if (record is null)
        {
            return NotFound(ApiResponse<object>.Fail(
                $"Audit event with id '{auditId}' was not found.",
                traceId: traceId));
        }

        return Ok(ApiResponse<AuditEventRecordResponse>.Ok(record, traceId: traceId));
    }

    // ── GET /audit/entity/{entityType}/{entityId} ─────────────────────────────

    /// <summary>
    /// Retrieve all audit events that targeted a specific resource.
    /// Path segments are applied as exact-match filters.
    /// </summary>
    /// <param name="entityType">Resource type (e.g. "User", "Document").</param>
    /// <param name="entityId">Resource identifier.</param>
    /// <param name="query">Additional filters and pagination parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Query succeeded.</response>
    /// <response code="401">No valid credentials were presented.</response>
    /// <response code="403">Caller's scope is insufficient.</response>
    [HttpGet("entity/{entityType}/{entityId}")]
    [ProducesResponseType(typeof(ApiResponse<AuditEventQueryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>),                  StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>),                  StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetEntityEvents(
        string entityType,
        string entityId,
        [FromQuery] AuditEventQueryRequest query,
        CancellationToken ct)
    {
        query.EntityType = entityType;
        query.EntityId   = entityId;

        var deny = AuthorizeQuery(query);
        if (deny is not null) return deny;

        var result  = await _queryService.QueryAsync(query, ct);
        var traceId = TraceIdAccessor.Current();

        return Ok(ApiResponse<AuditEventQueryResponse>.Ok(result, traceId: traceId));
    }

    // ── GET /audit/actor/{actorId} ────────────────────────────────────────────

    /// <summary>
    /// Retrieve all audit events performed by a specific actor.
    /// </summary>
    /// <param name="actorId">The stable actor identifier.</param>
    /// <param name="query">Additional filters and pagination parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Query succeeded.</response>
    /// <response code="401">No valid credentials were presented.</response>
    /// <response code="403">Caller's scope is insufficient.</response>
    [HttpGet("actor/{actorId}")]
    [ProducesResponseType(typeof(ApiResponse<AuditEventQueryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>),                  StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>),                  StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetActorEvents(
        string actorId,
        [FromQuery] AuditEventQueryRequest query,
        CancellationToken ct)
    {
        query.ActorId = actorId;

        var deny = AuthorizeQuery(query);
        if (deny is not null) return deny;

        var result  = await _queryService.QueryAsync(query, ct);
        var traceId = TraceIdAccessor.Current();

        return Ok(ApiResponse<AuditEventQueryResponse>.Ok(result, traceId: traceId));
    }

    // ── GET /audit/user/{userId} ──────────────────────────────────────────────

    /// <summary>
    /// Retrieve all audit events associated with a specific user.
    /// <c>actorType = User</c> is enforced server-side.
    /// </summary>
    /// <param name="userId">The user's stable identifier.</param>
    /// <param name="query">Additional filters and pagination parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Query succeeded.</response>
    /// <response code="401">No valid credentials were presented.</response>
    /// <response code="403">Caller's scope is insufficient.</response>
    [HttpGet("user/{userId}")]
    [ProducesResponseType(typeof(ApiResponse<AuditEventQueryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>),                  StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>),                  StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetUserEvents(
        string userId,
        [FromQuery] AuditEventQueryRequest query,
        CancellationToken ct)
    {
        query.ActorId   = userId;
        query.ActorType = Enums.ActorType.User;

        var deny = AuthorizeQuery(query);
        if (deny is not null) return deny;

        var result  = await _queryService.QueryAsync(query, ct);
        var traceId = TraceIdAccessor.Current();

        return Ok(ApiResponse<AuditEventQueryResponse>.Ok(result, traceId: traceId));
    }

    // ── GET /audit/tenant/{tenantId} ──────────────────────────────────────────

    /// <summary>
    /// Retrieve all audit events scoped to a specific tenant.
    /// For non-PlatformAdmin callers, the authorizer overrides this to the caller's own tenant.
    /// </summary>
    /// <param name="tenantId">Tenant identifier.</param>
    /// <param name="query">Additional filters and pagination parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Query succeeded.</response>
    /// <response code="401">No valid credentials were presented.</response>
    /// <response code="403">Caller's scope is insufficient or tenant mismatch.</response>
    [HttpGet("tenant/{tenantId}")]
    [ProducesResponseType(typeof(ApiResponse<AuditEventQueryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>),                  StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>),                  StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetTenantEvents(
        string tenantId,
        [FromQuery] AuditEventQueryRequest query,
        CancellationToken ct)
    {
        query.TenantId = tenantId;

        var deny = AuthorizeQuery(query);
        if (deny is not null) return deny;

        var result  = await _queryService.QueryAsync(query, ct);
        var traceId = TraceIdAccessor.Current();

        return Ok(ApiResponse<AuditEventQueryResponse>.Ok(result, traceId: traceId));
    }

    // ── GET /audit/organization/{organizationId} ──────────────────────────────

    /// <summary>
    /// Retrieve all audit events scoped to a specific organization.
    /// </summary>
    /// <param name="organizationId">Organization identifier.</param>
    /// <param name="query">Additional filters and pagination parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Query succeeded.</response>
    /// <response code="401">No valid credentials were presented.</response>
    /// <response code="403">Caller's scope is insufficient.</response>
    [HttpGet("organization/{organizationId}")]
    [ProducesResponseType(typeof(ApiResponse<AuditEventQueryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>),                  StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>),                  StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetOrganizationEvents(
        string organizationId,
        [FromQuery] AuditEventQueryRequest query,
        CancellationToken ct)
    {
        query.OrganizationId = organizationId;

        var deny = AuthorizeQuery(query);
        if (deny is not null) return deny;

        var result  = await _queryService.QueryAsync(query, ct);
        var traceId = TraceIdAccessor.Current();

        return Ok(ApiResponse<AuditEventQueryResponse>.Ok(result, traceId: traceId));
    }

    // ── Private authorization helper ──────────────────────────────────────────

    /// <summary>
    /// Resolves the caller context from HttpContext.Items, calls the authorizer,
    /// and returns an error IActionResult if the query is denied.
    /// Returns null when the caller is authorized — the action may proceed.
    ///
    /// The query is mutated in-place by the authorizer when authorization succeeds.
    /// </summary>
    private IActionResult? AuthorizeQuery(AuditEventQueryRequest query)
    {
        var traceId = TraceIdAccessor.Current();

        // Resolve caller from HttpContext.Items (set by QueryAuthMiddleware).
        // Fall back to Anonymous if not present (e.g. tests that bypass middleware).
        var caller = HttpContext.Items.TryGetValue(QueryCallerContext.ItemKey, out var raw)
            && raw is IQueryCallerContext ctx
                ? ctx
                : QueryCallerContext.Anonymous();

        var result = _authorizer.Authorize(caller, query);

        if (result.IsAuthorized) return null;

        _logger.LogWarning(
            "Query access denied. Scope={Scope} StatusCode={Status} Reason={Reason} TraceId={TraceId}",
            caller.Scope, result.StatusCode, result.DenialReason, traceId);

        return result.StatusCode switch
        {
            StatusCodes.Status401Unauthorized =>
                Unauthorized(ApiResponse<object>.Fail(result.DenialReason!, traceId: traceId)),
            _ =>
                StatusCode(StatusCodes.Status403Forbidden,
                    ApiResponse<object>.Fail(result.DenialReason!, traceId: traceId)),
        };
    }
}
