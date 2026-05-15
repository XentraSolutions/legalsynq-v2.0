using Microsoft.AspNetCore.Mvc;
using Billing.Api.Contracts;
using Billing.Api.Tenancy;
using Billing.Domain.Services;

namespace Billing.Api.Controllers;

/// <summary>
/// TB-DATA-01 — REST surface over <see cref="ITenantBillingProfileService"/>.
/// Tenant-scoped via <see cref="ITenantContext"/> (X-Tenant-Id middleware);
/// gated by <c>RequireInternalTokenMiddleware</c>. Cross-tenant access by
/// id surfaces as 404 — never as 403 — so existence is not leaked.
/// </summary>
[ApiController]
[Route("api/tenant-billing/profiles")]
public sealed class TenantBillingProfilesController : ControllerBase
{
    private readonly ITenantBillingProfileService _service;
    private readonly ITenantContext _tenant;

    public TenantBillingProfilesController(ITenantBillingProfileService service, ITenantContext tenant)
    {
        _service = service;
        _tenant  = tenant;
    }

    [HttpPost]
    [ProducesResponseType(typeof(TenantBillingProfileResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateTenantBillingProfileRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        try
        {
            var created = await _service.CreateAsync(
                _tenant.TenantId,
                request.BillingAccountId,
                request.HostPlatformKey,
                request.ExternalTenantId,
                request.Mode,
                request.Notes,
                ct);
            var dto = TenantBillingProfileResponse.From(created);
            return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
        }
        catch (TenantBillingProfileConflictException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict);
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpGet]
    [ProducesResponseType(typeof(TenantBillingProfileListResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = ITenantBillingProfileService.DefaultPageSize,
        CancellationToken ct = default)
    {
        var result = await _service.ListAsync(_tenant.TenantId, page, pageSize, ct);
        return Ok(new TenantBillingProfileListResponse(
            result.Items.Select(TenantBillingProfileResponse.From).ToList(),
            result.Page, result.PageSize, result.TotalCount, result.TotalPages));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(TenantBillingProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        if (id == Guid.Empty)
            return Problem(detail: "id is required.", statusCode: StatusCodes.Status400BadRequest);

        var found = await _service.GetAsync(_tenant.TenantId, id, ct);
        return found is null
            ? Problem(detail: $"Profile {id} not found.", statusCode: StatusCodes.Status404NotFound)
            : Ok(TenantBillingProfileResponse.From(found));
    }

    [HttpGet("by-billing-account/{billingAccountId:guid}")]
    [ProducesResponseType(typeof(TenantBillingProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByBillingAccount(Guid billingAccountId, CancellationToken ct)
    {
        if (billingAccountId == Guid.Empty)
            return Problem(detail: "billingAccountId is required.", statusCode: StatusCodes.Status400BadRequest);

        var found = await _service.GetByBillingAccountAsync(_tenant.TenantId, billingAccountId, ct);
        return found is null
            ? Problem(detail: $"No open profile for billing account {billingAccountId}.", statusCode: StatusCodes.Status404NotFound)
            : Ok(TenantBillingProfileResponse.From(found));
    }

    [HttpPost("{id:guid}/activate")]
    [ProducesResponseType(typeof(TenantBillingProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public Task<IActionResult> Activate(Guid id, CancellationToken ct)
        => RunTransitionAsync(() => _service.ActivateAsync(_tenant.TenantId, id, ct));

    [HttpPost("{id:guid}/suspend")]
    [ProducesResponseType(typeof(TenantBillingProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public Task<IActionResult> Suspend(Guid id, CancellationToken ct)
        => RunTransitionAsync(() => _service.SuspendAsync(_tenant.TenantId, id, ct));

    [HttpPost("{id:guid}/close")]
    [ProducesResponseType(typeof(TenantBillingProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public Task<IActionResult> Close(Guid id, CancellationToken ct)
        => RunTransitionAsync(() => _service.CloseAsync(_tenant.TenantId, id, ct));

    private async Task<IActionResult> RunTransitionAsync(Func<Task<Domain.Entities.TenantBillingProfile>> action)
    {
        try
        {
            var updated = await action();
            return Ok(TenantBillingProfileResponse.From(updated));
        }
        catch (TenantBillingProfileNotFoundException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status404NotFound);
        }
        catch (InvalidTenantBillingProfileTransitionException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict);
        }
        catch (TenantBillingProfileConflictException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict);
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }
}
