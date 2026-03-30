using Microsoft.AspNetCore.Mvc;
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
/// All endpoints return records in the canonical <see cref="AuditEventRecordResponse"/>
/// shape, wrapped in <see cref="ApiResponse{T}"/>.
///
/// Pagination: all list endpoints accept <c>page</c>, <c>pageSize</c>, <c>sortBy</c>,
/// and <c>sortDescending</c> query parameters. Page size is capped at the server-side
/// <c>QueryAuth:MaxPageSize</c> (default 500).
///
/// Scoped endpoints (entity, actor, user, tenant, organization) accept additional
/// filter parameters from the query string. The path segment always takes precedence
/// over the corresponding query-string field.
/// </summary>
[ApiController]
[Route("audit")]
[Produces("application/json")]
public sealed class AuditEventQueryController : ControllerBase
{
    private readonly IAuditEventQueryService             _queryService;
    private readonly ILogger<AuditEventQueryController>  _logger;

    public AuditEventQueryController(
        IAuditEventQueryService            queryService,
        ILogger<AuditEventQueryController> logger)
    {
        _queryService = queryService;
        _logger       = logger;
    }

    // ── GET /audit/events ─────────────────────────────────────────────────────

    /// <summary>
    /// Execute a filtered, paginated query over all accessible audit event records.
    ///
    /// All filter parameters are optional. Omitting a filter returns all records for
    /// that dimension. Multiple filters are AND-ed together.
    /// </summary>
    /// <param name="query">Filter and pagination parameters (bound from query string).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Query succeeded. Body contains the paginated result.</response>
    /// <response code="400">Invalid query parameters.</response>
    [HttpGet("events")]
    [ProducesResponseType(typeof(ApiResponse<AuditEventQueryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>),                  StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ListEvents(
        [FromQuery] AuditEventQueryRequest query,
        CancellationToken ct)
    {
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
    /// <response code="200">Record found. Body contains the full audit event record.</response>
    /// <response code="404">No record exists with the given AuditId.</response>
    [HttpGet("events/{auditId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AuditEventRecordResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>),                   StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEvent(Guid auditId, CancellationToken ct)
    {
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
    ///
    /// The <c>entityType</c> and <c>entityId</c> path segments are applied as exact-match
    /// filters. Additional query parameters further narrow the result set.
    /// </summary>
    /// <param name="entityType">Resource type (e.g. "User", "Document", "Appointment").</param>
    /// <param name="entityId">Resource identifier.</param>
    /// <param name="query">Additional filters and pagination parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Query succeeded. Body contains the paginated result.</response>
    [HttpGet("entity/{entityType}/{entityId}")]
    [ProducesResponseType(typeof(ApiResponse<AuditEventQueryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEntityEvents(
        string entityType,
        string entityId,
        [FromQuery] AuditEventQueryRequest query,
        CancellationToken ct)
    {
        query.EntityType = entityType;
        query.EntityId   = entityId;

        var result  = await _queryService.QueryAsync(query, ct);
        var traceId = TraceIdAccessor.Current();

        _logger.LogDebug(
            "GET /audit/entity/{EntityType}/{EntityId} → TotalCount={Total} TraceId={TraceId}",
            entityType, entityId, result.TotalCount, traceId);

        return Ok(ApiResponse<AuditEventQueryResponse>.Ok(result, traceId: traceId));
    }

    // ── GET /audit/actor/{actorId} ────────────────────────────────────────────

    /// <summary>
    /// Retrieve all audit events performed by a specific actor.
    ///
    /// Returns events where <c>Actor.Id</c> matches <paramref name="actorId"/>.
    /// Additional query parameters further narrow the result set.
    /// </summary>
    /// <param name="actorId">The stable actor identifier.</param>
    /// <param name="query">Additional filters and pagination parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Query succeeded. Body contains the paginated result.</response>
    [HttpGet("actor/{actorId}")]
    [ProducesResponseType(typeof(ApiResponse<AuditEventQueryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActorEvents(
        string actorId,
        [FromQuery] AuditEventQueryRequest query,
        CancellationToken ct)
    {
        query.ActorId = actorId;

        var result  = await _queryService.QueryAsync(query, ct);
        var traceId = TraceIdAccessor.Current();

        return Ok(ApiResponse<AuditEventQueryResponse>.Ok(result, traceId: traceId));
    }

    // ── GET /audit/user/{userId} ──────────────────────────────────────────────

    /// <summary>
    /// Retrieve all audit events associated with a specific user.
    ///
    /// Equivalent to filtering by <c>actorId = userId</c> with <c>actorType = User</c>,
    /// returning all activity performed by or attributed to that user identity.
    /// Additional query parameters further narrow the result set.
    /// </summary>
    /// <param name="userId">The user's stable identifier.</param>
    /// <param name="query">Additional filters and pagination parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Query succeeded. Body contains the paginated result.</response>
    [HttpGet("user/{userId}")]
    [ProducesResponseType(typeof(ApiResponse<AuditEventQueryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserEvents(
        string userId,
        [FromQuery] AuditEventQueryRequest query,
        CancellationToken ct)
    {
        query.ActorId   = userId;
        query.ActorType = Enums.ActorType.User;

        var result  = await _queryService.QueryAsync(query, ct);
        var traceId = TraceIdAccessor.Current();

        return Ok(ApiResponse<AuditEventQueryResponse>.Ok(result, traceId: traceId));
    }

    // ── GET /audit/tenant/{tenantId} ──────────────────────────────────────────

    /// <summary>
    /// Retrieve all audit events scoped to a specific tenant.
    ///
    /// The <c>tenantId</c> path segment is applied as an exact-match filter.
    /// Additional query parameters further narrow the result set.
    /// </summary>
    /// <param name="tenantId">Tenant identifier.</param>
    /// <param name="query">Additional filters and pagination parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Query succeeded. Body contains the paginated result.</response>
    [HttpGet("tenant/{tenantId}")]
    [ProducesResponseType(typeof(ApiResponse<AuditEventQueryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTenantEvents(
        string tenantId,
        [FromQuery] AuditEventQueryRequest query,
        CancellationToken ct)
    {
        query.TenantId = tenantId;

        var result  = await _queryService.QueryAsync(query, ct);
        var traceId = TraceIdAccessor.Current();

        return Ok(ApiResponse<AuditEventQueryResponse>.Ok(result, traceId: traceId));
    }

    // ── GET /audit/organization/{organizationId} ──────────────────────────────

    /// <summary>
    /// Retrieve all audit events scoped to a specific organization.
    ///
    /// The <c>organizationId</c> path segment is applied as an exact-match filter.
    /// Additional query parameters further narrow the result set.
    /// </summary>
    /// <param name="organizationId">Organization identifier.</param>
    /// <param name="query">Additional filters and pagination parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Query succeeded. Body contains the paginated result.</response>
    [HttpGet("organization/{organizationId}")]
    [ProducesResponseType(typeof(ApiResponse<AuditEventQueryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOrganizationEvents(
        string organizationId,
        [FromQuery] AuditEventQueryRequest query,
        CancellationToken ct)
    {
        query.OrganizationId = organizationId;

        var result  = await _queryService.QueryAsync(query, ct);
        var traceId = TraceIdAccessor.Current();

        return Ok(ApiResponse<AuditEventQueryResponse>.Ok(result, traceId: traceId));
    }
}
