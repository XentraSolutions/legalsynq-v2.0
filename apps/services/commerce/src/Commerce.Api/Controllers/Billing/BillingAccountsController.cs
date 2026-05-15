using Commerce.Application.Billing.Abstractions;
using Commerce.Contracts.Billing;
using Microsoft.AspNetCore.Mvc;

namespace Commerce.Api.Controllers.Billing;

[ApiController]
[Route("api/commerce/billing-accounts")]
public sealed class BillingAccountsController : ControllerBase
{
    private readonly IBillingAccountService _service;

    public BillingAccountsController(IBillingAccountService service) => _service = service;

    [HttpPost]
    [ProducesResponseType(typeof(BillingAccountResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<BillingAccountResponse>> Create(
        [FromBody] CreateBillingAccountRequest request, CancellationToken ct)
    {
        var result = await _service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<BillingAccountResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BillingAccountResponse>>> List(CancellationToken ct)
        => Ok(await _service.ListAsync(ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(BillingAccountResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BillingAccountResponse>> GetById(Guid id, CancellationToken ct)
        => Ok(await _service.GetAsync(id, ct));

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(BillingAccountResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<BillingAccountResponse>> Update(
        Guid id, [FromBody] UpdateBillingAccountRequest request, CancellationToken ct)
        => Ok(await _service.UpdateAsync(id, request, ct));

    [HttpPost("{id:guid}/activate")]
    [ProducesResponseType(typeof(BillingAccountResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<BillingAccountResponse>> Activate(Guid id, CancellationToken ct)
        => Ok(await _service.ActivateAsync(id, ct));

    [HttpPost("{id:guid}/suspend")]
    [ProducesResponseType(typeof(BillingAccountResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<BillingAccountResponse>> Suspend(Guid id, CancellationToken ct)
        => Ok(await _service.SuspendAsync(id, ct));

    [HttpPost("{id:guid}/close")]
    [ProducesResponseType(typeof(BillingAccountResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<BillingAccountResponse>> Close(Guid id, CancellationToken ct)
        => Ok(await _service.CloseAsync(id, ct));
}
