using Microsoft.AspNetCore.Mvc;
using Billing.Api.Contracts;
using Billing.Api.Security;
using Billing.Api.Tenancy;
using Billing.Domain.Services;

namespace Billing.Api.Controllers;

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
    [RequireTenantBillingAccess(TenantBillingOperationCategory.CustomerWrite)]
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

    /// <summary>
    /// GET /api/customers/by-external-reference?value=...
    ///
    /// MS-BILL-UI-017 — exact, tenant-scoped (X-Tenant-Id) lookup by
    /// <c>ExternalReference</c>. Returns:
    /// <list type="bullet">
    ///   <item>200 with the matching <see cref="CustomerResponse"/>
    ///         when exactly one active customer in this tenant has the
    ///         supplied external reference.</item>
    ///   <item>404 when no active match exists, or when the value is
    ///         empty/whitespace.</item>
    ///   <item>409 when two or more active customers in this tenant
    ///         share the same external reference (an operator must
    ///         deduplicate first).</item>
    /// </list>
    /// The route literal <c>by-external-reference</c> is a non-GUID
    /// segment, so MVC routing can never confuse it with the
    /// <c>{id:guid}</c> action below.
    /// </summary>
    [HttpGet("by-external-reference")]
    [ProducesResponseType(typeof(CustomerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GetByExternalReference(
        [FromQuery] string? value,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Problem(
                detail: "Query parameter 'value' is required.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var result = await _service.GetByExternalReferenceAsync(_tenant.TenantId, value, ct);

        return result.Outcome switch
        {
            CustomerLookupOutcome.Found =>
                Ok(CustomerResponse.From(result.Customer!)),
            CustomerLookupOutcome.Ambiguous =>
                Problem(
                    detail: "Multiple active customers in this tenant share the supplied external reference.",
                    statusCode: StatusCodes.Status409Conflict),
            _ => NotFound(),
        };
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
    [RequireTenantBillingAccess(TenantBillingOperationCategory.CustomerWrite)]
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
    [RequireTenantBillingAccess(TenantBillingOperationCategory.CustomerWrite)]
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
