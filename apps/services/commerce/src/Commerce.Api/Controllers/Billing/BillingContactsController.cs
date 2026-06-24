using Commerce.Application.Billing.Abstractions;
using Commerce.Contracts.Billing;
using Microsoft.AspNetCore.Mvc;

namespace Commerce.Api.Controllers.Billing;

[ApiController]
[Route("api/commerce/billing-accounts/{id:guid}/contacts")]
public sealed class BillingContactsController : ControllerBase
{
    private readonly IBillingContactService _service;

    public BillingContactsController(IBillingContactService service) => _service = service;

    [HttpPost]
    [ProducesResponseType(typeof(BillingContactResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<BillingContactResponse>> Add(
        Guid id, [FromBody] CreateBillingContactRequest request, CancellationToken ct)
    {
        var result = await _service.AddAsync(id, request, ct);
        return CreatedAtAction(nameof(List), new { id }, result);
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<BillingContactResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BillingContactResponse>>> List(Guid id, CancellationToken ct)
        => Ok(await _service.ListAsync(id, ct));

    [HttpPut("{contactId:guid}")]
    [ProducesResponseType(typeof(BillingContactResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<BillingContactResponse>> Update(
        Guid id, Guid contactId, [FromBody] UpdateBillingContactRequest request, CancellationToken ct)
        => Ok(await _service.UpdateAsync(id, contactId, request, ct));

    [HttpPost("{contactId:guid}/make-primary")]
    [ProducesResponseType(typeof(BillingContactResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<BillingContactResponse>> MakePrimary(
        Guid id, Guid contactId, CancellationToken ct)
        => Ok(await _service.MakePrimaryAsync(id, contactId, ct));
}
