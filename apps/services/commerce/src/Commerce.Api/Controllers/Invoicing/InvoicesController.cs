using Commerce.Application.Invoicing.Abstractions;
using Commerce.Contracts.Invoicing;
using Microsoft.AspNetCore.Mvc;

namespace Commerce.Api.Controllers.Invoicing;

[ApiController]
[Route("api/commerce/invoices")]
public sealed class InvoicesController : ControllerBase
{
    private readonly IInvoiceService _service;
    public InvoicesController(IInvoiceService service) => _service = service;

    [HttpPost]
    [ProducesResponseType(typeof(InvoiceResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<InvoiceResponse>> Create(
        [FromBody] CreateInvoiceRequest request, CancellationToken ct)
    {
        var created = await _service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(InvoiceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InvoiceResponse>> Get(Guid id, CancellationToken ct)
        => Ok(await _service.GetAsync(id, ct));

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<InvoiceResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<InvoiceResponse>>> List(
        [FromQuery] int take = 50, CancellationToken ct = default)
        => Ok(await _service.ListAsync(take, ct));
}

[ApiController]
[Route("api/commerce/billing-accounts/{billingAccountId:guid}/invoices")]
public sealed class BillingAccountInvoicesController : ControllerBase
{
    private readonly IInvoiceService _service;
    public BillingAccountInvoicesController(IInvoiceService service) => _service = service;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<InvoiceResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<InvoiceResponse>>> List(
        Guid billingAccountId, CancellationToken ct)
        => Ok(await _service.ListForBillingAccountAsync(billingAccountId, ct));
}

[ApiController]
[Route("api/commerce/subscriptions/{subscriptionId:guid}/invoices")]
public sealed class SubscriptionInvoicesController : ControllerBase
{
    private readonly IInvoiceService _service;
    public SubscriptionInvoicesController(IInvoiceService service) => _service = service;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<InvoiceResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<InvoiceResponse>>> List(
        Guid subscriptionId, CancellationToken ct)
        => Ok(await _service.ListForSubscriptionAsync(subscriptionId, ct));
}
