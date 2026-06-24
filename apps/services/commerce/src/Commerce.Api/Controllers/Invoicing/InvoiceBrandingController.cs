using Commerce.Application.Invoicing.Abstractions;
using Commerce.Contracts.Invoicing;
using Microsoft.AspNetCore.Mvc;

namespace Commerce.Api.Controllers.Invoicing;

[ApiController]
[Route("api/commerce/invoice-branding")]
public sealed class InvoiceBrandingController : ControllerBase
{
    private readonly IInvoiceBrandingService _service;

    public InvoiceBrandingController(IInvoiceBrandingService service)
        => _service = service;

    [HttpGet]
    [ProducesResponseType(typeof(InvoiceBrandingResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<InvoiceBrandingResponse>> Get(CancellationToken ct)
        => Ok(await _service.GetAsync(ct));

    [HttpPut]
    [ProducesResponseType(typeof(InvoiceBrandingResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<InvoiceBrandingResponse>> Update(
        [FromBody] UpdateInvoiceBrandingRequest request,
        CancellationToken ct)
        => Ok(await _service.UpdateAsync(request, ct));
}
