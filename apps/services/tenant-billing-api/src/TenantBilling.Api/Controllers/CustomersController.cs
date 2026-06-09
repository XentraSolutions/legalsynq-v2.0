using Microsoft.AspNetCore.Mvc;
using TenantBilling.Api.Contracts;
using TenantBilling.Api.Tenancy;
using TenantBilling.Domain.Services;

namespace TenantBilling.Api.Controllers;

[ApiController]
[Route("api/customers")]
public sealed class CustomersController : ControllerBase
{
    private readonly ICustomerService _service;
    private readonly ITenantContext _tenant;

    public CustomersController(ICustomerService service, ITenantContext tenant)
    {
        _service = service;
        _tenant = tenant;
    }

    /// <summary>POST /api/customers — create a customer for the request's tenant (X-Tenant-Id).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(CustomerResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateCustomerRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        try
        {
            var created = await _service.CreateAsync(
                _tenant.TenantId,
                request.Name,
                request.Email,
                request.Phone,
                request.BillingAddress,
                request.ExternalReference,
                request.Notes,
                request.ToBillingAddressDetails(),
                ct);

            var dto = CustomerResponse.From(created);
            return CreatedAtAction(
                nameof(GetById),
                new { id = dto.Id },
                dto);
        }
        catch (DuplicateCustomerEmailException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict);
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    /// <summary>
    /// GET /api/customers?search=...&amp;page=1&amp;pageSize=25 — paginated, tenant-scoped list
    /// (tenant comes from X-Tenant-Id).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(CustomerListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = ICustomerService.DefaultPageSize,
        CancellationToken ct = default)
    {
        var result = await _service.ListAsync(_tenant.TenantId, search, page, pageSize, ct);
        var response = new CustomerListResponse(
            result.Items.Select(CustomerResponse.From).ToList(),
            result.Page,
            result.PageSize,
            result.TotalCount,
            result.TotalPages);
        return Ok(response);
    }

    /// <summary>GET /api/customers/{id} — tenant-scoped fetch (active customers only).</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CustomerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        if (id == Guid.Empty)
            return Problem(detail: "id is required.", statusCode: StatusCodes.Status400BadRequest);

        var c = await _service.GetAsync(_tenant.TenantId, id, ct);
        return c is null ? NotFound() : Ok(CustomerResponse.From(c));
    }

    /// <summary>PUT /api/customers/{id} — update a tenant-owned customer.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(CustomerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateCustomerRequest request,
        CancellationToken ct)
    {
        if (id == Guid.Empty)
            return Problem(detail: "id is required.", statusCode: StatusCodes.Status400BadRequest);
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        try
        {
            var updated = await _service.UpdateAsync(
                _tenant.TenantId,
                id,
                request.Name,
                request.Email,
                request.Phone,
                request.BillingAddress,
                request.ExternalReference,
                request.Notes,
                request.ToBillingAddressDetails(),
                ct);

            return updated is null ? NotFound() : Ok(CustomerResponse.From(updated));
        }
        catch (DuplicateCustomerEmailException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict);
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    /// <summary>DELETE /api/customers/{id} — soft delete (sets IsDeleted=true).</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (id == Guid.Empty)
            return Problem(detail: "id is required.", statusCode: StatusCodes.Status400BadRequest);

        var deleted = await _service.DeleteAsync(_tenant.TenantId, id, ct);
        return deleted ? NoContent() : NotFound();
    }
}
