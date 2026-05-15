using Microsoft.AspNetCore.Mvc;
using TenantBilling.Api.Contracts;
using TenantBilling.Api.Tenancy;
using TenantBilling.Domain.Statements;
using TenantBilling.Domain.StatementTemplates;

namespace TenantBilling.Api.Controllers;

/// <summary>
/// STAT-B01 + STAT-B02 — Tenant-scoped customer statement endpoints.
///
/// STAT-B01 routes (preserved unchanged):
/// <list type="bullet">
///   <item><c>GET /api/statements/customers/{customerId}</c> – build JSON statement.</item>
///   <item><c>GET /api/statements/customers/{customerId}/render/html</c> – render HTML.</item>
///   <item><c>GET /api/statements/customers/{customerId}/monthly</c> – monthly JSON shortcut.</item>
/// </list>
///
/// STAT-B02 routes (persisted snapshots):
/// <list type="bullet">
///   <item><c>POST .../generate</c> – build, snapshot, and persist.</item>
///   <item><c>POST .../monthly/generate</c> – monthly variant.</item>
///   <item><c>GET  .../history</c> – per-customer list of persisted snapshots.</item>
///   <item><c>GET  /api/statements/history/{id}</c> – fetch one persisted snapshot.</item>
///   <item><c>GET  /api/statements/history/{id}/render/html</c> – render persisted snapshot.</item>
///   <item><c>POST /api/statements/history/{id}/void</c> – soft-void a snapshot.</item>
/// </list>
///
/// All routes flow through <see cref="TenantResolutionMiddleware"/>, so
/// the controller can rely on <see cref="ITenantContext"/> resolving to a
/// non-empty GUID; missing / invalid headers are short-circuited with
/// HTTP 400 by the middleware before this controller runs.
/// </summary>
[ApiController]
[Route("api/statements")]
public sealed class StatementsController : ControllerBase
{
    private readonly ICustomerStatementService _statements;
    private readonly ICustomerStatementPersistenceService _persistence;
    private readonly ITenantContext _tenant;

    public StatementsController(
        ICustomerStatementService statements,
        ICustomerStatementPersistenceService persistence,
        ITenantContext tenant)
    {
        _statements = statements;
        _persistence = persistence;
        _tenant = tenant;
    }

    // ===============================================================
    // STAT-B01 — preserved verbatim
    // ===============================================================

    [HttpGet("customers/{customerId:guid}")]
    [ProducesResponseType(typeof(CustomerStatementDocument), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetJson(
        [FromRoute] Guid customerId,
        [FromQuery] StatementPeriodQuery query,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        return await BuildJsonAsync(customerId, query.From!.Value, query.To!.Value, ct);
    }

    [HttpGet("customers/{customerId:guid}/render/html")]
    [Produces("text/html")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetHtml(
        [FromRoute] Guid customerId,
        [FromQuery] StatementPeriodQuery query,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        try
        {
            var html = await _statements.RenderHtmlAsync(
                _tenant.TenantId, customerId, query.From!.Value, query.To!.Value, ct);
            if (html is null)
            {
                return Problem(
                    detail: $"Customer '{customerId}' was not found.",
                    statusCode: StatusCodes.Status404NotFound);
            }
            return Content(html, "text/html");
        }
        catch (StatementValidationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpGet("customers/{customerId:guid}/monthly")]
    [ProducesResponseType(typeof(CustomerStatementDocument), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMonthlyJson(
        [FromRoute] Guid customerId,
        [FromQuery] StatementMonthlyQuery query,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var year = query.Year!.Value;
        var month = query.Month!.Value;
        var from = new DateTime(year, month, 1);
        var to = from.AddMonths(1).AddDays(-1);

        return await BuildJsonAsync(customerId, from, to, ct);
    }

    private async Task<IActionResult> BuildJsonAsync(
        Guid customerId, DateTime from, DateTime to, CancellationToken ct)
    {
        try
        {
            var doc = await _statements.BuildStatementAsync(
                _tenant.TenantId, customerId, from, to, ct);
            if (doc is null)
            {
                return Problem(
                    detail: $"Customer '{customerId}' was not found.",
                    statusCode: StatusCodes.Status404NotFound);
            }
            return Ok(doc);
        }
        catch (StatementValidationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    // ===============================================================
    // STAT-B02 — persisted snapshots
    // ===============================================================

    [HttpPost("customers/{customerId:guid}/generate")]
    [ProducesResponseType(typeof(CustomerStatementResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Generate(
        [FromRoute] Guid customerId,
        [FromBody] GenerateStatementRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        return await PersistResponseAsync(() => _persistence.GenerateAsync(
            _tenant.TenantId, customerId,
            request.PeriodStart!.Value, request.PeriodEnd!.Value,
            request.TemplateId, request.RenderHtml, ct), customerId);
    }

    [HttpPost("customers/{customerId:guid}/monthly/generate")]
    [ProducesResponseType(typeof(CustomerStatementResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GenerateMonthly(
        [FromRoute] Guid customerId,
        [FromBody] GenerateMonthlyStatementRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        return await PersistResponseAsync(() => _persistence.GenerateMonthlyAsync(
            _tenant.TenantId, customerId,
            request.Year!.Value, request.Month!.Value,
            request.TemplateId, request.RenderHtml, ct), customerId);
    }

    [HttpGet("customers/{customerId:guid}/history")]
    [ProducesResponseType(typeof(IReadOnlyList<CustomerStatementSummaryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ListHistory([FromRoute] Guid customerId, CancellationToken ct)
    {
        try
        {
            var items = await _persistence.ListHistoryAsync(_tenant.TenantId, customerId, ct);
            return Ok(items.Select(CustomerStatementSummaryResponse.From).ToList());
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpGet("history/{id:guid}")]
    [ProducesResponseType(typeof(CustomerStatementResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetHistory([FromRoute] Guid id, CancellationToken ct)
    {
        try
        {
            var s = await _persistence.GetHistoryAsync(_tenant.TenantId, id, ct);
            return s is null ? NotFound() : Ok(CustomerStatementResponse.From(s));
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpGet("history/{id:guid}/render/html")]
    [Produces("text/html")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RenderHistoryHtml([FromRoute] Guid id, CancellationToken ct)
    {
        try
        {
            var html = await _persistence.RenderHtmlAsync(_tenant.TenantId, id, ct);
            return html is null ? NotFound() : Content(html, "text/html");
        }
        catch (StatementValidationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpPost("history/{id:guid}/void")]
    [ProducesResponseType(typeof(CustomerStatementResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Void(
        [FromRoute] Guid id,
        [FromBody] VoidStatementRequest? request,
        CancellationToken ct)
    {
        try
        {
            var s = await _persistence.VoidAsync(_tenant.TenantId, id, request?.Reason, ct);
            return s is null ? NotFound() : Ok(CustomerStatementResponse.From(s));
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    /// <summary>
    /// Common error mapping for the persistence-write endpoints. The
    /// service-layer exception → status code chart:
    /// <list type="bullet">
    ///   <item><see cref="StatementValidationException"/> → 400</item>
    ///   <item><see cref="StatementTemplateNotFoundInScopeException"/> → 400</item>
    ///   <item><see cref="StatementTemplateNotSelectableException"/> → 400</item>
    ///   <item><see cref="ArgumentException"/> → 400</item>
    ///   <item><see cref="CustomerStatementNumberConflictException"/> → 409</item>
    /// </list>
    /// A null result is mapped to 404 (customer missing in scope).
    /// </summary>
    private async Task<IActionResult> PersistResponseAsync(
        Func<Task<Domain.Entities.CustomerStatement?>> action, Guid customerId)
    {
        try
        {
            var s = await action();
            if (s is null)
            {
                return Problem(
                    detail: $"Customer '{customerId}' was not found.",
                    statusCode: StatusCodes.Status404NotFound);
            }
            return CreatedAtAction(nameof(GetHistory), new { id = s.Id },
                CustomerStatementResponse.From(s));
        }
        catch (StatementValidationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (StatementTemplateNotFoundInScopeException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (StatementTemplateNotSelectableException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (CustomerStatementNumberConflictException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict);
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }
}
