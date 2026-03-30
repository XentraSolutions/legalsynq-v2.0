using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using PlatformAuditEventService.DTOs;
using PlatformAuditEventService.Services;
using PlatformAuditEventService.Utilities;

namespace PlatformAuditEventService.Controllers;

/// <summary>
/// Audit event ingestion and retrieval endpoints.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class AuditEventsController : ControllerBase
{
    private readonly IAuditEventService          _service;
    private readonly IValidator<IngestAuditEventRequest> _validator;

    public AuditEventsController(
        IAuditEventService           service,
        IValidator<IngestAuditEventRequest> validator)
    {
        _service   = service;
        _validator = validator;
    }

    /// <summary>
    /// Ingest a single audit/event record from a distributed source system.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<AuditEventResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Ingest(
        [FromBody] IngestAuditEventRequest request,
        CancellationToken ct)
    {
        var validation = await _validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var errors = validation.Errors.Select(e => e.ErrorMessage).ToList();
            return BadRequest(ApiResponse<object>.ValidationFail(errors, TraceIdAccessor.Current()));
        }

        var result = await _service.IngestAsync(request, ct);
        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Id },
            ApiResponse<AuditEventResponse>.Ok(result, traceId: TraceIdAccessor.Current()));
    }

    /// <summary>
    /// Retrieve a single audit event by its unique identifier.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AuditEventResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await _service.GetByIdAsync(id, ct);
        if (result is null)
            return NotFound(ApiResponse<object>.Fail("Audit event not found.", traceId: TraceIdAccessor.Current()));

        return Ok(ApiResponse<AuditEventResponse>.Ok(result, traceId: TraceIdAccessor.Current()));
    }

    /// <summary>
    /// Query audit events with optional filters. Supports tenant, actor, event type,
    /// category, severity, outcome, date range, and pagination.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<AuditEventResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Query(
        [FromQuery] AuditEventQueryRequest query,
        CancellationToken ct)
    {
        var result = await _service.QueryAsync(query, ct);
        return Ok(ApiResponse<PagedResult<AuditEventResponse>>.Ok(result, traceId: TraceIdAccessor.Current()));
    }
}
