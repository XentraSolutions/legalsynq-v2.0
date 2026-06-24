using Microsoft.AspNetCore.Mvc;
using TenantBilling.Api.Contracts;
using TenantBilling.Api.Tenancy;
using TenantBilling.Domain.Entities;
using TenantBilling.Domain.StatementTemplates;

namespace TenantBilling.Api.Controllers;

/// <summary>
/// STAT-B02 — Tenant-scoped REST surface for the statement template
/// catalogue. Mirrors the tenant half of
/// <see cref="InvoiceTemplatesController"/>; there is no platform tier.
///
/// Conflict ordering matters in every catch chain:
/// <see cref="StatementTemplateDefaultConflictException"/> → 409 must
/// come BEFORE <see cref="InvalidOperationException"/> → 400, since
/// the conflict type derives from <c>InvalidOperationException</c>.
/// </summary>
[ApiController]
[Route("api/statement-templates")]
public sealed class StatementTemplatesController : ControllerBase
{
    private readonly IStatementTemplateService _service;
    private readonly ITenantContext _tenant;

    public StatementTemplatesController(IStatementTemplateService service, ITenantContext tenant)
    {
        _service = service;
        _tenant = tenant;
    }

    [HttpPost("")]
    [ProducesResponseType(typeof(StatementTemplateResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateStatementTemplateRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        try
        {
            var created = await _service.CreateAsync(_tenant.TenantId, request.ToCommand(), ct);
            return CreatedAtAction(nameof(Get), new { id = created.Id }, StatementTemplateResponse.From(created));
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (StatementTemplateDefaultConflictException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpGet("")]
    [ProducesResponseType(typeof(IReadOnlyList<StatementTemplateSummaryResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var items = await _service.ListAsync(_tenant.TenantId, ct);
        return Ok(items.Select(StatementTemplateSummaryResponse.From).ToList());
    }

    [HttpGet("default")]
    [ProducesResponseType(typeof(StatementTemplateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDefault(CancellationToken ct)
    {
        var t = await _service.GetDefaultAsync(_tenant.TenantId, ct);
        return t is null ? NotFound() : Ok(StatementTemplateResponse.From(t));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(StatementTemplateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Get([FromRoute] Guid id, CancellationToken ct)
    {
        if (id == Guid.Empty)
            return Problem(detail: "Template id is required.", statusCode: StatusCodes.Status400BadRequest);
        try
        {
            var t = await _service.GetAsync(_tenant.TenantId, id, ct);
            return t is null ? NotFound() : Ok(StatementTemplateResponse.From(t));
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(StatementTemplateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] UpdateStatementTemplateRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        if (id == Guid.Empty)
            return Problem(detail: "Template id is required.", statusCode: StatusCodes.Status400BadRequest);
        try
        {
            var updated = await _service.UpdateAsync(_tenant.TenantId, id, request.ToCommand(), ct);
            return updated is null ? NotFound() : Ok(StatementTemplateResponse.From(updated));
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (StatementTemplateDefaultConflictException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpPost("{id:guid}/activate")]
    [ProducesResponseType(typeof(StatementTemplateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Activate([FromRoute] Guid id, CancellationToken ct)
    {
        if (id == Guid.Empty)
            return Problem(detail: "Template id is required.", statusCode: StatusCodes.Status400BadRequest);
        try
        {
            var t = await _service.ActivateAsync(_tenant.TenantId, id, ct);
            return t is null ? NotFound() : Ok(StatementTemplateResponse.From(t));
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpPost("{id:guid}/retire")]
    [ProducesResponseType(typeof(StatementTemplateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Retire([FromRoute] Guid id, CancellationToken ct)
    {
        if (id == Guid.Empty)
            return Problem(detail: "Template id is required.", statusCode: StatusCodes.Status400BadRequest);
        try
        {
            var t = await _service.RetireAsync(_tenant.TenantId, id, ct);
            return t is null ? NotFound() : Ok(StatementTemplateResponse.From(t));
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpPost("{id:guid}/make-default")]
    [ProducesResponseType(typeof(MakeDefaultStatementTemplateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> MakeDefault([FromRoute] Guid id, CancellationToken ct)
    {
        if (id == Guid.Empty)
            return Problem(detail: "Template id is required.", statusCode: StatusCodes.Status400BadRequest);

        StatementTemplate? previousDefault;
        try
        {
            previousDefault = await _service.GetDefaultAsync(_tenant.TenantId, ct);
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }

        try
        {
            var t = await _service.MakeDefaultAsync(_tenant.TenantId, id, ct);
            if (t is null) return NotFound();
            return Ok(new MakeDefaultStatementTemplateResponse(
                StatementTemplateResponse.From(t),
                previousDefault?.Id == t.Id ? null : previousDefault?.Id));
        }
        catch (StatementTemplateDefaultConflictException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }
}
